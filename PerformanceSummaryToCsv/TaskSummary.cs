using System;
using System.Diagnostics.CodeAnalysis;

namespace PerformanceSummaryToCsv
{
    public record TaskSummary(string Name, double DurationMS)
    {
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

            summary = new(elements[2], durationMS);

            return true;
        }
    }
}
