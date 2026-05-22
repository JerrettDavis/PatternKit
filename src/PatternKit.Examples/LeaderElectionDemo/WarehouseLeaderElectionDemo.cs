using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatternKit.Cloud.LeaderElection;
using PatternKit.Generators.LeaderElection;

namespace PatternKit.Examples.LeaderElectionDemo;

public sealed record WarehouseWorkerContext(string NodeId, List<string> Log);

public sealed class WarehouseLeaderElectionService(LeaderElection<WarehouseWorkerContext> election)
{
    public LeaderElectionResult TryLead(WarehouseWorkerContext context)
    {
        var candidate = GeneratedWarehouseLeaderElection.Create(context);
        return election.TryAcquire(candidate);
    }

    public LeaderElectionResult Renew(WarehouseWorkerContext context)
        => election.Renew(GeneratedWarehouseLeaderElection.Create(context));

    public LeaderElectionResult Release(WarehouseWorkerContext context)
        => election.Release(GeneratedWarehouseLeaderElection.Create(context));
}

public sealed class WarehouseLeadershipHostedService(WarehouseLeaderElectionService service) : IHostedService
{
    public WarehouseWorkerContext Context { get; } = new("warehouse-node-a", []);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        service.TryLead(Context);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        service.Release(Context);
        return Task.CompletedTask;
    }
}

public static class WarehouseLeaderElections
{
    public static LeaderElection<WarehouseWorkerContext> CreateFluent(Func<DateTimeOffset>? clock = null)
        => LeaderElection<WarehouseWorkerContext>.Create("warehouse-replenishment-leader")
            .LeaseDuration(TimeSpan.FromSeconds(30))
            .Clock(clock ?? (() => DateTimeOffset.UtcNow))
            .Build();
}

[GenerateLeaderElection(typeof(WarehouseWorkerContext), FactoryMethodName = "Create", ElectionName = "warehouse-replenishment-leader", LeaseDurationMilliseconds = 30000)]
public static partial class GeneratedWarehouseLeaderElection
{
    [LeaderCandidateId]
    private static string CandidateId(WarehouseWorkerContext context) => context.NodeId;

    [LeaderAcquired]
    private static void Acquired(LeaderLease lease, WarehouseWorkerContext context)
        => context.Log.Add($"acquired:{lease.Term}");

    [LeaderRenewed]
    private static void Renewed(LeaderLease lease, WarehouseWorkerContext context)
        => context.Log.Add($"renewed:{lease.Term}");

    [LeaderReleased]
    private static void Released(WarehouseWorkerContext context)
        => context.Log.Add("released");
}

public sealed class WarehouseLeaderElectionDemoRunner(WarehouseLeaderElectionService service)
{
    public IReadOnlyList<string> RunGenerated()
    {
        var context = new WarehouseWorkerContext("warehouse-node-a", []);
        service.TryLead(context);
        service.Renew(context);
        service.Release(context);
        return context.Log;
    }

    public static LeaderElectionResult RunFluent()
    {
        var now = DateTimeOffset.UtcNow;
        var election = WarehouseLeaderElections.CreateFluent(() => now);
        var context = new WarehouseWorkerContext("warehouse-node-a", []);
        var candidate = LeaderElectionCandidate.Create(context.NodeId, context).Build();
        return election.TryAcquire(candidate);
    }
}

public static class WarehouseLeaderElectionServiceCollectionExtensions
{
    public static IServiceCollection AddWarehouseLeaderElectionDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedWarehouseLeaderElection.CreateElection());
        services.AddSingleton<WarehouseLeaderElectionService>();
        services.AddSingleton<WarehouseLeaderElectionDemoRunner>();
        services.AddHostedService<WarehouseLeadershipHostedService>();
        return services;
    }
}
