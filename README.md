# PerformanceSummaryToCsv

This is a tool to compare builds using MSBuild. You can pass it any combination of

* Text log files built with the logger option `PerformanceSummary`
* Binary logs (`.binlog`s)
* ETW traces (`.etl.zip`) that include [MSBuild's event source](https://github.com/dotnet/msbuild/blob/main/documentation/specs/event-source.md)

And get as output a CSV file with a task-level "where did the time go" breakdown, and optionally an HTML chart comparing the builds.

```text
Usage:
  PerformanceSummaryToCsv [options]

Options:
  --inputs <inputs>  Input text files containing an MSBuild PerformanceSummary section. Separate multiple inputs with a space.
  --output <output>  Path of the final csv file. [default: MSBuild_performance.csv]
  --show             Open a browser window with a comparison chart. [default: False]
  --version          Show version information
  -?, -h, --help     Show help and usage information
```

As of now there isn't an easy way to install the tool. After cloning and building this repo you should have an executable. Building requires .NET SDK 5.0.100 or higher.

```bash
git clone https://github.com/rainersigwald/PerformanceSummaryToCsv.git
cd PerformanceSummaryToCsv
dotnet build
```

Then run `PerformanceSummaryToCsv\bin\Debug\net5.0\PerformanceSummaryToCsv.exe` (or the equivalent non-Windows binary).
