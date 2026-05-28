using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.ContextMaps;
using PatternKit.Generators.ContextMaps;
using GeneratedRelationshipKind = PatternKit.Generators.ContextMaps.ContextMapRelationshipKind;

namespace PatternKit.Examples.ContextMapDemo;

public static class CommerceContextMapDemo
{
    public sealed record CatalogProduct(string Sku, string Name);

    public sealed record FulfillmentProduct(string Sku, string Description);

    public sealed record BillingShipment(string ShipmentId, decimal Charge);

    public sealed record ContextMapSummary(int RelationshipCount, bool HasPublishedLanguage, bool HasCustomerSupplier);

    public sealed class CatalogToFulfillmentTranslator
    {
        public FulfillmentProduct Translate(CatalogProduct product) => new(product.Sku, product.Name);
    }

    public sealed class CommerceContextMapReporter(ContextMapDescriptor descriptor)
    {
        public ContextMapSummary Summarize()
            => new(
                descriptor.Relationships.Count,
                descriptor.Relationships.Any(static relationship => relationship.Kind == ContextRelationshipKind.PublishedLanguage),
                descriptor.Relationships.Any(static relationship => relationship.Kind == ContextRelationshipKind.CustomerSupplier));
    }

    public static ContextMapDescriptor CreateFluentMap()
        => ContextMapDescriptor.Create("Commerce")
            .AddRelationship("Catalog", "Fulfillment", ContextRelationshipKind.PublishedLanguage, "ProductFeed")
            .AddRelationship("Fulfillment", "Billing", ContextRelationshipKind.CustomerSupplier, "ShipmentBilling")
            .Build();

    public static ContextMapDescriptor CreateGeneratedMap()
        => GeneratedCommerceContextMap.Create();

    public static IServiceCollection AddCommerceContextMapDemo(this IServiceCollection services)
    {
        services.AddSingleton(CreateGeneratedMap());
        services.AddSingleton<CatalogToFulfillmentTranslator>();
        services.AddSingleton<CommerceContextMapReporter>();
        return services;
    }
}

[GenerateContextMapDescriptor("Commerce")]
[ContextMapRelationship("Catalog", "Fulfillment", GeneratedRelationshipKind.PublishedLanguage, "ProductFeed")]
[ContextMapRelationship("Fulfillment", "Billing", GeneratedRelationshipKind.CustomerSupplier, "ShipmentBilling")]
public static partial class GeneratedCommerceContextMap;
