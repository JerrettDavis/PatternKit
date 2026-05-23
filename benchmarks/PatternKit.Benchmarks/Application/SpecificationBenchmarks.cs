using BenchmarkDotNet.Attributes;
using PatternKit.Application.Specification;
using PatternKit.Examples.SpecificationDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "Specification")]
public class SpecificationBenchmarks
{
    private static readonly LoanApprovalSpecificationDemo.LoanApplication PrimeApplication =
        LoanApprovalSpecificationDemo.CreatePrimeApplication();

    private readonly SpecificationRegistry<LoanApprovalSpecificationDemo.LoanApplication> _fluent =
        LoanApprovalSpecificationDemo.CreateFluentRegistry();

    private readonly SpecificationRegistry<LoanApprovalSpecificationDemo.LoanApplication> _generated =
        LoanApprovalSpecificationDemo.CreateGeneratedRegistry();

    [Benchmark(Baseline = true, Description = "Fluent: create specification registry")]
    [BenchmarkCategory("Fluent", "Construction")]
    public SpecificationRegistry<LoanApprovalSpecificationDemo.LoanApplication> Fluent_CreateRegistry()
        => LoanApprovalSpecificationDemo.CreateFluentRegistry();

    [Benchmark(Description = "Generated: create specification registry")]
    [BenchmarkCategory("Generated", "Construction")]
    public SpecificationRegistry<LoanApprovalSpecificationDemo.LoanApplication> Generated_CreateRegistry()
        => LoanApprovalSpecificationDemo.CreateGeneratedRegistry();

    [Benchmark(Description = "Fluent: evaluate loan application")]
    [BenchmarkCategory("Fluent", "Execution")]
    public LoanApprovalSpecificationDemo.LoanDecision Fluent_EvaluateLoanApplication()
        => LoanApprovalSpecificationDemo.Evaluate(PrimeApplication, _fluent);

    [Benchmark(Description = "Generated: evaluate loan application")]
    [BenchmarkCategory("Generated", "Execution")]
    public LoanApprovalSpecificationDemo.LoanDecision Generated_EvaluateLoanApplication()
        => LoanApprovalSpecificationDemo.Evaluate(PrimeApplication, _generated);
}
