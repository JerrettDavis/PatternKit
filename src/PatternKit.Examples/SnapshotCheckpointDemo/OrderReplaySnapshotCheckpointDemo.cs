using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.EventSourcing;
using PatternKit.Application.SnapshotCheckpoints;
using PatternKit.Generators.SnapshotCheckpoints;

namespace PatternKit.Examples.SnapshotCheckpointDemo;

public static class OrderReplaySnapshotCheckpointDemo
{
    public static async ValueTask<OrderReplaySummary> RunFluentAsync()
    {
        var manager = OrderReplaySnapshotCheckpointPolicies.CreateFluentManager();
        var service = new OrderReplayService(CreateSeededStore(), manager);
        return await service.ReplayAsync("order-100", 2);
    }

    public static async ValueTask<OrderReplaySummary> RunGeneratedAsync()
    {
        var manager = GeneratedOrderReplayCheckpoints.CreateManager();
        var service = new OrderReplayService(CreateSeededStore(), manager);
        return await service.ReplayAsync("order-200", 3);
    }

    public static IEventStore<OrderReplayEvent, string> CreateSeededStore()
    {
        var store = InMemoryEventStore<OrderReplayEvent, string>.Create("order-replay-events").Build();
        Seed(store, "order-100").GetAwaiter().GetResult();
        Seed(store, "order-200").GetAwaiter().GetResult();
        return store;
    }

    private static async Task Seed(IEventStore<OrderReplayEvent, string> store, string orderId)
    {
        _ = await store.AppendAsync(orderId, 0, [
            new OrderReplayPlaced(orderId, "customer-1", 125m, DateTimeOffset.UtcNow),
            new OrderReplayPaid(orderId, "payment-1", 125m, DateTimeOffset.UtcNow),
            new OrderReplayShipped(orderId, "tracking-1", DateTimeOffset.UtcNow)
        ]).ConfigureAwait(false);
    }
}

public abstract record OrderReplayEvent(string OrderId, DateTimeOffset OccurredAt);

public sealed record OrderReplayPlaced(string OrderId, string CustomerId, decimal Total, DateTimeOffset OccurredAt)
    : OrderReplayEvent(OrderId, OccurredAt);

public sealed record OrderReplayPaid(string OrderId, string PaymentId, decimal Amount, DateTimeOffset OccurredAt)
    : OrderReplayEvent(OrderId, OccurredAt);

public sealed record OrderReplayShipped(string OrderId, string TrackingNumber, DateTimeOffset OccurredAt)
    : OrderReplayEvent(OrderId, OccurredAt);

public sealed record OrderReplaySnapshot(
    string OrderId,
    string CustomerId,
    decimal Total,
    decimal PaidTotal,
    bool Shipped,
    long Version);

public sealed record OrderReplaySummary(
    string ManagerName,
    string OrderId,
    long StartingVersion,
    long FinalVersion,
    bool UsedCheckpoint,
    bool RebuiltCheckpoint,
    bool Shipped,
    decimal PaidTotal);

public static class OrderReplaySnapshotCheckpointPolicies
{
    public static SnapshotCheckpointManager<string, OrderReplaySnapshot> CreateFluentManager()
        => SnapshotCheckpointManager<string, OrderReplaySnapshot>
            .Create("order-replay-checkpoints")
            .UseComparer(StringComparer.OrdinalIgnoreCase)
            .Build();
}

public sealed class OrderReplayService
{
    private readonly IEventStore<OrderReplayEvent, string> _store;
    private readonly SnapshotCheckpointManager<string, OrderReplaySnapshot> _checkpoints;

    public OrderReplayService(
        IEventStore<OrderReplayEvent, string> store,
        SnapshotCheckpointManager<string, OrderReplaySnapshot> checkpoints)
    {
        _store = store;
        _checkpoints = checkpoints;
    }

    public async ValueTask<OrderReplaySummary> ReplayAsync(
        string orderId,
        long minimumCheckpointVersion,
        CancellationToken cancellationToken = default)
    {
        var load = _checkpoints.Load(orderId, minimumCheckpointVersion);
        var startingVersion = load.IsUsable ? load.Checkpoint!.Version : 0;
        var snapshot = load.IsUsable
            ? load.Checkpoint!.Snapshot
            : new OrderReplaySnapshot(orderId, "", 0m, 0m, false, 0);

        var events = await _store.ReadStreamAsync(orderId, cancellationToken).ConfigureAwait(false);
        foreach (var stored in events.Where(entry => entry.Version > startingVersion).OrderBy(static entry => entry.Version))
            snapshot = Apply(snapshot, stored.Event, stored.Version);

        var save = _checkpoints.Save(
            orderId,
            snapshot.Version,
            snapshot,
            $"replay:{orderId}",
            new Dictionary<string, string> { ["source"] = "event-store" });

        return new OrderReplaySummary(
            _checkpoints.Name,
            snapshot.OrderId,
            startingVersion,
            snapshot.Version,
            load.IsUsable,
            save.IsSaved && !load.IsUsable,
            snapshot.Shipped,
            snapshot.PaidTotal);
    }

    private static OrderReplaySnapshot Apply(OrderReplaySnapshot snapshot, OrderReplayEvent @event, long version)
        => @event switch
        {
            OrderReplayPlaced placed => snapshot with
            {
                OrderId = placed.OrderId,
                CustomerId = placed.CustomerId,
                Total = placed.Total,
                Version = version
            },
            OrderReplayPaid paid => snapshot with
            {
                OrderId = paid.OrderId,
                PaidTotal = snapshot.PaidTotal + paid.Amount,
                Version = version
            },
            OrderReplayShipped shipped => snapshot with
            {
                OrderId = shipped.OrderId,
                Shipped = true,
                Version = version
            },
            _ => snapshot
        };
}

public sealed record OrderReplaySnapshotCheckpointDemoRunner(
    Func<ValueTask<OrderReplaySummary>> RunFluentAsync,
    Func<ValueTask<OrderReplaySummary>> RunGeneratedAsync);

public static class OrderReplaySnapshotCheckpointServiceCollectionExtensions
{
    public static IServiceCollection AddOrderReplaySnapshotCheckpointDemo(this IServiceCollection services)
    {
        services.AddSingleton<IEventStore<OrderReplayEvent, string>>(_ => OrderReplaySnapshotCheckpointDemo.CreateSeededStore());
        services.AddSingleton(_ => OrderReplaySnapshotCheckpointPolicies.CreateFluentManager());
        services.AddTransient<OrderReplayService>();
        services.AddSingleton(new OrderReplaySnapshotCheckpointDemoRunner(
            OrderReplaySnapshotCheckpointDemo.RunFluentAsync,
            OrderReplaySnapshotCheckpointDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateSnapshotCheckpointManager(typeof(string), typeof(OrderReplaySnapshot), FactoryMethodName = "CreateManager", ManagerName = "order-replay-checkpoints")]
public static partial class GeneratedOrderReplayCheckpoints;
