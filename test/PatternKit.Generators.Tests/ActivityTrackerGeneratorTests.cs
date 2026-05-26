using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.ActivityTracking;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ActivityTrackerGeneratorTests
{
    [Scenario("Generates activity tracker factory")]
    [Fact]
    public void GeneratesActivityTrackerFactory()
    {
        var source = """
            using PatternKit.Generators.ActivityTracking;

            namespace Demo;

            [GenerateActivityTracker(FactoryMethodName = "Build", TrackerName = "dashboard-loading")]
            public static partial class DashboardTracker;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesActivityTrackerFactory));
        _ = RoslynTestHelpers.Run(comp, new ActivityTrackerGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(static result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("DashboardTracker.ActivityTracker.g.cs", generated.HintName);
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("ActivityTracker.Create(\"dashboard-loading\").Build()", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Reports activity tracker diagnostics")]
    [Theory]
    [InlineData("public static class DashboardTracker;", "PKAT001")]
    [InlineData("public static partial class DashboardTracker;", "PKAT002")]
    public void ReportsActivityTrackerDiagnostics(string declaration, string expected)
    {
        var invalidConfig = expected == "PKAT002" ? "TrackerName = \"\"" : "";
        var source = $$"""
            using PatternKit.Generators.ActivityTracking;

            namespace Demo;

            [GenerateActivityTracker({{invalidConfig}})]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsActivityTrackerDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new ActivityTrackerGenerator(), out var run, out _);

        ScenarioExpect.Equal(expected, ScenarioExpect.Single(run.Results.SelectMany(static result => result.Diagnostics)).Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Application.ActivityTracking.ActivityTracker).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(ActivityTrackerGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");
}
