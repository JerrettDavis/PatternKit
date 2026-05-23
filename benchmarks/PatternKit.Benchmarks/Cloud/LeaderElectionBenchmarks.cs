using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.LeaderElection;
using PatternKit.Generators.LeaderElection;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "LeaderElection")]
public class LeaderElectionBenchmarks
{
    private static readonly DateTimeOffset Now = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(5);
    private readonly LeaderElectionBenchmarkContext _context = new("node-a");

    [Benchmark(Baseline = true, Description = "Fluent: create election and candidate")]
    [BenchmarkCategory("Fluent", "Construction")]
    public (LeaderElection<LeaderElectionBenchmarkContext> Election, LeaderElectionCandidate<LeaderElectionBenchmarkContext> Candidate) Fluent_CreateElectionAndCandidate()
    {
        var election = LeaderElection<LeaderElectionBenchmarkContext>
            .Create("benchmark-leader")
            .LeaseDuration(LeaseDuration)
            .Clock(static () => Now)
            .Build();
        var candidate = LeaderElectionCandidate
            .Create(_context.NodeId, _context)
            .OnAcquired(static (lease, context) => context.AcquiredTerm = lease.Term)
            .OnRenewed(static (lease, context) => context.RenewedTerm = lease.Term)
            .OnReleased(static context => context.ReleasedCount++)
            .Build();

        return (election, candidate);
    }

    [Benchmark(Description = "Generated: create election and candidate")]
    [BenchmarkCategory("Generated", "Construction")]
    public (LeaderElection<LeaderElectionBenchmarkContext> Election, LeaderElectionCandidate<LeaderElectionBenchmarkContext> Candidate) Generated_CreateElectionAndCandidate()
        => (GeneratedLeaderElectionBenchmark.CreateElection(), GeneratedLeaderElectionBenchmark.Create(_context));

    [Benchmark(Description = "Fluent: acquire, renew, release")]
    [BenchmarkCategory("Fluent", "Execution")]
    public LeaderElectionResult Fluent_AcquireRenewRelease()
    {
        var election = LeaderElection<LeaderElectionBenchmarkContext>
            .Create("benchmark-leader")
            .LeaseDuration(LeaseDuration)
            .Clock(static () => Now)
            .Build();
        var candidate = Fluent_CreateElectionAndCandidate().Candidate;

        _ = election.TryAcquire(candidate);
        _ = election.Renew(candidate);
        return election.Release(candidate);
    }

    [Benchmark(Description = "Generated: acquire, renew, release")]
    [BenchmarkCategory("Generated", "Execution")]
    public LeaderElectionResult Generated_AcquireRenewRelease()
    {
        var election = GeneratedLeaderElectionBenchmark.CreateElection();
        var candidate = GeneratedLeaderElectionBenchmark.Create(_context);

        _ = election.TryAcquire(candidate);
        _ = election.Renew(candidate);
        return election.Release(candidate);
    }
}

public sealed record LeaderElectionBenchmarkContext(string NodeId)
{
    public long AcquiredTerm { get; set; }

    public long RenewedTerm { get; set; }

    public int ReleasedCount { get; set; }
}

[GenerateLeaderElection(
    typeof(LeaderElectionBenchmarkContext),
    FactoryMethodName = "Create",
    ElectionName = "benchmark-leader",
    LeaseDurationMilliseconds = 5000)]
public static partial class GeneratedLeaderElectionBenchmark
{
    [LeaderCandidateId]
    private static string CandidateId(LeaderElectionBenchmarkContext context) => context.NodeId;

    [LeaderAcquired]
    private static void Acquired(LeaderLease lease, LeaderElectionBenchmarkContext context) => context.AcquiredTerm = lease.Term;

    [LeaderRenewed]
    private static void Renewed(LeaderLease lease, LeaderElectionBenchmarkContext context) => context.RenewedTerm = lease.Term;

    [LeaderReleased]
    private static void Released(LeaderElectionBenchmarkContext context) => context.ReleasedCount++;
}
