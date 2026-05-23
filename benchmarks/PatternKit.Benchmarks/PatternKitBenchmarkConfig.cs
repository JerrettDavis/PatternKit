using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;

namespace PatternKit.Benchmarks;

public static class PatternKitBenchmarkConfig
{
    public static IConfig Create()
        => ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithId("net10.0"))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddColumn(CategoriesColumn.Default)
            .AddColumn(RankColumn.Arabic)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(CsvExporter.Default)
            .AddExporter(JsonSummaryExporter.Default)
            .AddLogger(ConsoleLogger.Default)
            .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest))
            .WithOptions(ConfigOptions.JoinSummary);
}
