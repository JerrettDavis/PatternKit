using Microsoft.Extensions.DependencyInjection;
using PatternKit.EnterpriseIntegration.EventCarriedStateTransfer;
using PatternKit.Generators.EventCarriedStateTransfer;

namespace PatternKit.Examples.EventCarriedStateTransferDemo;

public sealed record InventoryAdjustedEvent(string Sku, int QuantityOnHand, string Warehouse, long Version);

public sealed record InventoryReadModel(string Sku, int QuantityOnHand, string Warehouse);

public sealed record InventoryProjectionSummary(string TransferName, string Sku, long Version, int QuantityOnHand, string Warehouse);

public interface IInventoryReadModelStore
{
    void Upsert(string sku, long version, InventoryReadModel state);

    InventoryProjectionSummary? Find(string sku);
}

public sealed class InMemoryInventoryReadModelStore : IInventoryReadModelStore
{
    private readonly Dictionary<string, InventoryProjectionSummary> _summaries = new(StringComparer.OrdinalIgnoreCase);

    public void Upsert(string sku, long version, InventoryReadModel state)
        => _summaries[sku] = new("inventory-state", sku, version, state.QuantityOnHand, state.Warehouse);

    public InventoryProjectionSummary? Find(string sku)
        => _summaries.TryGetValue(sku, out var summary) ? summary : null;
}

public sealed class InventoryProjectionService(
    EventCarriedStateTransfer<InventoryAdjustedEvent, string, InventoryReadModel> transfer,
    IInventoryReadModelStore store)
{
    public InventoryProjectionSummary Project(InventoryAdjustedEvent evt)
    {
        var result = transfer.Transfer(evt);
        if (result.Failed)
            throw new InvalidOperationException("Inventory event could not transfer carried state.", result.Exception);

        store.Upsert(result.Key!, result.Version, result.State!);
        return store.Find(result.Key!)!;
    }
}

public static class InventoryStateTransfers
{
    public static EventCarriedStateTransfer<InventoryAdjustedEvent, string, InventoryReadModel> CreateFluent()
        => EventCarriedStateTransfer<InventoryAdjustedEvent, string, InventoryReadModel>.Create("inventory-state")
            .WithKey(static evt => evt.Sku)
            .WithVersion(static evt => evt.Version)
            .WithState(ToReadModel)
            .Build();

    public static InventoryReadModel ToReadModel(InventoryAdjustedEvent evt)
        => new(evt.Sku, evt.QuantityOnHand, evt.Warehouse);
}

[GenerateEventCarriedStateTransfer(typeof(InventoryAdjustedEvent), typeof(string), typeof(InventoryReadModel), FactoryMethodName = "Create", TransferName = "inventory-state")]
public static partial class GeneratedInventoryStateTransfer
{
    [EventCarriedStateKey]
    private static string Key(InventoryAdjustedEvent evt) => evt.Sku;

    [EventCarriedStateVersion]
    private static long Version(InventoryAdjustedEvent evt) => evt.Version;

    [EventCarriedStateMapper]
    private static InventoryReadModel Map(InventoryAdjustedEvent evt) => InventoryStateTransfers.ToReadModel(evt);
}

public sealed class InventoryEventCarriedStateTransferDemoRunner(InventoryProjectionService service)
{
    public InventoryProjectionSummary RunGenerated(InventoryAdjustedEvent evt) => service.Project(evt);

    public static InventoryProjectionSummary RunFluent()
    {
        var store = new InMemoryInventoryReadModelStore();
        var service = new InventoryProjectionService(InventoryStateTransfers.CreateFluent(), store);
        return service.Project(new InventoryAdjustedEvent("SKU-100", 12, "CHI-01", 4));
    }
}

public static class InventoryEventCarriedStateTransferServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryEventCarriedStateTransferDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedInventoryStateTransfer.Create());
        services.AddSingleton<IInventoryReadModelStore, InMemoryInventoryReadModelStore>();
        services.AddSingleton<InventoryProjectionService>();
        services.AddSingleton<InventoryEventCarriedStateTransferDemoRunner>();
        return services;
    }
}
