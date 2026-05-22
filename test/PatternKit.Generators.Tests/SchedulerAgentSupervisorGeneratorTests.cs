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
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Scheduler_Declarations()
        => Given("invalid scheduler declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.SchedulerAgentSupervisor;
                [GenerateSchedulerAgentSupervisor(typeof(string), typeof(string))]
                public static class SchedulerHost;
                """),
            Compile("""
                using PatternKit.Generators.SchedulerAgentSupervisor;
                [GenerateSchedulerAgentSupervisor(typeof(string), typeof(string))]
                public static partial class SchedulerHost;
                """),
            Compile("""
                using PatternKit.Generators.SchedulerAgentSupervisor;
                [GenerateSchedulerAgentSupervisor(typeof(string), typeof(string))]
                public static partial class SchedulerHost
                {
                    [SchedulerAgent("agent")]
                    private static int Run(string value) => 1;
                }
                """),
            Compile("""
                using PatternKit.Cloud.SchedulerAgentSupervisor;
                using PatternKit.Generators.SchedulerAgentSupervisor;
                [GenerateSchedulerAgentSupervisor(typeof(string), typeof(string), MaxAttempts = 0)]
                public static partial class SchedulerHost
                {
                    [SchedulerAgent("agent")]
                    private static string Run(SchedulerAgentContext<string> context) => context.Work;
                }
                """),
            Compile("""
                using PatternKit.Cloud.SchedulerAgentSupervisor;
                using PatternKit.Generators.SchedulerAgentSupervisor;
                [GenerateSchedulerAgentSupervisor(typeof(string), typeof(string))]
                public static partial class SchedulerHost
                {
                    [SchedulerAgent("agent")]
                    private static string Run(SchedulerAgentContext<string> context) => context.Work;
                    [SchedulerAgent("agent")]
                    private static string RunAgain(SchedulerAgentContext<string> context) => context.Work;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKSAS001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKSAS002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKSAS003");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKSAS004");
            ScenarioExpect.Contains(results[4].Diagnostics, diagnostic => diagnostic.Id == "PKSAS005");
        })
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
