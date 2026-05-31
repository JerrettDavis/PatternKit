using BenchmarkDotNet.Attributes;
using PatternKit.Creational.ObjectPool;
using PatternKit.Examples.ObjectPoolDemo;

namespace PatternKit.Benchmarks.Creational;

[BenchmarkCategory("Creational", "ObjectPool")]
public class ObjectPoolBenchmarks
{
    private static readonly FormulaEvaluationRequest Request = new(
        "D4",
        "subtotal + tax + 5",
        new Dictionary<string, decimal>
        {
            ["subtotal"] = 100m,
            ["tax"] = 8.25m
        });

    [Benchmark(Baseline = true, Description = "Fluent: create object pool")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ObjectPool<FormulaEvaluationBuffer> Fluent_CreateObjectPool()
        => SpreadsheetFormulaBufferPools.CreateFluent();

    [Benchmark(Description = "Generated: create object pool")]
    [BenchmarkCategory("Generated", "Construction")]
    public ObjectPool<FormulaEvaluationBuffer> Generated_CreateObjectPool()
        => SpreadsheetFormulaBufferPools.CreateGenerated();

    [Benchmark(Description = "Fluent: evaluate spreadsheet formula")]
    [BenchmarkCategory("Fluent", "Execution")]
    public FormulaEvaluationResult Fluent_EvaluateSpreadsheetFormula()
        => SpreadsheetFormulaObjectPoolDemoRunner.RunFluent(Request);

    [Benchmark(Description = "Generated: evaluate spreadsheet formula")]
    [BenchmarkCategory("Generated", "Execution")]
    public FormulaEvaluationResult Generated_EvaluateSpreadsheetFormula()
        => SpreadsheetFormulaObjectPoolDemoRunner.RunGenerated(Request);
}
