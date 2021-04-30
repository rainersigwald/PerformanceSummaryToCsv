using PerformanceSummaryToCsv;
using Shouldly;
using Xunit;

namespace UnitTests
{
    public class TaskSummaryTests
    {
        [Theory]
        [InlineData("       92 ms  WriteLinesToFile                         340 calls", "WriteLinesToFile", 92, 340)]
        public void SuccessfulParses(string line, string name, uint duration, uint invocations)
        {
            TaskSummary.TryParse(line, out TaskSummary summary).ShouldBeTrue();

            summary.Name.ShouldBe(name);
            summary.DurationMS.ShouldBe(duration);
            summary.Invocations.ShouldBe(invocations);
        }
    }
}
