using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.Chain;
using PatternKit.Generators.Chain;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "GoF", "ChainOfResponsibility")]
public class ChainOfResponsibilityBenchmarks
{
    private static readonly ApprovalRequest Request = new(2500m, true);

    [Benchmark(Baseline = true, Description = "Fluent: create responsibility chain")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ResultChain<ApprovalRequest, ApprovalDecision> Fluent_CreateChain()
        => ResultChain<ApprovalRequest, ApprovalDecision>.Create()
            .When(static (in ApprovalRequest request) => request.RequiresReview)
            .Then(static request => new ApprovalDecision("review", request.Amount))
            .When(static (in ApprovalRequest request) => request.Amount <= 5000m)
            .Then(static request => new ApprovalDecision("approved", request.Amount))
            .Finally(static (in ApprovalRequest request, out ApprovalDecision decision, ResultChain<ApprovalRequest, ApprovalDecision>.Next _) =>
            {
                decision = new ApprovalDecision("escalated", request.Amount);
                return true;
            })
            .Build();

    [Benchmark(Description = "Generated: create responsibility chain")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedApprovalChain Generated_CreateChain()
        => new();

    [Benchmark(Description = "Fluent: handle approval request")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ApprovalDecision Fluent_HandleApproval()
    {
        var chain = Fluent_CreateChain();
        chain.Execute(Request, out var decision);
        return decision;
    }

    [Benchmark(Description = "Generated: handle approval request")]
    [BenchmarkCategory("Generated", "Execution")]
    public ApprovalDecision Generated_HandleApproval()
        => new GeneratedApprovalChain().Handle(Request);
}

public readonly record struct ApprovalRequest(decimal Amount, bool RequiresReview);

public readonly record struct ApprovalDecision(string Route, decimal Amount);

[Chain]
public partial class GeneratedApprovalChain
{
    [ChainHandler(Order = 0)]
    private bool TryReview(in ApprovalRequest request, out ApprovalDecision decision)
    {
        decision = new ApprovalDecision("review", request.Amount);
        return request.RequiresReview;
    }

    [ChainHandler(Order = 1)]
    private bool TryApprove(in ApprovalRequest request, out ApprovalDecision decision)
    {
        decision = new ApprovalDecision("approved", request.Amount);
        return request.Amount <= 5000m;
    }

    [ChainDefault]
    private ApprovalDecision Escalate(in ApprovalRequest request)
        => new("escalated", request.Amount);
}
