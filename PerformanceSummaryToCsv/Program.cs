using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PerformanceSummaryToCsv
{
    public class Program
    {
        async static Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<FileInfo[]>(
                    name: "--inputs",
                    description: "Input text files containing an MSBuild PerformanceSummary section; separate multiple files with a space")
                {
                    Arity = ArgumentArity.OneOrMore
                },
                new Option<FileInfo>(
                    "--output",
                    getDefaultValue: () => new FileInfo("MSBuild_performance.csv"),
                    "Path of the final csv file"),
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

            await Task.WhenAll(
                inputs.Select(input =>
                {
                    if (input.FullName.EndsWith(".etl.zip"))
                    {
                        return Task.Run(() => ReadETWFile(aggregate, input.FullName));
                    }
                    else if (input.FullName.EndsWith(".binlog"))
                    {
                        return ReadBinlog(aggregate, input.FullName);
                    }
                    else
                    {
                        return ReadFile(aggregate, input.FullName);
                    }

                }));

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

        public async static Task ReadFile(AggregateData aggregate, string filePath)
        {
            using var file = new StreamReader(filePath);

            await ReadFile(aggregate, file, filePath);
        }

        public async static Task ReadBinlog(AggregateData aggregate, string filePath)
        {
            ProcessStartInfo psi = new()
            {
                FileName = "dotnet",
                ArgumentList = { "msbuild", "-consoleLoggerParameters:verbosity=quiet;PerformanceSummary", filePath },
                RedirectStandardOutput = true,
            };

            var p = Process.Start(psi);
            var reader = ReadFile(aggregate, p.StandardOutput, filePath);

            await p.WaitForExitAsync();

            await reader;
        }

        public async static Task ReadFile(AggregateData aggregate, TextReader file, string name)
        {
            List<TaskSummary> tasks = new();

            await FastForwardUntil(file, name, "Project Evaluation Performance Summary:");

            string? line = await ReadLine(file, name);

            List<EvaluationSummary> evaluationSummaries = new();

            while (EvaluationSummary.TryParse(line, out var summary))
            {
                evaluationSummaries.Add(summary);

                line = await ReadLine(file, name);
            }

            if (evaluationSummaries.Any())
            {
                // Add synthetic "task" for Evaluation time
                tasks.Add(new TaskSummary(
                    "Evaluation",
                    evaluationSummaries.Sum(s => s.DurationMS)));
            }

            await FastForwardUntil(file, name, "Task Performance Summary:");

            line = await ReadLine(file, name);

            while (TaskSummary.TryParse(line, out var summary))
            {
                tasks.Add(summary);

                line = await file.ReadLineAsync();

                if (line is null)
                {
                    break;
                }
            }

            aggregate.AddBuild(name, tasks);
        }

        private static async Task FastForwardUntil(TextReader file, string name, string exampleLine)
        {
            string? line = string.Empty;

            while (line != exampleLine)
            {
                // Fast-forward through most of the file
                line = await file.ReadLineAsync();

                if (line is null)
                {
                    throw new FileFormatException($"File {name} didn't have a line that matched \"{exampleLine}\".");
                }
            }
        }

        private static async Task<string> ReadLine(TextReader file, string name)
        {
            string? line = await file.ReadLineAsync();
            if (line is null)
            {
                throw new FileFormatException($"File {name} ended prematurely.");
            }

            return line;
        }
    }
}
