using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatternKit.Cloud.SchedulerAgentSupervisor;
using PatternKit.Generators.SchedulerAgentSupervisor;

namespace PatternKit.Examples.SchedulerAgentSupervisorDemo;

public sealed record WarehouseReplenishmentWork(string BatchId, bool FailFirstAttempt, List<string> Log);

public sealed record WarehouseReplenishmentSummary(string BatchId, int Attempt);

public sealed class WarehouseSchedulerService(SchedulerAgentSupervisor<WarehouseReplenishmentWork, WarehouseReplenishmentSummary> supervisor)
{
    public SchedulerAgentSupervisor<WarehouseReplenishmentWork, WarehouseReplenishmentSummary> Supervisor { get; } = supervisor;

    public void Schedule(WarehouseReplenishmentWork work, DateTimeOffset dueAt)
        => Supervisor.Schedule($"replenish:{work.BatchId}", work, dueAt);

    public IReadOnlyList<SchedulerAgentResult<WarehouseReplenishmentSummary>> RunDue(DateTimeOffset now)
        => Supervisor.RunDue(now);
}

public sealed class WarehouseSchedulerHostedService(WarehouseSchedulerService service) : IHostedService
{
    public DateTimeOffset Now { get; } = new(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);

    public WarehouseReplenishmentWork Work { get; } = new("B-100", FailFirstAttempt: false, []);

    public IReadOnlyList<SchedulerAgentResult<WarehouseReplenishmentSummary>> LastResults { get; private set; } = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        service.Schedule(Work, Now);
        LastResults = service.RunDue(Now);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class WarehouseSchedulers
{
    public static SchedulerAgentSupervisor<WarehouseReplenishmentWork, WarehouseReplenishmentSummary> CreateFluent()
        => SchedulerAgentSupervisor<WarehouseReplenishmentWork, WarehouseReplenishmentSummary>
            .Create("warehouse-replenishment-scheduler")
            .Supervision(SchedulerSupervisionPolicy<WarehouseReplenishmentWork>.Create()
                .MaxAttempts(2)
                .RetryDelay(TimeSpan.FromSeconds(5))
                .RetryWhen((_, ctx) => ctx.Work.FailFirstAttempt)
                .Build())
            .Agent("release-replenishment", ctx =>
            {
                ctx.Work.Log.Add($"fluent:{ctx.Attempt}");
                return new WarehouseReplenishmentSummary(ctx.Work.BatchId, ctx.Attempt);
            })
            .Build();
}

[GenerateSchedulerAgentSupervisor(
    typeof(WarehouseReplenishmentWork),
    typeof(WarehouseReplenishmentSummary),
    FactoryMethodName = "Create",
    SupervisorName = "warehouse-replenishment-scheduler",
    MaxAttempts = 2,
    RetryDelayMilliseconds = 5000)]
public static partial class GeneratedWarehouseScheduler
{
    [SchedulerAgent("release-replenishment")]
    private static WarehouseReplenishmentSummary Release(SchedulerAgentContext<WarehouseReplenishmentWork> context)
    {
        context.Work.Log.Add($"generated:{context.Attempt}");
        if (context.Work.FailFirstAttempt && context.Attempt == 1)
            throw new InvalidOperationException("inventory feed unavailable");

        return new WarehouseReplenishmentSummary(context.Work.BatchId, context.Attempt);
    }

    [SchedulerRetryWhen]
    private static bool Retry(Exception exception, SchedulerAgentContext<WarehouseReplenishmentWork> context)
        => exception is InvalidOperationException && context.Work.FailFirstAttempt;
}

public sealed class WarehouseSchedulerDemoRunner(WarehouseSchedulerService service)
{
    public IReadOnlyList<SchedulerAgentResult<WarehouseReplenishmentSummary>> RunGenerated()
    {
        var now = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);
        var work = new WarehouseReplenishmentWork("B-100", FailFirstAttempt: true, []);
        service.Schedule(work, now);
        var first = service.RunDue(now);
        var retry = service.RunDue(now.AddSeconds(5));
        return first.Concat(retry).ToArray();
    }

    public SchedulerAgentResult<WarehouseReplenishmentSummary> RunFluent()
    {
        var now = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);
        var work = new WarehouseReplenishmentWork("B-200", FailFirstAttempt: false, []);
        var supervisor = WarehouseSchedulers.CreateFluent().Schedule("replenish:B-200", work, now);
        return supervisor.RunDue(now).Single();
    }
}

public static class WarehouseSchedulerServiceCollectionExtensions
{
    public static IServiceCollection AddWarehouseSchedulerAgentSupervisorDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedWarehouseScheduler.Create());
        services.AddSingleton<WarehouseSchedulerService>();
        services.AddSingleton<WarehouseSchedulerDemoRunner>();
        services.AddSingleton<WarehouseSchedulerHostedService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<WarehouseSchedulerHostedService>());
        return services;
    }
}
