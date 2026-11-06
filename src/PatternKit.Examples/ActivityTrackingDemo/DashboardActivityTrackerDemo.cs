using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.ActivityTracking;
using PatternKit.Generators.ActivityTracking;

namespace PatternKit.Examples.ActivityTrackingDemo;

public sealed record DashboardLoadRequest(string RequestId, IReadOnlyList<string> Widgets);

public sealed record DashboardLoadSummary(bool LoadingVisible, int ActiveWidgetLoads, IReadOnlyList<string> ActiveWidgets);

public sealed class DashboardActivityTrackerService(ActivityTracker tracker)
{
    public DashboardLoadSummary BeginLoading(DashboardLoadRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        foreach (var widget in request.Widgets)
            tracker.Track(widget, request.RequestId);

        return ToSummary();
    }

    public DashboardLoadSummary Complete(string activityId)
    {
        tracker.Complete(activityId);
        return ToSummary();
    }

    public DashboardLoadSummary ToSummary()
    {
        var state = tracker.GetGateState();
        return new(
            state.IsBlocked,
            state.ActiveCount,
            state.ActiveActivities.Select(static activity => activity.Name).ToArray());
    }
}

public static partial class DashboardActivityTrackers
{
    public static ActivityTracker CreateFluent()
        => ActivityTracker.Create("dashboard-loading").Build();
}

[GenerateActivityTracker(FactoryMethodName = "CreateGenerated", TrackerName = "dashboard-loading")]
public static partial class GeneratedDashboardActivityTracker;

public sealed class DashboardActivityTrackerDemoRunner(DashboardActivityTrackerService service)
{
    public DashboardLoadSummary RunGenerated(DashboardLoadRequest request)
        => service.BeginLoading(request);

    public static DashboardLoadSummary RunFluent(DashboardLoadRequest request)
        => new DashboardActivityTrackerService(DashboardActivityTrackers.CreateFluent()).BeginLoading(request);

    public static DashboardLoadSummary RunGeneratedStatic(DashboardLoadRequest request)
        => new DashboardActivityTrackerService(GeneratedDashboardActivityTracker.CreateGenerated()).BeginLoading(request);
}

public static class DashboardActivityTrackerDemoServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardActivityTrackerDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedDashboardActivityTracker.CreateGenerated());
        services.AddSingleton<DashboardActivityTrackerService>();
        services.AddSingleton<DashboardActivityTrackerDemoRunner>();
        return services;
    }
}
