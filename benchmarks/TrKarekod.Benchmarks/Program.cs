using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using TrKarekod.Benchmarks;

// dotnet run -c Release --project benchmarks/TrKarekod.Benchmarks -- --filter '*'
// Results land in BenchmarkDotNet.Artifacts/results; docs/benchmarks.md records the numbers and the environment.
Sanity.Check();
var config = DefaultConfig.Instance
    .AddJob(Job.Default.WithWarmupCount(5).WithIterationCount(20))
    .AddColumn(StatisticColumn.Median)
    .WithSummaryStyle(BenchmarkDotNet.Reports.SummaryStyle.Default.WithMaxParameterColumnWidth(60));
BenchmarkSwitcher.FromAssembly(typeof(ParseBenchmarks).Assembly).Run(args, config);
