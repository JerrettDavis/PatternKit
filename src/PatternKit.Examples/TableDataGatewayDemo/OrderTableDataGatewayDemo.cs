using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.TableDataGateway;
using PatternKit.Generators.TableDataGateway;

namespace PatternKit.Examples.TableDataGatewayDemo;

public static class OrderTableDataGatewayDemo
{
    public static async ValueTask<OrderTableGatewaySummary> RunFluentAsync()
    {
        var gateway = OrderTableGatewayPolicies.CreateFluentGateway();
        return await RunScenarioAsync(gateway, "order-100");
    }

    public static async ValueTask<OrderTableGatewaySummary> RunGeneratedAsync()
        => await RunScenarioAsync(GeneratedOrderTableGateway.CreateGateway(), "order-200");

    private static async ValueTask<OrderTableGatewaySummary> RunScenarioAsync(ITableDataGateway<OrderTableRow, string> gateway, string orderId)
    {
        _ = await gateway.InsertAsync(new OrderTableRow(orderId, "customer-1", "Pending", 125m));
        _ = await gateway.UpdateAsync(new OrderTableRow(orderId, "customer-1", "Closed", 125m));
        var closed = await gateway.QueryAsync(static row => row.Status == "Closed");
        return new(gateway.TableName, closed.Count, ScenarioExpectId(closed));
    }

    private static string ScenarioExpectId(IReadOnlyList<OrderTableRow> rows)
        => rows.Count == 0 ? "" : rows[0].OrderId;
}

public sealed record OrderTableRow(string OrderId, string CustomerId, string Status, decimal Total);

public sealed record OrderTableGatewaySummary(string TableName, int ClosedOrderCount, string FirstClosedOrderId);

public static class OrderTableGatewayPolicies
{
    public static InMemoryTableDataGateway<OrderTableRow, string> CreateFluentGateway()
        => InMemoryTableDataGateway<OrderTableRow, string>.Create("orders", static row => row.OrderId).Build();
}

public sealed class OrderTableGatewayWorkflow
{
    private readonly ITableDataGateway<OrderTableRow, string> _gateway;

    public OrderTableGatewayWorkflow(ITableDataGateway<OrderTableRow, string> gateway)
    {
        _gateway = gateway;
    }

    public async ValueTask<OrderTableGatewaySummary> CloseAsync(string orderId, string customerId, decimal total, CancellationToken cancellationToken = default)
    {
        _ = await _gateway.InsertAsync(new OrderTableRow(orderId, customerId, "Pending", total), cancellationToken).ConfigureAwait(false);
        _ = await _gateway.UpdateAsync(new OrderTableRow(orderId, customerId, "Closed", total), cancellationToken).ConfigureAwait(false);
        var closed = await _gateway.QueryAsync(static row => row.Status == "Closed", cancellationToken).ConfigureAwait(false);
        return new(_gateway.TableName, closed.Count, closed.Count == 0 ? "" : closed[0].OrderId);
    }
}

public sealed record OrderTableDataGatewayDemoRunner(
    Func<ValueTask<OrderTableGatewaySummary>> RunFluentAsync,
    Func<ValueTask<OrderTableGatewaySummary>> RunGeneratedAsync);

public static class OrderTableDataGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddOrderTableDataGatewayDemo(this IServiceCollection services)
    {
        services.AddScoped<ITableDataGateway<OrderTableRow, string>>(_ => OrderTableGatewayPolicies.CreateFluentGateway());
        services.AddScoped<OrderTableGatewayWorkflow>();
        services.AddSingleton(new OrderTableDataGatewayDemoRunner(
            OrderTableDataGatewayDemo.RunFluentAsync,
            OrderTableDataGatewayDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateTableDataGateway(typeof(OrderTableRow), typeof(string), FactoryName = "CreateGateway", TableName = "orders")]
public static partial class GeneratedOrderTableGateway
{
    [TableGatewayKeySelector]
    private static string SelectKey(OrderTableRow row) => row.OrderId;
}
