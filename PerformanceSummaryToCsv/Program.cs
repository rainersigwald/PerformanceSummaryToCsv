using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PerformanceSummaryToCsv
{
    class Program
    {
        async static Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<FileInfo[]>(
                    "--inputs",
                    "Input text files containing an MSBuild PerformanceSummary section",
                    ArgumentArity.OneOrMore),
                new Option<FileInfo>(
                    "--output",
                    getDefaultValue: () => new FileInfo("MSBuild_performance.csv"),
                    "Path of the final csv file."),
                new Option<bool>(
                    "--show",
                    () => false,
                    "Open a browser window with a comparison chart")
            };

            rootCommand.Description = "Converts and aggregates MSBuild text performance summaries into a CSV.";

            // Note that the parameters of the handler method are matched according to the names of the options
            rootCommand.Handler = CommandHandler.Create<FileInfo[], FileInfo, bool>(Run);

            // Parse the incoming args and invoke the handler
            return await rootCommand.InvokeAsync(args);
        }

        async static Task Run(FileInfo[] inputs, FileInfo output, bool show)
        {
            AggregateData aggregate = await Aggregate(inputs);

            await aggregate.WriteCsv(output.FullName);

            if (show)
            {
                aggregate.ShowChart();
            }
        }

        async static Task<AggregateData> Aggregate(FileInfo[] inputs)
        {
            Console.WriteLine($"Aggregating {string.Join(", ", inputs.Select(fi => fi.FullName))}");

            AggregateData aggregate = new();

            int numCols = inputs.Length;

            foreach (FileInfo item in inputs)
            {
                if (item.FullName.EndsWith(".etl.zip"))
                {
                    ReadETWFile(aggregate, item.FullName);
                }
                else
                {
                    await ReadFile(aggregate, item);
                }
            }

            return aggregate;
        }

        static void ReadETWFile(AggregateData aggregate, string fileName)
        {
            ZippedETLReader zipReader = new ZippedETLReader(fileName, Console.Out);
            zipReader.UnpackArchive();

            var traceLog = TraceLog.OpenOrConvert(fileName.Substring(0, fileName.Length - 4), new TraceLogOptions() { ConversionLog = Console.Out });
            var evts = traceLog.Events.Filter(e => e.ProviderName.Equals("Microsoft-Build"));
            Dictionary<string, Dictionary<int, double>> startTimes = new Dictionary<string, Dictionary<int, double>>();
            List<Tuple<string, string, double>> events = new();
            foreach (var evt in evts.Where(e => e.EventName.Contains("ExecuteTask") || e.EventName.Contains("Target")))
            {
                string key = evt.EventName.Contains("Target") ? "Target," + evt.PayloadValue(evt.PayloadIndex("targetName")) : "Task," + evt.PayloadValue(evt.PayloadIndex("taskName"));
                if (evt.EventName.Contains("Start"))
                {
                    if (startTimes.TryGetValue(key, out Dictionary<int, double>? latest))
                    {
                        latest[evt.ThreadID] = evt.TimeStampRelativeMSec;
                    }
                    else
                    {
                        startTimes.Add(key, new Dictionary<int, double>() { { evt.ThreadID, evt.TimeStampRelativeMSec } });
                    }
                }
                else
                {
                    try
                    {
                        double startTime = startTimes[key][evt.ThreadID];
                        events.Add(Tuple.Create(key.Split(',')[0], key.Split(',')[1], evt.TimeStampRelativeMSec - startTime));
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            events = events.GroupBy(t => t.Item2, (key, enumerable) => enumerable.Aggregate((f, s) => Tuple.Create(f.Item1, f.Item2, f.Item3 + s.Item3))).ToList();
            events.Sort((f, s) => f.Item3 > s.Item3 ? -1 : f.Item3 == s.Item3 ? 0 : 1);
            List<TaskSummary> tasks = new();
            foreach (var evt in events)
            {
                tasks.Add(new(evt.Item2, evt.Item3));
            }

            aggregate.AddBuild(fileName.Substring(0, fileName.Length - 8), tasks);
        }

        async static Task ReadFile(AggregateData aggregate, FileInfo item)
        {
            using var file = new StreamReader(item.FullName);

            List<TaskSummary> tasks = new();

            string? line = string.Empty;

            while (line != "Task Performance Summary:")
            {
                // Fast-forward through most of the file
                line = await file.ReadLineAsync();

                if (line is null)
                {
                    throw new FileFormatException($"File {item.FullName} didn't have a performance summary.");
                }
            }

            line = await file.ReadLineAsync(); // blank line

            if (line is null)
            {
                throw new FileFormatException($"File {item.FullName} ended prematurely.");
            }

            while (TaskSummary.TryParse(line, out var summary))
            {
                tasks.Add(summary);

                line = await file.ReadLineAsync();

                if (line is null)
                {
                    break;
                }
            }

            aggregate.AddBuild(item.Name, tasks);
        }
    }
}
