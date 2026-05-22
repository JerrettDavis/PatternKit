using Microsoft.CodeAnalysis;
using PatternKit.Cloud.LeaderElection;
using PatternKit.Generators.LeaderElection;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Leader Election generator")]
public sealed partial class LeaderElectionGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates leader election factories")]
    [Fact]
    public Task Generates_Leader_Election_Factories()
        => Given("a leader election declaration", () => Compile("""
            using PatternKit.Cloud.LeaderElection;
            using PatternKit.Generators.LeaderElection;
            namespace Demo;
            public sealed record WorkerContext(string NodeId);
            [GenerateLeaderElection(typeof(WorkerContext), FactoryMethodName = "Build", ElectionName = "orders-leader", LeaseDurationMilliseconds = 5000)]
            public static partial class OrdersLeader
            {
                [LeaderCandidateId]
                private static string CandidateId(WorkerContext context) => context.NodeId;
                [LeaderAcquired]
                private static void Acquired(LeaderLease lease, WorkerContext context) { }
                [LeaderRenewed]
                private static void Renewed(LeaderLease lease, WorkerContext context) { }
                [LeaderReleased]
                private static void Released(WorkerContext context) { }
            }
            """))
        .Then("the generated source creates election and candidate factories", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("BuildElection()", source);
            ScenarioExpect.Contains("LeaderElection<global::Demo.WorkerContext>.Create(\"orders-leader\")", source);
            ScenarioExpect.Contains(".LeaseDuration(global::System.TimeSpan.FromMilliseconds(5000))", source);
            ScenarioExpect.Contains("Build(global::Demo.WorkerContext context)", source);
            ScenarioExpect.Contains("LeaderElectionCandidate.Create(CandidateId(context), context)", source);
            ScenarioExpect.Contains(".OnAcquired(Acquired)", source);
            ScenarioExpect.Contains(".OnRenewed(Renewed)", source);
            ScenarioExpect.Contains(".OnReleased(Released)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid leader election declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Leader_Election_Declarations()
        => Given("invalid leader election declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.LeaderElection;
                [GenerateLeaderElection(typeof(string))]
                public static class LeaderHost;
                """),
            Compile("""
                using PatternKit.Generators.LeaderElection;
                [GenerateLeaderElection(typeof(string))]
                public static partial class LeaderHost;
                """),
            Compile("""
                using PatternKit.Generators.LeaderElection;
                [GenerateLeaderElection(typeof(string))]
                public static partial class LeaderHost
                {
                    [LeaderCandidateId]
                    private static int CandidateId(string value) => 1;
                }
                """),
            Compile("""
                using PatternKit.Generators.LeaderElection;
                [GenerateLeaderElection(typeof(string), LeaseDurationMilliseconds = 0)]
                public static partial class LeaderHost
                {
                    [LeaderCandidateId]
                    private static string CandidateId(string value) => value;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKLE001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKLE002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKLE003");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKLE004");
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "LeaderElectionGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(LeaderElection<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new LeaderElectionGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
