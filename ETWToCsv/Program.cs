using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            ZippedETLReader zipReader = new ZippedETLReader(@"C:\Users\namytelk\Desktop\PerfViewData.etl.zip", Console.Out);
            zipReader.UnpackArchive();

            var traceLog = TraceLog.OpenOrConvert(@"C:\Users\namytelk\Desktop\PerfViewData.etl", new TraceLogOptions() { ConversionLog = Console.Out });
            var evts = traceLog.Events.Filter(e => e.ProviderName.Equals("Microsoft-Build"));
            Dictionary<string, Dictionary<int, double>> startTimes = new Dictionary<string, Dictionary<int, double>>();
            List<Tuple<string, string, double>> events = new();
            foreach (var evt in evts.Where(e => e.EventName.Contains("ExecuteTask") || e.EventName.Contains("Target")))
            {
                string key = evt.EventName.Contains("Target") ? "Target," + evt.PayloadValue(evt.PayloadIndex("targetName")) : "Task," + evt.PayloadValue(evt.PayloadIndex("taskName"));
                if (evt.EventName.Contains("Start"))
                {
                    if (startTimes.TryGetValue(key, out Dictionary<int, double> latest))
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
            foreach (var evt in events)
            {
                Console.WriteLine(evt.Item1 + "," + evt.Item2 + "," + evt.Item3);
            }
        }
    }
}
