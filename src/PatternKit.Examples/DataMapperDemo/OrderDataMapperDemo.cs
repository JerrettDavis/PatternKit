using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.DataMapping;
using PatternKit.Application.Repository;
using PatternKit.Generators.DataMapping;

namespace PatternKit.Examples.DataMapperDemo;

/// <summary>Production-shaped Data Mapper example for keeping order domain models isolated from persistence rows.</summary>
public static class OrderDataMapperDemo
{
    public static async ValueTask<OrderDataMapperSummary> RunFluentAsync()
    {
        var mapper = OrderDataMapperPolicies.CreateFluentMapper();
        return await MapStoreAndLoadAsync(mapper);
    }

    public static async ValueTask<OrderDataMapperSummary> RunGeneratedAsync()
    {
        var mapper = GeneratedOrderDataMapper.CreateMapper();
        return await MapStoreAndLoadAsync(mapper);
    }

    internal static async ValueTask<OrderDataMapperSummary> MapStoreAndLoadAsync(IDataMapper<OrderAggregate, OrderRow> mapper)
    {
        var repository = InMemoryRepository<OrderRow, string>.Create(static row => row.OrderId).Build();
        var order = new OrderAggregate("order-100", "customer-1", 125m, OrderState.Paid);
        var data = await mapper.ToDataAsync(order);
        if (!data.Succeeded)
            return new(false, false, data.Errors.Count, string.Empty, string.Empty);

        _ = await repository.AddAsync(data.Value!);
        var loaded = await repository.GetAsync("order-100");
        var mapped = await mapper.ToDomainAsync(loaded!);

        return new(
            data.Succeeded,
            mapped.Succeeded,
            mapped.Errors.Count,
            data.Value!.PaymentStatus,
            mapped.Value?.CustomerId ?? string.Empty);
    }

    public static async ValueTask<OrderDataMapperSummary> RunValidationAsync()
    {
        var mapper = OrderDataMapperPolicies.CreateFluentMapper();
        var mapped = await mapper.ToDataAsync(new OrderAggregate("", "customer-1", 10m, OrderState.Pending));
        return new(mapped.Succeeded, false, mapped.Errors.Count, string.Empty, string.Empty);
    }
}

public sealed record OrderAggregate(string OrderId, string CustomerId, decimal Total, OrderState State);

public enum OrderState
{
    Pending,
    Paid
}

public sealed record OrderRow(string OrderId, string BuyerId, decimal TotalAmount, string PaymentStatus);

public sealed record OrderDataMapperSummary(
    bool DataMapped,
    bool DomainMapped,
    int ValidationErrors,
    string StoredStatus,
    string LoadedCustomerId);

public static class OrderDataMapperPolicies
{
    public static DataMapper<OrderAggregate, OrderRow> CreateFluentMapper()
        => DataMapper<OrderAggregate, OrderRow>.Create()
            .MapToData(static order => new OrderRow(order.OrderId, order.CustomerId, order.Total, ToStatus(order.State)))
            .MapToDomain(static row => new OrderAggregate(row.OrderId, row.BuyerId, row.TotalAmount, ToState(row.PaymentStatus)))
            .ValidateDomain(static order => string.IsNullOrWhiteSpace(order.OrderId)
                ? new DataMapperError("order-id-required", "Order id is required before persistence mapping.")
                : null)
            .ValidateData(static row => string.IsNullOrWhiteSpace(row.OrderId)
                ? new DataMapperError("order-id-required", "Order row id is required before domain mapping.")
                : null)
            .Build();

    private static string ToStatus(OrderState state)
        => state == OrderState.Paid ? "PAID" : "PENDING";

    private static OrderState ToState(string status)
        => string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) ? OrderState.Paid : OrderState.Pending;
}

public sealed class OrderDataMapperWorkflow
{
    private readonly IDataMapper<OrderAggregate, OrderRow> _mapper;

    public OrderDataMapperWorkflow(IDataMapper<OrderAggregate, OrderRow> mapper)
    {
        _mapper = mapper;
    }

    public ValueTask<OrderDataMapperSummary> RunAsync()
        => OrderDataMapperDemo.MapStoreAndLoadAsync(_mapper);
}

public sealed record OrderDataMapperDemoRunner(
    Func<ValueTask<OrderDataMapperSummary>> RunFluentAsync,
    Func<ValueTask<OrderDataMapperSummary>> RunGeneratedAsync);

public static class OrderDataMapperServiceCollectionExtensions
{
    public static IServiceCollection AddOrderDataMapperDemo(this IServiceCollection services)
    {
        services.AddSingleton<IDataMapper<OrderAggregate, OrderRow>>(_ => OrderDataMapperPolicies.CreateFluentMapper());
        services.AddSingleton<OrderDataMapperWorkflow>();
        services.AddSingleton(new OrderDataMapperDemoRunner(OrderDataMapperDemo.RunFluentAsync, OrderDataMapperDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateDataMapper(typeof(OrderAggregate), typeof(OrderRow), FactoryName = "CreateMapper")]
public static partial class GeneratedOrderDataMapper
{
    [DataMapperToData]
    private static OrderRow ToData(OrderAggregate order)
        => new(order.OrderId, order.CustomerId, order.Total, order.State == OrderState.Paid ? "PAID" : "PENDING");

    [DataMapperToDomain]
    private static OrderAggregate ToDomain(OrderRow row)
        => new(row.OrderId, row.BuyerId, row.TotalAmount, string.Equals(row.PaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase) ? OrderState.Paid : OrderState.Pending);
}
