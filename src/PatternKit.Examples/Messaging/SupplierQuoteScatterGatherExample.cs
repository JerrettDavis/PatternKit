using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Examples.Messaging;

public sealed record SupplierQuoteRequest(string Sku, int Quantity, bool RequiresColdChain);

public sealed record SupplierQuote(string Supplier, decimal UnitPrice, bool Accepted);

public sealed record SupplierQuoteSummary(int AcceptedQuotes, string? BestSupplier, decimal? BestUnitPrice);

public sealed class SupplierQuoteService(ScatterGather<SupplierQuoteRequest, SupplierQuote, SupplierQuoteSummary> scatterGather)
{
    public SupplierQuoteSummary RequestQuotes(SupplierQuoteRequest request)
    {
        var result = scatterGather.Dispatch(Message<SupplierQuoteRequest>.Create(request).WithCorrelationId(request.Sku));
        return result.Result ?? new SupplierQuoteSummary(0, null, null);
    }
}

public static class SupplierQuoteScatterGathers
{
    public static ScatterGather<SupplierQuoteRequest, SupplierQuote, SupplierQuoteSummary> Create()
        => ScatterGather<SupplierQuoteRequest, SupplierQuote, SupplierQuoteSummary>.Create("supplier-quotes")
            .AddRecipient("regional", static (message, _) => ScatterGatherReply<SupplierQuote>.Success(new("regional", message.Payload.Quantity >= 100 ? 9.75m : 11.25m, true)))
            .AddRecipient("cold-chain", static (message, _) => ScatterGatherReply<SupplierQuote>.Success(new("cold-chain", 13.50m, true)), static (message, _) => message.Payload.RequiresColdChain)
            .AddRecipient("national", static (_, _) => ScatterGatherReply<SupplierQuote>.Success(new("national", 10.10m, true)))
            .AggregateWith(Aggregate)
            .Build();

    public static SupplierQuoteSummary Aggregate(
        IReadOnlyList<ScatterGatherReply<SupplierQuote>> replies,
        Message<SupplierQuoteRequest> message,
        MessageContext context)
    {
        var accepted = replies
            .Where(static reply => reply.Accepted && reply.Response is { Accepted: true })
            .Select(static reply => reply.Response!)
            .OrderBy(static quote => quote.UnitPrice)
            .ToArray();

        return accepted.Length == 0
            ? new SupplierQuoteSummary(0, null, null)
            : new SupplierQuoteSummary(accepted.Length, accepted[0].Supplier, accepted[0].UnitPrice);
    }
}

[GenerateScatterGather(typeof(SupplierQuoteRequest), typeof(SupplierQuote), typeof(SupplierQuoteSummary), FactoryName = "Create", Name = "supplier-quotes")]
public static partial class GeneratedSupplierQuoteScatterGather
{
    [ScatterGatherRecipient("regional", 10)]
    private static ScatterGatherReply<SupplierQuote> Regional(Message<SupplierQuoteRequest> message, MessageContext context)
        => ScatterGatherReply<SupplierQuote>.Success(new("regional", message.Payload.Quantity >= 100 ? 9.75m : 11.25m, true));

    [ScatterGatherRecipient("cold-chain", 20, "RequiresColdChain")]
    private static ScatterGatherReply<SupplierQuote> ColdChain(Message<SupplierQuoteRequest> message, MessageContext context)
        => ScatterGatherReply<SupplierQuote>.Success(new("cold-chain", 13.50m, true));

    [ScatterGatherRecipient("national", 30)]
    private static ScatterGatherReply<SupplierQuote> National(Message<SupplierQuoteRequest> message, MessageContext context)
        => ScatterGatherReply<SupplierQuote>.Success(new("national", 10.10m, true));

    private static bool RequiresColdChain(Message<SupplierQuoteRequest> message, MessageContext context)
        => message.Payload.RequiresColdChain;

    [ScatterGatherAggregator]
    private static SupplierQuoteSummary Aggregate(
        IReadOnlyList<ScatterGatherReply<SupplierQuote>> replies,
        Message<SupplierQuoteRequest> message,
        MessageContext context)
        => SupplierQuoteScatterGathers.Aggregate(replies, message, context);
}

public sealed class SupplierQuoteScatterGatherExampleRunner(SupplierQuoteService service)
{
    public SupplierQuoteSummary RunGenerated(SupplierQuoteRequest request) => service.RequestQuotes(request);

    public static SupplierQuoteSummary RunFluent(SupplierQuoteRequest request)
    {
        var result = SupplierQuoteScatterGathers.Create().Dispatch(Message<SupplierQuoteRequest>.Create(request));
        return result.Result ?? new SupplierQuoteSummary(0, null, null);
    }
}

public static class SupplierQuoteScatterGatherExampleServiceCollectionExtensions
{
    public static IServiceCollection AddSupplierQuoteScatterGatherDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedSupplierQuoteScatterGather.Create());
        services.AddSingleton<SupplierQuoteService>();
        services.AddSingleton<SupplierQuoteScatterGatherExampleRunner>();
        return services;
    }
}
