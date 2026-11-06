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
    [Theory]
    [InlineData("public static class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; }", "PKLE001")]
    [InlineData("public static partial class LeaderHost;", "PKLE002")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string One(string value) => value; [LeaderCandidateId] private static string Two(string value) => value; }", "PKLE002")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; [LeaderAcquired] private static void One(LeaderLease lease, string value) { } [LeaderAcquired] private static void Two(LeaderLease lease, string value) { } }", "PKLE002")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; [LeaderRenewed] private static void One(LeaderLease lease, string value) { } [LeaderRenewed] private static void Two(LeaderLease lease, string value) { } }", "PKLE002")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; [LeaderReleased] private static void One(string value) { } [LeaderReleased] private static void Two(string value) { } }", "PKLE002")]
    [InlineData("public partial class LeaderHost { [LeaderCandidateId] private string CandidateId(string value) => value; }", "PKLE003")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static int CandidateId(string value) => 1; }", "PKLE003")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId() => string.Empty; }", "PKLE003")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(int value) => value.ToString(); }", "PKLE003")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; [LeaderAcquired] private static int Acquired(LeaderLease lease, string context) => 1; }", "PKLE003")]
    [InlineData("public partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; [LeaderAcquired] private void Acquired(LeaderLease lease, string context) { } }", "PKLE003")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; [LeaderAcquired] private static void Acquired(string lease, string context) { } }", "PKLE003")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; [LeaderAcquired] private static void Acquired(LeaderLease lease, int context) { } }", "PKLE003")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; [LeaderRenewed] private static void Renewed(LeaderLease lease) { } }", "PKLE003")]
    [InlineData("public partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; [LeaderReleased] private void Released(string context) { } }", "PKLE003")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; [LeaderReleased] private static void Released(int context) { } }", "PKLE003")]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; }", "PKLE004", 0)]
    [InlineData("public static partial class LeaderHost { [LeaderCandidateId] private static string CandidateId(string value) => value; }", "PKLE004", -1)]
    public Task Reports_Diagnostics_For_Invalid_Leader_Election_Declarations(string declaration, string diagnosticId, int leaseDurationMilliseconds = 30000)
        => Given("an invalid leader election declaration", () => Compile($$"""
            using PatternKit.Cloud.LeaderElection;
            using PatternKit.Generators.LeaderElection;
            [GenerateLeaderElection(typeof(string), LeaseDurationMilliseconds = {{leaseDurationMilliseconds}})]
            {{declaration}}
            """))
        .Then("diagnostics identify invalid declarations", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates leader election defaults and host shapes")]
    [Fact]
    public Task Generates_Leader_Election_Defaults_And_Host_Shapes()
        => Given("leader election declarations with default names and different host shapes", () => Compile("""
            using PatternKit.Cloud.LeaderElection;
            using PatternKit.Generators.LeaderElection;
            namespace Demo;
            public sealed record WorkerContext(string NodeId);

            [GenerateLeaderElection(typeof(WorkerContext))]
            internal abstract partial class AbstractLeader
            {
                [LeaderCandidateId]
                private static string CandidateId(WorkerContext context) => context.NodeId;
            }

            [GenerateLeaderElection(typeof(WorkerContext), ElectionName = "tenant\\\"leader")]
            public sealed partial class SealedLeader
            {
                [LeaderCandidateId]
                private static string CandidateId(WorkerContext context) => context.NodeId;
            }

            [GenerateLeaderElection(typeof(WorkerContext))]
            internal partial struct StructLeader
            {
                [LeaderCandidateId]
                private static string CandidateId(WorkerContext context) => context.NodeId;
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractLeader", combined);
            ScenarioExpect.Contains("public sealed partial class SealedLeader", combined);
            ScenarioExpect.Contains("internal partial struct StructLeader", combined);
            ScenarioExpect.Contains("CreateElection()", combined);
            ScenarioExpect.Contains("Create(\"leader-election\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"leader\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested leader election host wrappers")]
    [Fact]
    public Task Generates_Nested_Leader_Election_Host_Wrappers()
        => Given("nested leader election declarations", () => Compile("""
            using PatternKit.Cloud.LeaderElection;
            using PatternKit.Generators.LeaderElection;
            namespace Demo;
            public sealed record WorkerContext(string NodeId);

            public partial class LeaderContainer
            {
                private partial class PrivateHost
                {
                    [GenerateLeaderElection(typeof(WorkerContext))]
                    protected partial class ProtectedLeader
                    {
                        [LeaderCandidateId]
                        private static string CandidateId(WorkerContext context) => context.NodeId;
                    }

                    [GenerateLeaderElection(typeof(WorkerContext))]
                    private protected partial class PrivateProtectedLeader
                    {
                        [LeaderCandidateId]
                        private static string CandidateId(WorkerContext context) => context.NodeId;
                    }

                    [GenerateLeaderElection(typeof(WorkerContext))]
                    protected internal partial class ProtectedInternalLeader
                    {
                        [LeaderCandidateId]
                        private static string CandidateId(WorkerContext context) => context.NodeId;
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class LeaderContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedLeader", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedLeader", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalLeader", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed leader election type arguments")]
    [Fact]
    public Task Skips_Malformed_Leader_Election_Type_Arguments()
        => Given("a leader election declaration with a null type argument", () => Compile("""
            using PatternKit.Generators.LeaderElection;
            [GenerateLeaderElection(null!)]
            public static partial class LeaderHost
            {
                [LeaderCandidateId]
                private static string CandidateId(string value) => value;
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
