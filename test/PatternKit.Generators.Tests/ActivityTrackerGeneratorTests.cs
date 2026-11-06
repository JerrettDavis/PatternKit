using Microsoft.CodeAnalysis;
using PatternKit.Generators.ActivityTracking;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Activity Tracker generator")]
public sealed partial class ActivityTrackerGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates activity tracker factory")]
    [Fact]
    public Task Generates_Activity_Tracker_Factory()
        => Given("an activity tracker declaration", () => Compile("""
            using PatternKit.Generators.ActivityTracking;

            namespace Demo;

            [GenerateActivityTracker(FactoryMethodName = "Build", TrackerName = "dashboard-loading")]
            public static partial class DashboardTracker;
            """))
        .Then("the generated source creates the configured tracker", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class DashboardTracker", source);
            ScenarioExpect.Contains("ActivityTracker Build()", source);
            ScenarioExpect.Contains("ActivityTracker.Create(\"dashboard-loading\").Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostic for non-partial activity tracker declarations")]
    [Fact]
    public Task Reports_Diagnostic_For_Non_Partial_Activity_Tracker_Declarations()
        => Given("a non-partial activity tracker declaration", () => Compile("""
            using PatternKit.Generators.ActivityTracking;

            [GenerateActivityTracker]
            public static class DashboardTracker;
            """))
        .Then("the diagnostic identifies the host", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKAT001"))
        .AssertPassed();

    [Scenario("Reports diagnostic for invalid activity tracker configuration")]
    [Theory]
    [InlineData("FactoryMethodName = \"\"", "PKAT002")]
    [InlineData("TrackerName = \"\"", "PKAT002")]
    [InlineData("FactoryMethodName = \"   \"", "PKAT002")]
    [InlineData("TrackerName = \"   \"", "PKAT002")]
    public Task Reports_Diagnostic_For_Invalid_Activity_Tracker_Configuration(string invalidConfiguration, string expected)
        => Given("an invalid activity tracker declaration", () => Compile($$"""
            using PatternKit.Generators.ActivityTracking;

            [GenerateActivityTracker({{invalidConfiguration}})]
            public static partial class DashboardTracker;
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == expected))
        .AssertPassed();

    [Scenario("Generates activity tracker defaults and type shapes")]
    [Fact]
    public Task Generates_Activity_Tracker_Defaults_And_Type_Shapes()
        => Given("activity tracker declarations using default names and different host shapes", () => Compile("""
            using PatternKit.Generators.ActivityTracking;

            namespace Demo;

            [GenerateActivityTracker]
            internal abstract partial class AbstractTracker;

            [GenerateActivityTracker(TrackerName = "tenant\\\"dashboard")]
            public sealed partial class SealedTracker;

            [GenerateActivityTracker]
            internal partial struct StructTracker;
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractTracker", combined);
            ScenarioExpect.Contains("Create()", combined);
            ScenarioExpect.Contains("ActivityTracker.Create(\"activity-tracker\")", combined);
            ScenarioExpect.Contains("public sealed partial class SealedTracker", combined);
            ScenarioExpect.Contains("ActivityTracker.Create(\"tenant\\\\\\\"dashboard\")", combined);
            ScenarioExpect.Contains("internal partial struct StructTracker", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested activity tracker host wrappers")]
    [Fact]
    public Task Generates_Nested_Activity_Tracker_Host_Wrappers()
        => Given("nested activity tracker declarations with non-public accessibility", () => Compile("""
            using PatternKit.Generators.ActivityTracking;

            namespace Demo;

            public partial class TrackerContainer
            {
                private partial class PrivateHost
                {
                    [GenerateActivityTracker]
                    protected partial class ProtectedTracker;

                    [GenerateActivityTracker]
                    private protected partial class PrivateProtectedTracker;

                    [GenerateActivityTracker]
                    protected internal partial class ProtectedInternalTracker;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class TrackerContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedTracker", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedTracker", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalTracker", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "ActivityTrackerGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Application.ActivityTracking.ActivityTracker).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new ActivityTrackerGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);
}
