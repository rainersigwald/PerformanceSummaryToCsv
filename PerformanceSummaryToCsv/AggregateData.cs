using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace PerformanceSummaryToCsv
{
    public class AggregateData
    {
        SortedSet<string> allKnownTasks = new();

        List<(string Name, Dictionary<string, TaskSummary> tasks)> buildSummaries = new();

        public void AddBuild(string sourceName, IEnumerable<TaskSummary> tasks)
        {
            Dictionary<string, TaskSummary> taskDict = new();

            foreach (var task in tasks)
            {
                allKnownTasks.Add(task.Name);
                taskDict.Add(task.Name, task);
            }
            buildSummaries.Add((sourceName, taskDict));
        }

        public async Task WriteCsv(string path)
        {
            using var output = File.CreateText(path);

            // Header: Name, [disambiguator, disambiguator]
            await output.WriteAsync("Name");
            foreach (var build in buildSummaries)
            {
                await output.WriteAsync(',');
                await output.WriteAsync(build.Name);
            }
            await output.WriteLineAsync();

            foreach (var taskName in allKnownTasks)
            {
                await output.WriteAsync(taskName);
                foreach (var build in buildSummaries)
                {
                    await output.WriteAsync(',');
                    await output.WriteAsync(
                        build.tasks.TryGetValue(taskName, out var task)
                          ? task.DurationMS.ToString()
                          : "0");
                }
                await output.WriteLineAsync();
            }
        }
    }
}
