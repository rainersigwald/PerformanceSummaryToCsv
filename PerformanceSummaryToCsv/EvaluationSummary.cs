using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceSummaryToCsv
{
    public record EvaluationSummary(string ProjectPath, double DurationMS)
    {
        public static bool TryParse(string line, [NotNullWhen(true)] out EvaluationSummary? summary)
        {
            summary = default;

            var elements = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // 391 ms  S:\roslyn\src\Tools\BuildValidator\BuildValidator.csproj   3 calls
            // 0   1   2                                                          3 4
            if (elements?.Length != 5 || elements[1] != "ms" || elements[4] != "calls")
            {
                return false;
            }

            if (!double.TryParse(elements[0], out double durationMS))
            {
                return false;
            }

            if (!int.TryParse(elements[3], out _))
            {
                return false;
            }

            summary = new(elements[2], durationMS);

            return true;
        }

    }
}
