using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceSummaryToCsv
{
    public record TaskSummary(string Name, uint DurationMS, uint Invocations)
    {
        public static bool TryParse(string line, [NotNullWhen(true)] out TaskSummary? summary)
        {
            summary = default;

            var elements = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // 3 ms  GetRestorePackageReferencesTask           60 calls
            // 1 2   3                                         4  5
            if (elements?.Length != 5 || elements[1] != "ms" || elements[4] != "calls")
            {
                return false;
            }

            if (!uint.TryParse(elements[0], out uint durationMS))
            {
                return false;
            }

            if (!uint.TryParse(elements[3], out uint invocations))
            {
                return false;
            }

            summary = new(elements[2], durationMS, invocations);

            return true;
        }
    }
}
