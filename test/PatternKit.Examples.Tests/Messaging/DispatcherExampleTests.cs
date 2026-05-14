using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Examples.Messaging.SourceGenerated;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Source-generated dispatcher examples")]
[Collection(PatternKit.Examples.Tests.ConsoleTestCollection.Name)]
public sealed class DispatcherExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
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

    [Scenario("Comprehensive mediator demo runs a production-style CQRS and notification workflow")]
    [Fact]
    public async Task ComprehensiveMediatorDemo_RunAsync_CompletesEndToEnd()
    {
        await Given("a redirected console", CaptureConsole)
            .When("running the comprehensive mediator demo", async Task<string> (capture) =>
            {
                try
                {
                    await ComprehensiveMediatorDemo.RunAsync();
                    return capture.Output();
                }
                finally
                {
                    capture.Dispose();
                }
            })
            .Then("customer creation was dispatched", output => output.Contains("Create Customer", StringComparison.Ordinal))
            .And("order placement was dispatched", output => output.Contains("Place Order", StringComparison.Ordinal))
            .And("payment processing was dispatched", output => output.Contains("Process Payment", StringComparison.Ordinal))
            .And("streaming search returned products", output => output.Contains("Product:", StringComparison.Ordinal))
            .And("the demo reported logged operations", output => output.Contains("Total operations logged:", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Pipeline behaviors cover query bypass, validation failure, and rollback")]
    [Fact]
    public async Task PipelineBehaviors_CoverSadPaths()
    {
        await Given("pipeline behaviors with an in-memory logger", () =>
            {
                var logger = new InMemoryLogger();
                return new
                {
                    Logger = logger,
                    Validation = new ValidationBehavior<CreateCustomerCommand, Customer>(logger),
                    QueryTransaction = new TransactionBehavior<GetCustomerQuery, Customer?>(logger),
                    CommandTransaction = new TransactionBehavior<CreateCustomerCommand, Customer>(logger)
                };
            })
            .When("executing bypass, validation failure, and rollback paths",
                async Task<(InMemoryLogger Logger, Customer? queryResult, bool queryBypassedTransaction, ArgumentNullException? validationFailure, InvalidOperationException? rollbackFailure)> (harness) =>
            {
                var customer = new Customer(42, "Ada", "ada@example.com", 1000m);
                var queryResult = await harness.QueryTransaction.Handle(
                    new GetCustomerQuery(customer.Id),
                    default,
                    () => ValueTask.FromResult<Customer?>(customer));
                var queryBypassedTransaction = !harness.Logger.GetLogs()
                    .Any(log => log.Contains("Beginning transaction", StringComparison.Ordinal));

                ArgumentNullException? validationFailure = null;
                try
                {
                    await harness.Validation.Handle(null!, default, () => ValueTask.FromResult(customer));
                }
                catch (ArgumentNullException ex)
                {
                    validationFailure = ex;
                }

                InvalidOperationException? rollbackFailure = null;
                try
                {
                    await harness.CommandTransaction.Handle(
                        new CreateCustomerCommand("Grace", "grace@example.com", 2000m),
                        default,
                        () => throw new InvalidOperationException("handler failed"));
                }
                catch (InvalidOperationException ex)
                {
                    rollbackFailure = ex;
                }

                return (harness.Logger, queryResult, queryBypassedTransaction, validationFailure, rollbackFailure);
            })
            .Then("query requests bypass transaction logging", result =>
                result.queryResult?.Id == 42
                && result.queryBypassedTransaction)
            .And("validation rejects null requests", result => result.validationFailure is not null)
            .And("command failures are rolled back", result =>
                result.rollbackFailure is not null
                && result.Logger.GetLogs().Any(log => log.Contains("Rolling back transaction", StringComparison.Ordinal)))
            .AssertPassed();
    }

    [Scenario("Generated dispatcher reports missing commands and streams while empty notification publish is a no-op")]
    [Fact]
    public async Task GeneratedDispatcher_CoversMissingHandlersAndStreamLimits()
    {
        await Given("an empty dispatcher and a registered stream dispatcher", () =>
            {
                var empty = ProductionDispatcher.Create().Build();
                var products = new InMemoryProductRepository();
                var stream = ProductionDispatcher.Create()
                    .Stream<SearchProductsQuery, ProductSearchResult>((query, ct) =>
                        new SearchProductsHandler(products).Handle(query, ct))
                    .Build();

                return (empty, stream);
            })
            .When("sending unregistered messages and streaming limited search results",
                async Task<(InvalidOperationException? missingCommand, InvalidOperationException? missingNotification, InvalidOperationException? missingStream, List<ProductSearchResult> streamed)> (dispatchers) =>
            {
                InvalidOperationException? missingCommand = null;
                InvalidOperationException? missingNotification = null;
                InvalidOperationException? missingStream = null;

                try
                {
                    await dispatchers.empty.Send<CreateCustomerCommand, Customer>(
                        new CreateCustomerCommand("Ada", "ada@example.com", 1000m),
                        default);
                }
                catch (InvalidOperationException ex)
                {
                    missingCommand = ex;
                }

                try
                {
                    await dispatchers.empty.Publish(new CustomerCreatedEvent(1, "Ada", "ada@example.com"), default);
                }
                catch (InvalidOperationException ex)
                {
                    missingNotification = ex;
                }

                try
                {
                    await foreach (var _ in dispatchers.empty.Stream<SearchProductsQuery, ProductSearchResult>(
                                       new SearchProductsQuery("o", 1),
                                       default))
                    {
                    }
                }
                catch (InvalidOperationException ex)
                {
                    missingStream = ex;
                }

                var streamed = new List<ProductSearchResult>();
                await foreach (var product in dispatchers.stream.Stream<SearchProductsQuery, ProductSearchResult>(
                                   new SearchProductsQuery("o", 2),
                                   default))
                {
                    streamed.Add(product);
                }

                return (missingCommand, missingNotification, missingStream, streamed);
            })
            .Then("missing command handlers fail clearly", result => result.missingCommand is not null)
            .And("missing notification handlers are treated as a no-op", result => result.missingNotification is null)
            .And("missing stream handlers fail clearly", result => result.missingStream is not null)
            .And("registered stream handlers honor requested limits", result => result.streamed.Count == 2)
            .AssertPassed();
    }

    private static ConsoleCapture CaptureConsole() => new();

    private sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter _original = Console.Out;
        private readonly StringWriter _writer = new();

        public ConsoleCapture()
        {
            Console.SetOut(_writer);
        }

        public string Output() => _writer.ToString();

        public void Dispose()
        {
            Console.SetOut(_original);
            _writer.Dispose();
        }
    }
}
