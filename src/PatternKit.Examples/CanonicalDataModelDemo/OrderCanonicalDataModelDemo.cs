using Microsoft.Extensions.DependencyInjection;
using PatternKit.EnterpriseIntegration.CanonicalDataModel;
using PatternKit.Generators.CanonicalDataModel;

namespace PatternKit.Examples.CanonicalDataModelDemo;

public sealed record PartnerOrderDocument(string ExternalOrderId, decimal Amount, string CurrencyCode);

public sealed record MarketplaceOrderDocument(string MarketplaceId, int AmountInCents, string Currency);

public sealed record CanonicalCommerceOrder(string OrderId, decimal Total, string Currency);

public sealed record CanonicalOrderSummary(string ModelName, string AdapterName, string OrderId, decimal Total, string Currency);

public sealed class CanonicalOrderImportService(CanonicalDataModel<CanonicalCommerceOrder> model)
{
    public CanonicalOrderSummary ImportPartnerOrder(PartnerOrderDocument order)
    {
        var result = model.Normalize(order);
        if (result.Failed)
            throw new InvalidOperationException("Partner order could not be normalized.", result.Exception);

        var canonical = result.Value!;
        return new(result.ModelName, result.AdapterName, canonical.OrderId, canonical.Total, canonical.Currency);
    }
}

public static class CanonicalOrderModels
{
    public static CanonicalDataModel<CanonicalCommerceOrder> CreateFluent()
        => CanonicalDataModel<CanonicalCommerceOrder>.Create("commerce-orders")
            .From<PartnerOrderDocument>("partner-orders", ToCanonical)
            .From<MarketplaceOrderDocument>("marketplace-orders", static order => new(
                order.MarketplaceId,
                order.AmountInCents / 100m,
                order.Currency.ToUpperInvariant()))
            .Build();

    public static CanonicalCommerceOrder ToCanonical(PartnerOrderDocument order)
        => new(order.ExternalOrderId, order.Amount, order.CurrencyCode.ToUpperInvariant());
}

[GenerateCanonicalDataModel(typeof(PartnerOrderDocument), typeof(CanonicalCommerceOrder), FactoryMethodName = "Create", ModelName = "commerce-orders", AdapterName = "partner-orders")]
public static partial class GeneratedPartnerOrderCanonicalDataModel
{
    [CanonicalDataModelMapper]
    private static CanonicalCommerceOrder Map(PartnerOrderDocument order) => CanonicalOrderModels.ToCanonical(order);
}

public sealed class CanonicalOrderDemoRunner(CanonicalOrderImportService service)
{
    public CanonicalOrderSummary RunGenerated(PartnerOrderDocument order) => service.ImportPartnerOrder(order);

    public static CanonicalOrderSummary RunFluent()
    {
        var result = CanonicalOrderModels.CreateFluent().Normalize(new MarketplaceOrderDocument("M-200", 7500, "usd"));
        var canonical = result.Value!;
        return new(result.ModelName, result.AdapterName, canonical.OrderId, canonical.Total, canonical.Currency);
    }

    public static CanonicalOrderSummary RunGeneratedStatic()
    {
        var service = new CanonicalOrderImportService(GeneratedPartnerOrderCanonicalDataModel.Create());
        return service.ImportPartnerOrder(new PartnerOrderDocument("P-100", 42.50m, "usd"));
    }
}

public static class CanonicalOrderServiceCollectionExtensions
{
    public static IServiceCollection AddCanonicalOrderDataModelDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedPartnerOrderCanonicalDataModel.Create());
        services.AddSingleton<CanonicalOrderImportService>();
        services.AddSingleton<CanonicalOrderDemoRunner>();
        return services;
    }
}
