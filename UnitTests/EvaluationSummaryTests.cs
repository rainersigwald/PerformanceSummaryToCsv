using PerformanceSummaryToCsv;

using Shouldly;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace UnitTests
{
    public class EvaluationSummaryTests
    {
        [Theory]
        [InlineData(@"      438 ms  S:\roslyn\src\NuGet\Microsoft.CodeAnalysis.Package.csproj   3 calls", @"S:\roslyn\src\NuGet\Microsoft.CodeAnalysis.Package.csproj", 438)]
        [InlineData(@"    29111 ms  S:\roslyn\src\Workspaces\Core\Portable\Microsoft.CodeAnalysis.Workspaces.csproj   6 calls", @"S:\roslyn\src\Workspaces\Core\Portable\Microsoft.CodeAnalysis.Workspaces.csproj", 29_111)]
        public void SuccessfulParses(string line, string path, uint duration)
        {
            EvaluationSummary.TryParse(line, out EvaluationSummary? summary).ShouldBeTrue();

            summary.ShouldNotBeNull();

            summary.ProjectPath.ShouldBe(path);
            summary.DurationMS.ShouldBe(duration);
        }

        [Theory]
        [InlineData("       a ms  WriteLinesToFile                         340 calls")]
        [InlineData("       92 ms  WriteLinesToFile                         // calls")]
        [InlineData("")]
        [InlineData("       92 *  WriteLinesToFile                         340 calls")]
        [InlineData("       92 *  WriteLinesToFile                         340 !!!")]
        public void FailedParses(string line)
        {
            TaskSummary.TryParse(line, out _).ShouldBeFalse();
        }
    }
}
