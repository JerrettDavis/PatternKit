using System.Text.Json;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

namespace PatternKit.Benchmarks;

public sealed class JsonSummaryExporter : IExporter
{
    public static readonly JsonSummaryExporter Default = new();

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private JsonSummaryExporter()
    {
    }

    public string Name => "json-summary";

    public void ExportToLog(Summary summary, ILogger logger)
    {
    }

    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        Directory.CreateDirectory(summary.ResultsDirectoryPath);
        var path = Path.Combine(summary.ResultsDirectoryPath, $"{Sanitize(summary.Title)}-summary.json");
        var payload = new
        {
            summary.Title,
            summary.AllRuntimes,
            totalTime = summary.TotalTime,
            host = summary.HostEnvironmentInfo.ToString(),
            benchmarks = summary.Reports.Select(static report => new
            {
                name = report.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo,
                displayName = report.BenchmarkCase.DisplayInfo,
                job = report.BenchmarkCase.Job.DisplayInfo,
                success = report.Success,
                statistics = report.ResultStatistics is null
                    ? null
                    : new
                    {
                        report.ResultStatistics.N,
                        report.ResultStatistics.Mean,
                        report.ResultStatistics.Median,
                        report.ResultStatistics.Min,
                        report.ResultStatistics.Max,
                        report.ResultStatistics.StandardDeviation,
                        report.ResultStatistics.StandardError
                    },
                gc = new
                {
                    report.GcStats.TotalOperations,
                    report.GcStats.Gen0Collections,
                    report.GcStats.Gen1Collections,
                    report.GcStats.Gen2Collections
                },
                metrics = report.Metrics.ToDictionary(static metric => metric.Key, static metric => metric.Value.ToString())
            })
        };

        File.WriteAllText(path, JsonSerializer.Serialize(payload, Options));
        return [path];
    }

    private static string Sanitize(string value)
        => string.Concat(value.Select(static character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
}
