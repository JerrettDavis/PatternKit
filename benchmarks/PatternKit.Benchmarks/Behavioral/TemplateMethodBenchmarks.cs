using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.Template;
using PatternKit.Generators.Template;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "GoF", "TemplateMethod")]
public class TemplateMethodBenchmarks
{
    private static readonly ImportBatch Batch = new("customers", 42);

    [Benchmark(Baseline = true, Description = "Fluent: create template")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Template<ImportBatch, ImportResult> Fluent_CreateTemplate()
        => Template<ImportBatch, ImportResult>.Create(static batch => new ImportResult(batch.Source, batch.Rows, true))
            .Before(static batch => _ = batch.Rows)
            .After(static (_, _) => { })
            .Build();

    [Benchmark(Description = "Generated: create template")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedImportWorkflow Generated_CreateTemplate()
        => new();

    [Benchmark(Description = "Fluent: execute template")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ImportResult Fluent_ExecuteTemplate()
        => Fluent_CreateTemplate().Execute(Batch);

    [Benchmark(Description = "Generated: execute template")]
    [BenchmarkCategory("Generated", "Execution")]
    public int Generated_ExecuteTemplate()
    {
        var context = new GeneratedImportContext(Batch.Source, Batch.Rows);
        new GeneratedImportWorkflow().Execute(context);
        return context.ProcessedRows;
    }
}

public readonly record struct ImportBatch(string Source, int Rows);

public readonly record struct ImportResult(string Source, int Rows, bool Persisted);

public sealed class GeneratedImportContext(string source, int rows)
{
    public string Source { get; } = source;

    public int Rows { get; } = rows;

    public int ProcessedRows { get; set; }
}

[Template]
public partial class GeneratedImportWorkflow
{
    [TemplateStep(0)]
    private void Validate(GeneratedImportContext context) => _ = context.Source;

    [TemplateStep(1)]
    private void Transform(GeneratedImportContext context) => context.ProcessedRows = context.Rows;

    [TemplateStep(2)]
    private void Persist(GeneratedImportContext context) => context.ProcessedRows += 0;
}
