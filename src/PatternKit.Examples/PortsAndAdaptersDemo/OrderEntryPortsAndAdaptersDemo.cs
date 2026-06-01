using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.PortsAndAdapters;
using PatternKit.Generators.PortsAndAdapters;

namespace PatternKit.Examples.PortsAndAdaptersDemo;

public sealed record OrderEntryHttpRequest(string OrderId, string CustomerEmail, decimal Total);
public sealed record PlaceOrderCommand(string OrderId, string CustomerEmail, decimal Total);
public sealed record PlaceOrderResult(string OrderId, bool Accepted, string Message);
public sealed record OrderEntryHttpResponse(int StatusCode, string OrderId, string Message);

public interface IOrderEntryApplicationPort
{
    ValueTask<PlaceOrderResult> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default);
}

public sealed class InMemoryOrderEntryApplicationPort : IOrderEntryApplicationPort
{
    private readonly List<PlaceOrderCommand> _accepted = [];

    public IReadOnlyList<PlaceOrderCommand> Accepted => _accepted;

    public ValueTask<PlaceOrderResult> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _accepted.Add(command);
        return new(new PlaceOrderResult(command.OrderId, true, "accepted"));
    }
}

public static class OrderEntryPortsAndAdaptersPolicies
{
    public static PortsAndAdaptersPipeline<OrderEntryHttpRequest, PlaceOrderCommand, PlaceOrderResult, OrderEntryHttpResponse> CreateFluent(IOrderEntryApplicationPort applicationPort)
    {
        if (applicationPort is null)
            throw new ArgumentNullException(nameof(applicationPort));

        return PortsAndAdaptersPipeline<OrderEntryHttpRequest, PlaceOrderCommand, PlaceOrderResult, OrderEntryHttpResponse>.Create("order-entry")
            .AdaptInboundWith(MapInbound)
            .HandleWith((command, cancellationToken) => applicationPort.PlaceOrderAsync(command, cancellationToken))
            .AdaptOutboundWith(MapOutbound)
            .Build();
    }

    public static PlaceOrderCommand MapInbound(OrderEntryHttpRequest request)
        => new(request.OrderId, request.CustomerEmail, request.Total);

    public static OrderEntryHttpResponse MapOutbound(PlaceOrderResult result)
        => new(result.Accepted ? 202 : 409, result.OrderId, result.Message);
}

public sealed class OrderEntryPortsAndAdaptersWorkflow(IPortsAndAdaptersPipeline<OrderEntryHttpRequest, PlaceOrderCommand, PlaceOrderResult, OrderEntryHttpResponse> pipeline)
{
    public ValueTask<OrderEntryHttpResponse> PlaceOrderAsync(OrderEntryHttpRequest request, CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(request, cancellationToken);
}

public static class OrderEntryPortsAndAdaptersServiceCollectionExtensions
{
    public static IServiceCollection AddOrderEntryPortsAndAdaptersDemo(this IServiceCollection services)
    {
        services.AddSingleton<IOrderEntryApplicationPort, InMemoryOrderEntryApplicationPort>();
        services.AddSingleton<IPortsAndAdaptersPipeline<OrderEntryHttpRequest, PlaceOrderCommand, PlaceOrderResult, OrderEntryHttpResponse>>(sp =>
            OrderEntryPortsAndAdaptersPolicies.CreateFluent(sp.GetRequiredService<IOrderEntryApplicationPort>()));
        services.AddSingleton<OrderEntryPortsAndAdaptersWorkflow>();
        return services;
    }
}

[GeneratePortsAndAdapters(
    typeof(OrderEntryHttpRequest),
    typeof(PlaceOrderCommand),
    typeof(PlaceOrderResult),
    typeof(OrderEntryHttpResponse),
    FactoryName = nameof(CreateGenerated),
    PipelineName = "order-entry")]
public static partial class GeneratedOrderEntryPortsAndAdapters
{
    public static IOrderEntryApplicationPort ApplicationPort { get; set; } = new InMemoryOrderEntryApplicationPort();

    [InboundAdapter]
    private static PlaceOrderCommand MapInbound(OrderEntryHttpRequest request)
        => OrderEntryPortsAndAdaptersPolicies.MapInbound(request);

    [ApplicationPort]
    private static ValueTask<PlaceOrderResult> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
        => ApplicationPort.PlaceOrderAsync(command, cancellationToken);

    [OutboundAdapter]
    private static OrderEntryHttpResponse MapOutbound(PlaceOrderResult result)
        => OrderEntryPortsAndAdaptersPolicies.MapOutbound(result);
}
