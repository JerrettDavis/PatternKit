using PatternKit.Cloud.LeaderElection;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.LeaderElection;

[Feature("Leader Election")]
public sealed class LeaderElectionTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Leader election acquires renews and releases leases")]
    [Fact]
    public Task Leader_Election_Acquires_Renews_And_Releases_Leases()
        => Given("a leader election with a candidate", () =>
        {
            var now = DateTimeOffset.Parse("2026-05-22T00:00:00Z");
            var log = new List<string>();
            var election = LeaderElection<CandidateState>.Create("orders-leader")
                .LeaseDuration(TimeSpan.FromSeconds(5))
                .Clock(() => now)
                .Build();
            var candidate = Candidate("node-a", log);
            return new { Election = election, Candidate = candidate, Log = log };
        })
        .When("the candidate acquires renews and releases leadership", ctx => new
        {
            Acquired = ctx.Election.TryAcquire(ctx.Candidate),
            Renewed = ctx.Election.Renew(ctx.Candidate),
            Released = ctx.Election.Release(ctx.Candidate),
            ctx.Log,
            ctx.Election
        })
        .Then("each transition succeeds and callbacks run", result =>
        {
            ScenarioExpect.True(result.Acquired.Acquired);
            ScenarioExpect.Equal(1L, result.Acquired.Lease!.Term);
            ScenarioExpect.True(result.Renewed.Renewed);
            ScenarioExpect.True(result.Released.Released);
            ScenarioExpect.Null(result.Election.CurrentLease);
            ScenarioExpect.Equal(["acquired:1", "renewed:1", "released"], result.Log);
        })
        .AssertPassed();

    [Scenario("Leader election handles contention and expiry")]
    [Fact]
    public Task Leader_Election_Handles_Contention_And_Expiry()
        => Given("a leader election with two candidates", () =>
        {
            var now = DateTimeOffset.Parse("2026-05-22T00:00:00Z");
            var election = LeaderElection<CandidateState>.Create("orders-leader")
                .LeaseDuration(TimeSpan.FromSeconds(1))
                .Clock(() => now)
                .Build();
            return new
            {
                Election = election,
                Advance = new Action<TimeSpan>(delta => now = now.Add(delta)),
                Leader = Candidate("node-a"),
                Follower = Candidate("node-b")
            };
        })
        .When("a second candidate contends before and after expiry", ctx =>
        {
            var first = ctx.Election.TryAcquire(ctx.Leader);
            var blocked = ctx.Election.TryAcquire(ctx.Follower);
            ctx.Advance(TimeSpan.FromSeconds(2));
            var afterExpiry = ctx.Election.TryAcquire(ctx.Follower);
            return new { first, blocked, afterExpiry, ctx.Election };
        })
        .Then("contention fails until the lease expires", result =>
        {
            ScenarioExpect.True(result.first.Acquired);
            ScenarioExpect.True(result.blocked.Failed);
            ScenarioExpect.Contains("node-a", result.blocked.Exception!.Message);
            ScenarioExpect.True(result.afterExpiry.Acquired);
            ScenarioExpect.Equal("node-b", result.Election.CurrentLease!.CandidateId);
        })
        .AssertPassed();

    [Scenario("Leader election validates configuration and failures")]
    [Fact]
    public Task Leader_Election_Validates_Configuration_And_Failures()
        => Given("invalid leader election inputs", () => true)
        .Then("invalid election configuration is rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => LeaderElection<CandidateState>.Create("").Build());
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => LeaderElection<CandidateState>.Create().LeaseDuration(TimeSpan.Zero).Build());
            ScenarioExpect.Throws<ArgumentNullException>(() => LeaderElection<CandidateState>.Create().Clock(null!));
        })
        .And("invalid candidate configuration is rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => LeaderElectionCandidate.Create("", new CandidateState([])));
            ScenarioExpect.Throws<ArgumentNullException>(() => LeaderElectionCandidate.Create("node-a", (CandidateState)null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => LeaderElectionCandidate.Create("node-a", new CandidateState([])).OnAcquired(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => LeaderElectionCandidate.Create("node-a", new CandidateState([])).OnRenewed(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => LeaderElectionCandidate.Create("node-a", new CandidateState([])).OnReleased(null!));
        })
        .And("renew and release fail when candidate is not leader", _ =>
        {
            var election = LeaderElection<CandidateState>.Create("orders-leader").Build();
            var leader = Candidate("node-a");
            var follower = Candidate("node-b");
            ScenarioExpect.True(election.Renew(leader).Failed);
            ScenarioExpect.True(election.Release(leader).Failed);
            ScenarioExpect.True(election.TryAcquire(leader).Acquired);
            ScenarioExpect.True(election.Renew(follower).Failed);
            ScenarioExpect.True(election.Release(follower).Failed);
        })
        .And("null candidates and blank ids are guarded", _ =>
        {
            var election = LeaderElection<CandidateState>.Create().Build();
            ScenarioExpect.Throws<ArgumentNullException>(() => election.TryAcquire(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => election.Renew(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => election.Release(null!));
            ScenarioExpect.Throws<ArgumentException>(() => election.IsLeader(""));
        })
        .And("result factories guard required values", _ =>
        {
            ScenarioExpect.Throws<ArgumentNullException>(() => LeaderElectionResult.Acquisition("orders", "node", null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => LeaderElectionResult.Renewal("orders", "node", null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => LeaderElectionResult.Failure("orders", "node", null!));
        })
        .AssertPassed();

    private static LeaderElectionCandidate<CandidateState> Candidate(string id, List<string>? log = null)
        => LeaderElectionCandidate.Create(id, new CandidateState(log ?? []))
            .OnAcquired(static (lease, state) => state.Log.Add($"acquired:{lease.Term}"))
            .OnRenewed(static (lease, state) => state.Log.Add($"renewed:{lease.Term}"))
            .OnReleased(static state => state.Log.Add("released"))
            .Build();

    private sealed record CandidateState(List<string> Log);
}
