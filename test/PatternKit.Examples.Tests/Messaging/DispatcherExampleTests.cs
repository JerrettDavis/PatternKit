using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Examples.Messaging.SourceGenerated;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class DispatcherExampleTests
{
    [Fact]
    public async Task DispatcherUsageExamples_RunWithoutThrowing()
    {
        await DispatcherUsageExamples.BasicCommandExample();
        await DispatcherUsageExamples.NotificationExample();
        await DispatcherUsageExamples.StreamExample();
        await DispatcherUsageExamples.PipelineExample();
    }

    [Fact]
    public async Task CommandHandlers_ReturnExpectedResponsesAndLogOperations()
    {
        var logger = new InMemoryLogger();
        var customerHandler = new CreateCustomerHandler(logger);
        var orderHandler = new PlaceOrderHandler(logger);
        var paymentHandler = new ProcessPaymentHandler(logger);

        var customer = await customerHandler.Handle(new CreateCustomerCommand("Ada", "ada@example.com", 1000m), default);
        var order = await orderHandler.Handle(
            new PlaceOrderCommand(customer.Id, [new OrderItem(1, "Keyboard", 2, 50m)]),
            default);
        var paymentSuccess = await paymentHandler.Handle(new ProcessPaymentCommand(order.Id, order.Total), default);

        Assert.Equal("Ada", customer.Name);
        Assert.Equal(100m, order.Total);
        Assert.True(paymentSuccess);
        Assert.Contains(logger.GetLogs(), log => log.Contains("Creating customer", StringComparison.Ordinal));
        Assert.Contains(logger.GetLogs(), log => log.Contains("Placing order", StringComparison.Ordinal));
        Assert.Contains(logger.GetLogs(), log => log.Contains("Processing payment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task QueryAndStreamHandlers_ReadFromRepositories()
    {
        var customers = new InMemoryCustomerRepository();
        var orders = new InMemoryOrderRepository();
        var products = new InMemoryProductRepository();
        customers.Add(new Customer(1, "Ada", "ada@example.com", 1000m));
        orders.Add(new Order(10, 1, [new OrderItem(1, "Mouse", 1, 25m)], 25m, OrderStatus.Pending));

        var customer = await new GetCustomerHandler(customers).Handle(new GetCustomerQuery(1), default);
        var customerOrders = await new GetOrdersByCustomerHandler(orders).Handle(new GetOrdersByCustomerQuery(1), default);
        var productResults = new List<ProductSearchResult>();
        await foreach (var product in new SearchProductsHandler(products).Handle(new SearchProductsQuery("o", 2), default))
        {
            productResults.Add(product);
        }

        Assert.NotNull(customer);
        Assert.Equal("Ada", customer.Name);
        Assert.Single(customerOrders);
        Assert.Equal(2, productResults.Count);
        Assert.All(productResults, product => Assert.Contains("o", product.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NotificationHandlers_LogMessages()
    {
        var logger = new InMemoryLogger();

        await new SendWelcomeEmailHandler(logger).Handle(new CustomerCreatedEvent(1, "Ada", "ada@example.com"), default);
        await new UpdateCustomerStatsHandler(logger).Handle(new CustomerCreatedEvent(1, "Ada", "ada@example.com"), default);
        await new NotifyInventoryHandler(logger).Handle(new OrderPlacedEvent(5, 1, 125m), default);
        await new SendOrderConfirmationHandler(logger).Handle(new OrderPlacedEvent(5, 1, 125m), default);
        await new RecordPaymentAuditHandler(logger).Handle(new PaymentProcessedEvent(5, 125m, true), default);

        Assert.Equal(5, logger.GetLogs().Count);
    }

    [Fact]
    public async Task PipelineBehaviors_InvokeNextAndLogCrossCuttingSteps()
    {
        var logger = new InMemoryLogger();
        var request = new CreateCustomerCommand("Ada", "ada@example.com", 1000m);
        var expected = new Customer(7, "Ada", "ada@example.com", 1000m);

        var logged = await new LoggingBehavior<CreateCustomerCommand, Customer>(logger)
            .Handle(request, default, () => ValueTask.FromResult(expected));
        var validated = await new ValidationBehavior<CreateCustomerCommand, Customer>(logger)
            .Handle(request, default, () => ValueTask.FromResult(expected));
        var measured = await new PerformanceBehavior<CreateCustomerCommand, Customer>(logger)
            .Handle(request, default, () => ValueTask.FromResult(expected));
        var transacted = await new TransactionBehavior<CreateCustomerCommand, Customer>(logger)
            .Handle(request, default, () => ValueTask.FromResult(expected));

        Assert.Same(expected, logged);
        Assert.Same(expected, validated);
        Assert.Same(expected, measured);
        Assert.Same(expected, transacted);
        Assert.Contains(logger.GetLogs(), log => log.Contains("[Logging]", StringComparison.Ordinal));
        Assert.Contains(logger.GetLogs(), log => log.Contains("[Validation]", StringComparison.Ordinal));
        Assert.Contains(logger.GetLogs(), log => log.Contains("[Performance]", StringComparison.Ordinal));
        Assert.Contains(logger.GetLogs(), log => log.Contains("[Transaction] Committing", StringComparison.Ordinal));
    }

    [Fact]
    public void ServiceCollectionExtensions_RegisterGeneratedDispatcherAndHandlers()
    {
        var services = new ServiceCollection()
            .AddSingleton<ILogger, InMemoryLogger>()
            .AddSingleton<ICustomerRepository, InMemoryCustomerRepository>()
            .AddSingleton<IOrderRepository, InMemoryOrderRepository>()
            .AddSingleton<IProductRepository, InMemoryProductRepository>()
            .AddSourceGeneratedMediator()
            .AddHandlersFromAssembly(typeof(CreateCustomerHandler).Assembly)
            .AddBehavior(typeof(LoggingBehavior<,>))
            .AddBehavior<CreateCustomerCommand, Customer, LoggingBehavior<CreateCustomerCommand, Customer>>();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ProductionDispatcher>());
        Assert.NotNull(provider.GetRequiredService<ICommandHandler<CreateCustomerCommand, Customer>>());
        Assert.NotEmpty(provider.GetServices<INotificationHandler<CustomerCreatedEvent>>());
        Assert.NotNull(provider.GetRequiredService<IStreamHandler<SearchProductsQuery, ProductSearchResult>>());
        Assert.NotNull(provider.GetRequiredService<IPipelineBehavior<CreateCustomerCommand, Customer>>());
    }
}
