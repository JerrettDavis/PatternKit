using Microsoft.CodeAnalysis;
using PatternKit.Cloud.SchedulerAgentSupervisor;
using PatternKit.Generators.SchedulerAgentSupervisor;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Scheduler Agent Supervisor generator")]
public sealed partial class SchedulerAgentSupervisorGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates scheduler agent supervisor factory")]
    [Fact]
    public Task Generates_Scheduler_Agent_Supervisor_Factory()
        => Given("a scheduler agent supervisor declaration", () => Compile("""
            using System;
            using PatternKit.Cloud.SchedulerAgentSupervisor;
            using PatternKit.Generators.SchedulerAgentSupervisor;
            namespace Demo;
            public sealed record Work(string Id);
            public sealed record Summary(string Id);
            [GenerateSchedulerAgentSupervisor(typeof(Work), typeof(Summary), FactoryMethodName = "Build", SupervisorName = "orders-scheduler", MaxAttempts = 4, RetryDelayMilliseconds = 250)]
            public static partial class OrdersScheduler
            {
                [SchedulerAgent("release-agent")]
                private static Summary Release(SchedulerAgentContext<Work> context) => new(context.Work.Id);
                [SchedulerRetryWhen]
                private static bool Retry(Exception exception, SchedulerAgentContext<Work> context) => context.Attempt < 3;
            }
            """))
        .Then("the generated source creates a configured supervisor", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("SchedulerSupervisionPolicy<global::Demo.Work>.Create()", source);
            ScenarioExpect.Contains(".MaxAttempts(4)", source);
            ScenarioExpect.Contains(".RetryDelay(global::System.TimeSpan.FromMilliseconds(250))", source);
            ScenarioExpect.Contains(".RetryWhen(Retry)", source);
            ScenarioExpect.Contains("SchedulerAgentSupervisor<global::Demo.Work, global::Demo.Summary>.Create(\"orders-scheduler\")", source);
            ScenarioExpect.Contains(".Agent(\"release-agent\", Release)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid scheduler declarations")]
    [Theory]
    [InlineData("public static class SchedulerHost;", "PKSAS001")]
    [InlineData("public static partial class SchedulerHost;", "PKSAS002")]
    [InlineData("public static partial class SchedulerHost { [SchedulerAgent(\"agent\")] private static int Run(SchedulerAgentContext<Work> context) => 1; }", "PKSAS003")]
    [InlineData("public static partial class SchedulerHost { [SchedulerAgent(\"agent\")] private Summary Run(SchedulerAgentContext<Work> context) => new(context.Work.Id); }", "PKSAS003")]
    [InlineData("public static partial class SchedulerHost { [SchedulerAgent(\"agent\")] private static Summary Run(string context) => new(context); }", "PKSAS003")]
    [InlineData("public static partial class SchedulerHost { [SchedulerAgent(\"agent\")] private static Summary Run(SchedulerAgentContext<string> context) => new(context.Work); }", "PKSAS003")]
    [InlineData("public static partial class SchedulerHost { [SchedulerAgent(\"agent\")] private static Summary Run(SchedulerAgentContext<Work> context) => new(context.Work.Id); [SchedulerRetryWhen] private static string Retry(Exception exception, SchedulerAgentContext<Work> context) => exception.Message; }", "PKSAS003")]
    [InlineData("public static partial class SchedulerHost { [SchedulerAgent(\"agent\")] private static Summary Run(SchedulerAgentContext<Work> context) => new(context.Work.Id); [SchedulerRetryWhen] private bool Retry(Exception exception, SchedulerAgentContext<Work> context) => true; }", "PKSAS003")]
    [InlineData("public static partial class SchedulerHost { [SchedulerAgent(\"agent\")] private static Summary Run(SchedulerAgentContext<Work> context) => new(context.Work.Id); [SchedulerRetryWhen] private static bool Retry(string exception, SchedulerAgentContext<Work> context) => true; }", "PKSAS003")]
    [InlineData("public static partial class SchedulerHost { [SchedulerAgent(\"agent\")] private static Summary Run(SchedulerAgentContext<Work> context) => new(context.Work.Id); [SchedulerRetryWhen] private static bool One(Exception exception, SchedulerAgentContext<Work> context) => true; [SchedulerRetryWhen] private static bool Two(Exception exception, SchedulerAgentContext<Work> context) => true; }", "PKSAS002")]
    [InlineData("public static partial class SchedulerHost { [SchedulerAgent(\"agent\")] private static Summary Run(SchedulerAgentContext<Work> context) => new(context.Work.Id); [SchedulerAgent(\"agent\")] private static Summary RunAgain(SchedulerAgentContext<Work> context) => new(context.Work.Id); }", "PKSAS005")]
    public Task Reports_Diagnostics_For_Invalid_Scheduler_Declarations(string declaration, string diagnosticId)
        => Given("an invalid scheduler agent supervisor declaration", () => Compile($$"""
            using System;
            using PatternKit.Cloud.SchedulerAgentSupervisor;
            using PatternKit.Generators.SchedulerAgentSupervisor;
            public sealed record Work(string Id);
            public sealed record Summary(string Id);
            [GenerateSchedulerAgentSupervisor(typeof(Work), typeof(Summary))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid scheduler configuration")]
    [Theory]
    [InlineData("MaxAttempts = 0")]
    [InlineData("RetryDelayMilliseconds = -1")]
    public Task Reports_Diagnostics_For_Invalid_Scheduler_Configuration(string configuration)
        => Given("an invalid scheduler agent supervisor configuration", () => Compile($$"""
            using PatternKit.Cloud.SchedulerAgentSupervisor;
            using PatternKit.Generators.SchedulerAgentSupervisor;
            public sealed record Work(string Id);
            public sealed record Summary(string Id);
            [GenerateSchedulerAgentSupervisor(typeof(Work), typeof(Summary), {{configuration}})]
            public static partial class SchedulerHost
            {
                [SchedulerAgent("agent")]
                private static Summary Run(SchedulerAgentContext<Work> context) => new(context.Work.Id);
            }
            """))
        .Then("the configuration diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKSAS004"))
        .AssertPassed();

    [Scenario("Generates scheduler agent supervisor defaults and host shapes")]
    [Fact]
    public Task Generates_Scheduler_Agent_Supervisor_Defaults_And_Host_Shapes()
        => Given("scheduler declarations with default names and host shapes", () => Compile("""
            using PatternKit.Cloud.SchedulerAgentSupervisor;
            using PatternKit.Generators.SchedulerAgentSupervisor;
            namespace Demo;
            public sealed record Work(string Id);
            public sealed record Summary(string Id);

            [GenerateSchedulerAgentSupervisor(typeof(Work), typeof(Summary))]
            internal abstract partial class AbstractScheduler
            {
                [SchedulerAgent("release-agent")]
                private static Summary Release(SchedulerAgentContext<Work> context) => new(context.Work.Id);
            }

            [GenerateSchedulerAgentSupervisor(typeof(Work), typeof(Summary), SupervisorName = "tenant\\\"scheduler")]
            public sealed partial class SealedScheduler
            {
                [SchedulerAgent("release-agent")]
                private static Summary Release(SchedulerAgentContext<Work> context) => new(context.Work.Id);
            }

            [GenerateSchedulerAgentSupervisor(typeof(Work), typeof(Summary), MaxAttempts = 2, RetryDelayMilliseconds = 0)]
            internal partial struct StructScheduler
            {
                [SchedulerAgent("release-agent")]
                private static Summary Release(SchedulerAgentContext<Work> context) => new(context.Work.Id);
            }
            """))
        .Then("generated sources preserve host shape and configured defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractScheduler", combined);
            ScenarioExpect.Contains("public sealed partial class SealedScheduler", combined);
            ScenarioExpect.Contains("internal partial struct StructScheduler", combined);
            ScenarioExpect.Contains("Create(\"scheduler-agent-supervisor\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"scheduler\")", combined);
            ScenarioExpect.Contains(".MaxAttempts(3)", combined);
            ScenarioExpect.Contains("FromMilliseconds(1000)", combined);
            ScenarioExpect.Contains(".MaxAttempts(2)", combined);
            ScenarioExpect.Contains("FromMilliseconds(0)", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested scheduler agent supervisor host wrappers")]
    [Fact]
    public Task Generates_Nested_Scheduler_Agent_Supervisor_Host_Wrappers()
        => Given("nested scheduler agent supervisor declarations", () => Compile("""
            using PatternKit.Cloud.SchedulerAgentSupervisor;
            using PatternKit.Generators.SchedulerAgentSupervisor;
            namespace Demo;
            public sealed record Work(string Id);
            public sealed record Summary(string Id);

            public partial class SchedulerContainer
            {
                private partial class PrivateHost
                {
                    [GenerateSchedulerAgentSupervisor(typeof(Work), typeof(Summary))]
                    protected partial class ProtectedScheduler
                    {
                        [SchedulerAgent("release-agent")]
                        private static Summary Release(SchedulerAgentContext<Work> context) => new(context.Work.Id);
                    }

                    [GenerateSchedulerAgentSupervisor(typeof(Work), typeof(Summary))]
                    private protected partial class PrivateProtectedScheduler
                    {
                        [SchedulerAgent("release-agent")]
                        private static Summary Release(SchedulerAgentContext<Work> context) => new(context.Work.Id);
                    }

                    [GenerateSchedulerAgentSupervisor(typeof(Work), typeof(Summary))]
                    protected internal partial class ProtectedInternalScheduler
                    {
                        [SchedulerAgent("release-agent")]
                        private static Summary Release(SchedulerAgentContext<Work> context) => new(context.Work.Id);
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class SchedulerContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedScheduler", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedScheduler", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalScheduler", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed scheduler agent supervisor type arguments")]
    [Theory]
    [InlineData("null!", "typeof(Summary)")]
    [InlineData("typeof(Work)", "null!")]
    public Task Skips_Malformed_Scheduler_Agent_Supervisor_Type_Arguments(string workType, string resultType)
        => Given("a scheduler declaration with a null type argument", () => Compile($$"""
            using PatternKit.Cloud.SchedulerAgentSupervisor;
            using PatternKit.Generators.SchedulerAgentSupervisor;
            public sealed record Work(string Id);
            public sealed record Summary(string Id);
            [GenerateSchedulerAgentSupervisor({{workType}}, {{resultType}})]
            public static partial class SchedulerHost
            {
                [SchedulerAgent("agent")]
                private static Summary Run(SchedulerAgentContext<Work> context) => new(context.Work.Id);
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "SchedulerAgentSupervisorGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(SchedulerAgentSupervisor<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new SchedulerAgentSupervisorGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
