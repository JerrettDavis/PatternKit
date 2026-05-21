using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.AntiCorruption;
using PatternKit.Generators.AntiCorruption;

namespace PatternKit.Examples.AntiCorruptionDemo;

public sealed record LegacyOrderDto(string OrderNumber, decimal GrossAmount, string CurrencyCode, string CustomerCode);
public sealed record CommerceOrder(string OrderId, decimal TotalUsd, string CustomerId);
public sealed record OrderImportResult(string SourceSystem, bool Accepted, bool Rejected, string? RejectionReason, CommerceOrder? Order);

public interface ILegacyOrderFeed
{
    ValueTask<LegacyOrderDto> ReadAsync(string orderNumber, CancellationToken cancellationToken = default);
}

public sealed class ScriptedLegacyOrderFeed : ILegacyOrderFeed
{
    public ValueTask<LegacyOrderDto> ReadAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        cancellationToken.ThrowIfCancellationRequested();
        return new(new LegacyOrderDto(orderNumber, 125m, "USD", "CUST-42"));
    }
}

public sealed class LegacyOrderImportService(
    ILegacyOrderFeed feed,
    AntiCorruptionLayer<LegacyOrderDto, CommerceOrder> layer)
{
    public async ValueTask<OrderImportResult> ImportAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        var legacy = await feed.ReadAsync(orderNumber, cancellationToken);
        return Import(legacy);
    }

    public OrderImportResult Import(LegacyOrderDto legacy)
    {
        var result = layer.Translate(legacy);
        return new(
            result.SourceSystem,
            result.Accepted,
            result.Rejected,
            result.RejectionReason,
            result.Value);
    }
}

public static partial class LegacyOrderAntiCorruptionPolicies
{
    public static AntiCorruptionLayer<LegacyOrderDto, CommerceOrder> CreateFluentLayer()
        => AntiCorruptionLayer<LegacyOrderDto, CommerceOrder>
            .Create("legacy-order-import")
            .FromSource("legacy-erp")
            .RequireExternal(static order => !string.IsNullOrWhiteSpace(order.OrderNumber), "Legacy order number is required.")
            .RequireExternal(static order => string.Equals(order.CurrencyCode, "USD", StringComparison.OrdinalIgnoreCase), "Only USD orders are imported.")
            .TranslateWith(static order => new CommerceOrder(
                order.OrderNumber.Trim(),
                order.GrossAmount,
                order.CustomerCode.Trim().ToUpperInvariant()))
            .RequireDomain(static order => order.TotalUsd > 0m, "Imported order total must be positive.")
            .RequireDomain(static order => order.CustomerId.Length > 0, "Imported order must have a customer.")
            .Build();
}

[GenerateAntiCorruptionLayer(
    typeof(LegacyOrderDto),
    typeof(CommerceOrder),
    FactoryMethodName = "CreateGeneratedLayer",
    LayerName = "legacy-order-import",
    SourceSystem = "legacy-erp")]
public static partial class GeneratedLegacyOrderAntiCorruptionLayer
{
    [AntiCorruptionTranslator]
    private static CommerceOrder Translate(LegacyOrderDto order)
        => new(
            order.OrderNumber.Trim(),
            order.GrossAmount,
            order.CustomerCode.Trim().ToUpperInvariant());

    [AntiCorruptionExternalRule("Legacy order number is required.")]
    private static bool HasOrderNumber(LegacyOrderDto order)
        => !string.IsNullOrWhiteSpace(order.OrderNumber);

    [AntiCorruptionExternalRule("Only USD orders are imported.")]
    private static bool IsUsd(LegacyOrderDto order)
        => string.Equals(order.CurrencyCode, "USD", StringComparison.OrdinalIgnoreCase);

    [AntiCorruptionDomainRule("Imported order total must be positive.")]
    private static bool HasPositiveTotal(CommerceOrder order)
        => order.TotalUsd > 0m;

    [AntiCorruptionDomainRule("Imported order must have a customer.")]
    private static bool HasCustomer(CommerceOrder order)
        => order.CustomerId.Length > 0;
}

public static class LegacyOrderAntiCorruptionDemoServiceCollectionExtensions
{
    public static IServiceCollection AddLegacyOrderAntiCorruptionDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedLegacyOrderAntiCorruptionLayer.CreateGeneratedLayer());
        services.AddSingleton<ILegacyOrderFeed, ScriptedLegacyOrderFeed>();
        services.AddSingleton<LegacyOrderImportService>();
        return services;
    }
}
