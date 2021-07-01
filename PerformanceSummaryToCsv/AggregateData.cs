using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Plotly.NET;

using static Plotly.NET.ChartExtensions;

namespace PerformanceSummaryToCsv
{
    public class AggregateData
    {
        SortedSet<string> allKnownTasks = new();

        public SortedList<string, Dictionary<string, TaskSummary>> BuildSummaries = new();

        object locker = new object();

        public void AddBuild(string sourceName, IEnumerable<TaskSummary> tasks)
        {
            Dictionary<string, TaskSummary> taskDict = new();

            lock (locker)
            {
                foreach (var task in tasks)
                {
                    allKnownTasks.Add(task.Name);
                    taskDict.Add(task.Name, task);
                }
                BuildSummaries.Add(sourceName, taskDict);
            }
        }

        public async Task WriteCsv(string path)
        {
            using var output = File.CreateText(path);

            // Header: Name, [disambiguator, disambiguator]
            await output.WriteAsync("Name");
            foreach (var (name, _) in BuildSummaries)
            {
                await output.WriteAsync(',');
                await output.WriteAsync(name);
            }
            await output.WriteLineAsync();

            foreach (var taskName in allKnownTasks)
            {
                await output.WriteAsync(taskName);
                foreach (var (_, tasks) in BuildSummaries)
                {
                    await output.WriteAsync(',');
                    await output.WriteAsync(
                        tasks.TryGetValue(taskName, out var task)
                          ? task.DurationMS.ToString()
                          : "0");
                }
                await output.WriteLineAsync();
            }
        }

        /// <summary>
        /// Tasks to explicitly sort so the expected-to-be-hot stuff is in a deterministic order.
        /// </summary>
        static readonly string[] KnownExpensiveTasks = new[] { "Csc", "Vbc", "Copy", "ResolveAssemblyReference", "ResolvePackageAssets" };

        /// <summary>
        /// Tasks that call into the engine to build other projects and thus have weird elapsed-time characteristics themselves.
        /// </summary>
        static readonly string[] KnownYieldingTasks = new[] { "CallTarget", "MSBuild", "GenerateTemporaryTargetAssembly" };

        public void ShowChart()
        {
            IList<string> keys = BuildSummaries.Keys;

            List<GenericChart.GenericChart> charts = new ();

            var tasks = KnownExpensiveTasks.Concat(allKnownTasks.Except(KnownExpensiveTasks.Concat(KnownYieldingTasks)).OrderBy(s => s));

            foreach (var taskName in tasks)
            {
                double[] times = new double[BuildSummaries.Count];
                for (int i = 0; i < BuildSummaries.Count; i++)
                {
                    var buildTasks = BuildSummaries.Values[i];
                    times[i] = buildTasks.TryGetValue(taskName, out var task)
                                 ? task.DurationMS / 1_000.0
                                 : 0;
                }

                charts.Add(Chart.StackedColumn<string, double, string>(keys, times, Name: taskName));
            }

            Combine(charts)
                .WithY_AxisStyle(title: "Time (s)", Showgrid: false, Showline: true)
                .WithLegend(false)
                .WithSize(1024,768)
                .Show();

        }
    }
}
