using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.BoundedContexts;
using PatternKit.Generators.BoundedContexts;

namespace PatternKit.Examples.BoundedContextDemo;

public static class FulfillmentBoundedContextDemo
{
    public sealed record CatalogProduct(string Sku, decimal Weight);

    public sealed record FulfillmentItem(string Sku, decimal Weight);

    public sealed record FulfillmentPlan(string Sku, string Carrier, bool InventoryReserved);

    public interface IShipmentQuoter
    {
        string SelectCarrier(FulfillmentItem item);
    }

    public interface IInventoryAllocator
    {
        bool Reserve(FulfillmentItem item);
    }

    public sealed class ShipmentQuoter : IShipmentQuoter
    {
        public string SelectCarrier(FulfillmentItem item) => item.Weight > 25m ? "freight" : "parcel";
    }

    public sealed class InventoryAllocator : IInventoryAllocator
    {
        public bool Reserve(FulfillmentItem item) => !string.IsNullOrWhiteSpace(item.Sku);
    }

    public sealed class FulfillmentPlanner(IShipmentQuoter quoter, IInventoryAllocator allocator)
    {
        public FulfillmentPlan Plan(CatalogProduct product)
        {
            var item = Translate(product);
            return new FulfillmentPlan(item.Sku, quoter.SelectCarrier(item), allocator.Reserve(item));
        }
    }

    public static BoundedContextDescriptor CreateFluentDescriptor()
        => BoundedContextDescriptor.Create("Fulfillment")
            .AddCapability("quote shipment", typeof(IShipmentQuoter))
            .AddCapability("allocate inventory", typeof(IInventoryAllocator))
            .AddAdapter("Catalog", "Fulfillment", typeof(CatalogProduct), typeof(FulfillmentItem))
            .Build();

    public static BoundedContextDescriptor CreateGeneratedDescriptor()
        => GeneratedFulfillmentContext.Create();

    public static FulfillmentItem Translate(CatalogProduct product)
        => new(product.Sku, product.Weight);

    public static IServiceCollection AddFulfillmentBoundedContextDemo(this IServiceCollection services)
    {
        services.AddSingleton<IShipmentQuoter, ShipmentQuoter>();
        services.AddSingleton<IInventoryAllocator, InventoryAllocator>();
        services.AddSingleton<FulfillmentPlanner>();
        services.AddSingleton(CreateGeneratedDescriptor());
        return services;
    }
}

[GenerateBoundedContextDescriptor("Fulfillment")]
[BoundedContextCapability("quote shipment", typeof(FulfillmentBoundedContextDemo.IShipmentQuoter))]
[BoundedContextCapability("allocate inventory", typeof(FulfillmentBoundedContextDemo.IInventoryAllocator))]
[BoundedContextAdapter(
    "Catalog",
    "Fulfillment",
    typeof(FulfillmentBoundedContextDemo.CatalogProduct),
    typeof(FulfillmentBoundedContextDemo.FulfillmentItem))]
public static partial class GeneratedFulfillmentContext;
