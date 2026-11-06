using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.ActivityTracking;
using PatternKit.Examples.ActivityTrackingDemo;
using PatternKit.Examples.DependencyInjection;
using TinyBDD;

namespace PatternKit.Examples.Tests.ActivityTrackingDemo;

public sealed class DashboardActivityTrackerDemoTests
{
    [Scenario("Fluent activity tracker drives loading visibility")]
    [Fact]
    public void Fluent_Activity_Tracker_Drives_Loading_Visibility()
    {
        var summary = DashboardActivityTrackerDemoRunner.RunFluent(CreateRequest());

        ScenarioExpect.True(summary.LoadingVisible);
        ScenarioExpect.Equal(3, summary.ActiveWidgetLoads);
        ScenarioExpect.Equal(["orders", "inventory", "pricing"], summary.ActiveWidgets);
    }

    [Scenario("Generated activity tracker matches fluent behavior")]
    [Fact]
    public void Generated_Activity_Tracker_Matches_Fluent_Behavior()
    {
        var fluent = DashboardActivityTrackerDemoRunner.RunFluent(CreateRequest());
        var generated = DashboardActivityTrackerDemoRunner.RunGeneratedStatic(CreateRequest());

        ScenarioExpect.Equal(fluent.LoadingVisible, generated.LoadingVisible);
        ScenarioExpect.Equal(fluent.ActiveWidgetLoads, generated.ActiveWidgetLoads);
        ScenarioExpect.Equal(fluent.ActiveWidgets, generated.ActiveWidgets);
    }

    [Scenario("ServiceCollection imports activity tracker example")]
    [Fact]
    public void ServiceCollection_Imports_Activity_Tracker_Example()
    {
        var services = new ServiceCollection();
        services.AddDashboardActivityTrackerDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var runner = provider.GetRequiredService<DashboardActivityTrackerDemoRunner>();
        var summary = runner.RunGenerated(CreateRequest());

        ScenarioExpect.True(summary.LoadingVisible);
        ScenarioExpect.NotNull(provider.GetRequiredService<ActivityTracker>());
    }

    [Scenario("Aggregate examples import activity tracker example")]
    [Fact]
    public void Aggregate_Examples_Import_Activity_Tracker_Example()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<DashboardActivityTrackerExample>();
        var summary = example.Runner.RunGenerated(CreateRequest());

        ScenarioExpect.True(summary.LoadingVisible);
        ScenarioExpect.NotNull(example.Tracker);
    }

    private static DashboardLoadRequest CreateRequest()
        => new("REQ-100", ["orders", "inventory", "pricing"]);
}
