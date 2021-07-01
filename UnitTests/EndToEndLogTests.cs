using PerformanceSummaryToCsv;

using Shouldly;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace UnitTests
{
    public class EndToEndLogTests
    {
        [Fact]
        public async void ParseRealLog()
        {
            AggregateData aggregate = new();

            await Program.ReadFile(aggregate, new StringReader(UnitTests_ExampleLogs.VS16_10_20212406_1354), "VS16_10_20212406_1354.log");

            aggregate.BuildSummaries["VS16_10_20212406_1354.log"].ShouldContainKey("Csc");
            aggregate.BuildSummaries["VS16_10_20212406_1354.log"]["Csc"].ShouldBe(new("Csc", 1_056_578d));
            aggregate.BuildSummaries["VS16_10_20212406_1354.log"]["Copy"].ShouldBe(new("Copy", 385_116d));
        }

    }
}
