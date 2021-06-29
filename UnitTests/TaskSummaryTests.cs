using PerformanceSummaryToCsv;
using Shouldly;
using Xunit;

namespace UnitTests
{
    public class TaskSummaryTests
    {
        [Theory]
        [InlineData("       92 ms  WriteLinesToFile                         340 calls", "WriteLinesToFile", 92)]
        [InlineData("        3 ms  CheckIfPackageReferenceShouldBeFrameworkReference  60 calls", "CheckIfPackageReferenceShouldBeFrameworkReference", 3)]
        public void SuccessfulParses(string line, string name, uint duration)
        {
            TaskSummary.TryParse(line, out TaskSummary? summary).ShouldBeTrue();

            summary.ShouldNotBeNull();

            summary.Name.ShouldBe(name);
            summary.DurationMS.ShouldBe(duration);
        }

        [Theory]
        [InlineData("       a ms  WriteLinesToFile                         340 calls")]
        [InlineData("       92 ms  WriteLinesToFile                         // calls")]
        [InlineData("")]
        [InlineData("       92 *  WriteLinesToFile                         340 calls")]
        [InlineData("       92 *  WriteLinesToFile                         340 !!!")]
        [InlineData(@"      672 ms  S:\roslyn\src\Compilers\CSharp\csc\csc.csproj   3 calls")]
        public void FailedParses(string line)
        {
            TaskSummary.TryParse(line, out _).ShouldBeFalse();
        }
    }
}
