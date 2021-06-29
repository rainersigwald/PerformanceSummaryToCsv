using System;
using System.Diagnostics.CodeAnalysis;

namespace PerformanceSummaryToCsv
{
    public record TaskSummary(string Name, double DurationMS)
    {
        private static char[] IllegalTaskNameCharacters = new[] { '/', '\\' };

        public static bool TryParse(string line, [NotNullWhen(true)] out TaskSummary? summary)
        {
            summary = default;

            var elements = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // 3 ms  GetRestorePackageReferencesTask           60 calls
            // 0 1   2                                         3  4
            if (elements?.Length != 5 || elements[1] != "ms" || elements[4] != "calls")
            {
                return false;
            }

            if (!double.TryParse(elements[0], out double durationMS))
            {
                return false;
            }

            if (!int.TryParse(elements[3], out int invocations))
            {
                return false;
            }

            string taskName = elements[2];

            if (taskName.IndexOfAny(IllegalTaskNameCharacters) != -1)
            {
                // Doesn't look like a task name; probably in some other 
                return false;
            }

            summary = new(taskName, durationMS);

            return true;
        }
    }
}
