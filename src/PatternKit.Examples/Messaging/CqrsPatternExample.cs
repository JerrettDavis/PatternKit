using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PatternKit.Behavioral.Mediator;
using PatternKit.Examples.Messaging.SourceGenerated;
using SourceGenerated = PatternKit.Examples.Messaging.SourceGenerated;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Production-shaped CQRS example showing fluent and source-generated PatternKit paths side by side.
/// </summary>
public static class CqrsPatternExample
{
    public static async ValueTask<CqrsSummary> RunFluentAsync(CancellationToken cancellationToken = default)
    {
        var log = new List<string>();
        var orders = new Dictionary<int, CqrsOrder>();
        var nextOrderId = 1000;

        var mediator = Mediator.Create()
            .Pre((in request, _) =>
            {
                log.Add($"pre:{request?.GetType().Name}");
                return ValueTask.CompletedTask;
            })
            .Post((in request, response, _) =>
            {
                log.Add($"post:{request?.GetType().Name}:{response?.GetType().Name ?? "void"}");
                return ValueTask.CompletedTask;
            })
            .Command<CreateCqrsOrder, CqrsOrder>((in command, _) =>
            {
                var order = new CqrsOrder(++nextOrderId, command.CustomerId, command.Lines, command.Lines.Sum(static line => line.Quantity * line.UnitPrice));
                orders[order.Id] = order;
                return new ValueTask<CqrsOrder>(order);
            })
            .Command<GetCqrsOrder, CqrsOrder?>((in query, _) =>
            {
                orders.TryGetValue(query.OrderId, out var order);
                return new ValueTask<CqrsOrder?>(order);
            })
            .Notification<CqrsOrderCreated>((in notification, _) =>
            {
                log.Add($"event:order-created:{notification.OrderId}");
                return ValueTask.CompletedTask;
            })
            .Build();

        var created = await mediator.Send<CreateCqrsOrder, CqrsOrder>(
            new CreateCqrsOrder("customer-1", [new CqrsLine("SKU-1", 2, 19.95m)]),
            cancellationToken);

        await mediator.Publish(new CqrsOrderCreated(created!.Id), cancellationToken);
        var readModel = await mediator.Send<GetCqrsOrder, CqrsOrder?>(new GetCqrsOrder(created.Id), cancellationToken);

        return new CqrsSummary(
            "fluent",
            created.Id,
            readModel?.Id == created.Id,
            created.Total,
            log.ToArray());
    }

    public static async ValueTask<CqrsSummary> RunSourceGeneratedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var dispatcher = services.GetRequiredService<ProductionDispatcher>();
        var customers = services.GetRequiredService<ICustomerRepository>();
        var orders = services.GetRequiredService<IOrderRepository>();
        var logger = services.GetRequiredService<SourceGenerated.ILogger>();

        var customer = await dispatcher.Send<CreateCustomerCommand, Customer>(
            new CreateCustomerCommand("Ada Lovelace", "ada@example.com", 5000m),
            cancellationToken);

        customers.Add(customer);
        await dispatcher.Publish(new CustomerCreatedEvent(customer.Id, customer.Name, customer.Email), cancellationToken);

        var order = await dispatcher.Send<PlaceOrderCommand, Order>(
            new PlaceOrderCommand(customer.Id, [new OrderItem(1, "Keyboard", 2, 50m)]),
            cancellationToken);

        orders.Add(order);
        await dispatcher.Publish(new OrderPlacedEvent(order.Id, order.CustomerId, order.Total), cancellationToken);

        var readModel = await dispatcher.Send<GetOrdersByCustomerQuery, List<Order>>(
            new GetOrdersByCustomerQuery(customer.Id),
            cancellationToken);

        return new CqrsSummary(
            "source-generated",
            order.Id,
            readModel.Count == 1 && readModel[0].Id == order.Id,
            order.Total,
            logger.GetLogs().ToArray());
    }

    public static IServiceCollection AddSourceGeneratedCqrsServices(this IServiceCollection services)
    {
        services.TryAddSingleton<SourceGenerated.ILogger, InMemoryLogger>();
        services.TryAddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
        services.TryAddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.TryAddSingleton<IProductRepository, InMemoryProductRepository>();

        return services
            .AddSourceGeneratedMediator()
            .AddHandlersFromAssembly(typeof(CreateCustomerHandler).Assembly);
    }
}

public sealed record CreateCqrsOrder(string CustomerId, IReadOnlyList<CqrsLine> Lines);

public sealed record GetCqrsOrder(int OrderId);

public sealed record CqrsOrderCreated(int OrderId);

public sealed record CqrsLine(string Sku, int Quantity, decimal UnitPrice);

public sealed record CqrsOrder(int Id, string CustomerId, IReadOnlyList<CqrsLine> Lines, decimal Total);

public sealed record CqrsSummary(string Path, int OrderId, bool QueryMatchedCommand, decimal Total, IReadOnlyList<string> Log);
