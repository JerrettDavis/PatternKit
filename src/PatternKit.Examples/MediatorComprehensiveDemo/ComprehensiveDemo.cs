using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// Generate the dispatcher for the comprehensive demo - use different name to avoid conflict with ExampleDispatcher
[assembly: GenerateDispatcher(
    Namespace = "PatternKit.Examples.Messaging.SourceGenerated",
    Name = "ProductionDispatcher",
    IncludeStreaming = true,
    Visibility = GeneratedVisibility.Public)]

namespace PatternKit.Examples.Messaging.SourceGenerated;

#region Core Abstractions

/// <summary>
/// Marker interface for commands that return a response.
/// </summary>
public interface ICommand<TResponse> { }

/// <summary>
/// Marker interface for queries (read-only commands).
/// </summary>
public interface IQuery<TResponse> : ICommand<TResponse> { }

/// <summary>
/// Marker interface for notifications/events.
/// </summary>
public interface INotification { }

/// <summary>
/// Marker interface for streaming requests.
/// </summary>
public interface IStreamRequest<TItem> { }

/// <summary>
/// Pipeline behavior for cross-cutting concerns.
/// </summary>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    ValueTask<TResponse> Handle(
        TRequest request, 
        CancellationToken ct, 
        RequestHandlerDelegate<TResponse> next);
}

public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();

#endregion

#region Domain Models

/// <summary>
/// Represents a customer in the system.
/// </summary>
public record Customer(int Id, string Name, string Email, decimal CreditLimit);

/// <summary>
/// Represents an order in the system.
/// </summary>
public record Order(int Id, int CustomerId, List<OrderItem> Items, decimal Total, OrderStatus Status);

public record OrderItem(int ProductId, string ProductName, int Quantity, decimal Price);

public enum OrderStatus { Pending, Processing, Completed, Cancelled }

#endregion

#region Commands & Queries

// Commands (write operations)
public record CreateCustomerCommand(string Name, string Email, decimal CreditLimit) : ICommand<Customer>;

public record PlaceOrderCommand(int CustomerId, List<OrderItem> Items) : ICommand<Order>;

public record ProcessPaymentCommand(int OrderId, decimal Amount) : ICommand<bool>;

// Queries (read operations)
public record GetCustomerQuery(int CustomerId) : IQuery<Customer?>;

public record GetOrdersByCustomerQuery(int CustomerId) : IQuery<List<Order>>;

public record SearchProductsQuery(string SearchTerm, int MaxResults) : IStreamRequest<ProductSearchResult>;

public record ProductSearchResult(int ProductId, string Name, decimal Price, int Stock);

#endregion

#region Events (Notifications)

public record CustomerCreatedEvent(int CustomerId, string Name, string Email) : INotification;

public record OrderPlacedEvent(int OrderId, int CustomerId, decimal Total) : INotification;

public record PaymentProcessedEvent(int OrderId, decimal Amount, bool Success) : INotification;

#endregion

#region Command Handlers

public class CreateCustomerHandler : global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<CreateCustomerCommand, Customer>
{
    private static int _nextId = 0;
    private readonly ILogger _logger;
    
    public CreateCustomerHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public ValueTask<Customer> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        _logger.Log($"Creating customer: {request.Name}");
        var id = System.Threading.Interlocked.Increment(ref _nextId);
        var customer = new Customer(id, request.Name, request.Email, request.CreditLimit);
        return new ValueTask<Customer>(customer);
    }
}

public class PlaceOrderHandler : global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<PlaceOrderCommand, Order>
{
    private static int _nextOrderId = 999;
    private readonly ILogger _logger;
    
    public PlaceOrderHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public ValueTask<Order> Handle(PlaceOrderCommand request, CancellationToken ct)
    {
        _logger.Log($"Placing order for customer {request.CustomerId}");
        var total = request.Items.Sum(i => i.Price * i.Quantity);
        var orderId = System.Threading.Interlocked.Increment(ref _nextOrderId);
        var order = new Order(orderId, request.CustomerId, request.Items, total, OrderStatus.Pending);
        return new ValueTask<Order>(order);
    }
}

public class ProcessPaymentHandler : global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<ProcessPaymentCommand, bool>
{
    private readonly ILogger _logger;
    
    public ProcessPaymentHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public async ValueTask<bool> Handle(ProcessPaymentCommand request, CancellationToken ct)
    {
        _logger.Log($"Processing payment for order {request.OrderId}: ${request.Amount}");
        await Task.Delay(100, ct); // Simulate payment gateway call
        return request.Amount > 0;
    }
}

#endregion

#region Query Handlers

public class GetCustomerHandler : global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<GetCustomerQuery, Customer?>
{
    private readonly ICustomerRepository _repository;
    
    public GetCustomerHandler(ICustomerRepository repository)
    {
        _repository = repository;
    }
    
    public ValueTask<Customer?> Handle(GetCustomerQuery request, CancellationToken ct)
    {
        var customer = _repository.GetById(request.CustomerId);
        return new ValueTask<Customer?>(customer);
    }
}

public class GetOrdersByCustomerHandler : global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<GetOrdersByCustomerQuery, List<Order>>
{
    private readonly IOrderRepository _repository;
    
    public GetOrdersByCustomerHandler(IOrderRepository repository)
    {
        _repository = repository;
    }
    
    public ValueTask<List<Order>> Handle(GetOrdersByCustomerQuery request, CancellationToken ct)
    {
        var orders = _repository.GetByCustomerId(request.CustomerId);
        return new ValueTask<List<Order>>(orders);
    }
}

#endregion

#region Stream Handlers

public class SearchProductsHandler : global::PatternKit.Examples.Messaging.SourceGenerated.IStreamHandler<SearchProductsQuery, ProductSearchResult>
{
    private readonly IProductRepository _repository;
    
    public SearchProductsHandler(IProductRepository repository)
    {
        _repository = repository;
    }
    
    public async IAsyncEnumerable<ProductSearchResult> Handle(
        SearchProductsQuery request, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        var products = _repository.Search(request.SearchTerm);
        var count = 0;
        
        foreach (var product in products)
        {
            if (count >= request.MaxResults)
                yield break;
                
            await Task.Delay(10, ct); // Simulate async processing
            yield return product;
            count++;
        }
    }
}

#endregion

#region Event Handlers

public class SendWelcomeEmailHandler : global::PatternKit.Examples.Messaging.SourceGenerated.INotificationHandler<CustomerCreatedEvent>
{
    private readonly ILogger _logger;
    
    public SendWelcomeEmailHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public ValueTask Handle(CustomerCreatedEvent notification, CancellationToken ct)
    {
        _logger.Log($"Sending welcome email to {notification.Email}");
        return ValueTask.CompletedTask;
    }
}

public class UpdateCustomerStatsHandler : global::PatternKit.Examples.Messaging.SourceGenerated.INotificationHandler<CustomerCreatedEvent>
{
    private readonly ILogger _logger;
    
    public UpdateCustomerStatsHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public ValueTask Handle(CustomerCreatedEvent notification, CancellationToken ct)
    {
        _logger.Log($"Updating customer statistics for {notification.CustomerId}");
        return ValueTask.CompletedTask;
    }
}

public class NotifyInventoryHandler : global::PatternKit.Examples.Messaging.SourceGenerated.INotificationHandler<OrderPlacedEvent>
{
    private readonly ILogger _logger;
    
    public NotifyInventoryHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public ValueTask Handle(OrderPlacedEvent notification, CancellationToken ct)
    {
        _logger.Log($"Notifying inventory system of order {notification.OrderId}");
        return ValueTask.CompletedTask;
    }
}

public class SendOrderConfirmationHandler : global::PatternKit.Examples.Messaging.SourceGenerated.INotificationHandler<OrderPlacedEvent>
{
    private readonly ILogger _logger;
    
    public SendOrderConfirmationHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public ValueTask Handle(OrderPlacedEvent notification, CancellationToken ct)
    {
        _logger.Log($"Sending order confirmation for order {notification.OrderId}");
        return ValueTask.CompletedTask;
    }
}

public class RecordPaymentAuditHandler : global::PatternKit.Examples.Messaging.SourceGenerated.INotificationHandler<PaymentProcessedEvent>
{
    private readonly ILogger _logger;
    
    public RecordPaymentAuditHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public ValueTask Handle(PaymentProcessedEvent notification, CancellationToken ct)
    {
        _logger.Log($"Recording payment audit: Order={notification.OrderId}, Amount=${notification.Amount}, Success={notification.Success}");
        return ValueTask.CompletedTask;
    }
}

#endregion

#region Pipeline Behaviors

/// <summary>
/// Logs all commands before and after execution.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly ILogger _logger;
    
    public LoggingBehavior(ILogger logger)
    {
        _logger = logger;
    }
    
    public async ValueTask<TResponse> Handle(
        TRequest request, 
        CancellationToken ct, 
        RequestHandlerDelegate<TResponse> next)
    {
        var requestName = typeof(TRequest).Name;
        _logger.Log($"[Logging] Handling {requestName}");
        
        var response = await next();
        
        _logger.Log($"[Logging] Handled {requestName}");
        return response;
    }
}

/// <summary>
/// Validates commands before execution.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly ILogger _logger;
    
    public ValidationBehavior(ILogger logger)
    {
        _logger = logger;
    }
    
    public async ValueTask<TResponse> Handle(
        TRequest request, 
        CancellationToken ct, 
        RequestHandlerDelegate<TResponse> next)
    {
        _logger.Log($"[Validation] Validating {typeof(TRequest).Name}");
        
        // Validation logic here
        if (request == null)
            throw new System.ArgumentNullException(nameof(request));
        
        return await next();
    }
}

/// <summary>
/// Tracks performance metrics for all commands.
/// </summary>
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly ILogger _logger;
    
    public PerformanceBehavior(ILogger logger)
    {
        _logger = logger;
    }
    
    public async ValueTask<TResponse> Handle(
        TRequest request, 
        CancellationToken ct, 
        RequestHandlerDelegate<TResponse> next)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        var response = await next();
        
        sw.Stop();
        _logger.Log($"[Performance] {typeof(TRequest).Name} executed in {sw.ElapsedMilliseconds}ms");
        
        return response;
    }
}

/// <summary>
/// Wraps command execution in a transaction (simulated).
/// </summary>
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly ILogger _logger;
    
    public TransactionBehavior(ILogger logger)
    {
        _logger = logger;
    }
    
    public async ValueTask<TResponse> Handle(
        TRequest request, 
        CancellationToken ct, 
        RequestHandlerDelegate<TResponse> next)
    {
        // Skip transaction for queries
        if (request is IQuery<TResponse>)
            return await next();
        
        _logger.Log("[Transaction] Beginning transaction");
        
        try
        {
            var response = await next();
            _logger.Log("[Transaction] Committing transaction");
            return response;
        }
        catch
        {
            _logger.Log("[Transaction] Rolling back transaction");
            throw;
        }
    }
}

#endregion

#region Infrastructure

/// <summary>
/// Simple logger for demo purposes.
/// </summary>
public interface ILogger
{
    void Log(string message);
    List<string> GetLogs();
}

public class InMemoryLogger : ILogger
{
    private readonly List<string> _logs = new();
    
    public void Log(string message)
    {
        _logs.Add(message);
        System.Console.WriteLine($"  {message}");
    }
    
    public List<string> GetLogs() => _logs;
}

/// <summary>
/// Repository interfaces for demo.
/// </summary>
public interface ICustomerRepository
{
    Customer? GetById(int id);
    void Add(Customer customer);
}

public interface IOrderRepository
{
    List<Order> GetByCustomerId(int customerId);
    void Add(Order order);
}

public interface IProductRepository
{
    IEnumerable<ProductSearchResult> Search(string term);
}

// In-memory implementations
public class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<int, Customer> _customers = new();
    
    public Customer? GetById(int id) => _customers.GetValueOrDefault(id);
    public void Add(Customer customer) => _customers[customer.Id] = customer;
}

public class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = new();
    
    public List<Order> GetByCustomerId(int customerId) => 
        _orders.Where(o => o.CustomerId == customerId).ToList();
        
    public void Add(Order order) => _orders.Add(order);
}

public class InMemoryProductRepository : IProductRepository
{
    private readonly List<ProductSearchResult> _products = new()
    {
        new(1, "Laptop", 999.99m, 50),
        new(2, "Mouse", 29.99m, 200),
        new(3, "Keyboard", 79.99m, 150),
        new(4, "Monitor", 299.99m, 75),
        new(5, "Headphones", 149.99m, 100),
    };
    
    public IEnumerable<ProductSearchResult> Search(string term) =>
        _products.Where(p => p.Name.Contains(term, System.StringComparison.OrdinalIgnoreCase));
}

#endregion

#region Service Collection Extensions

/// <summary>
/// Extension methods for registering the mediator with dependency injection.
/// </summary>
public static class MediatorServiceCollectionExtensions
{
    /// <summary>
    /// Adds the source-generated mediator to the service collection.
    /// </summary>
    public static IServiceCollection AddSourceGeneratedMediator(
        this IServiceCollection services)
    {
        // Register the dispatcher factory
        services.AddSingleton(sp =>
        {
            var builder = ProductionDispatcher.Create();
            
            // Register commands
            builder.Command<CreateCustomerCommand, Customer>((req, ct) =>
                sp.GetRequiredService<global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<CreateCustomerCommand, Customer>>()
                    .Handle(req, ct));
                    
            builder.Command<PlaceOrderCommand, Order>((req, ct) =>
                sp.GetRequiredService<global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<PlaceOrderCommand, Order>>()
                    .Handle(req, ct));
                    
            builder.Command<ProcessPaymentCommand, bool>((req, ct) =>
                sp.GetRequiredService<global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<ProcessPaymentCommand, bool>>()
                    .Handle(req, ct));
            
            // Register queries
            builder.Command<GetCustomerQuery, Customer?>((req, ct) =>
                sp.GetRequiredService<global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<GetCustomerQuery, Customer?>>()
                    .Handle(req, ct));
                    
            builder.Command<GetOrdersByCustomerQuery, List<Order>>((req, ct) =>
                sp.GetRequiredService<global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<GetOrdersByCustomerQuery, List<Order>>>()
                    .Handle(req, ct));
            
            // Register streams
            builder.Stream<SearchProductsQuery, ProductSearchResult>((req, ct) =>
                sp.GetRequiredService<global::PatternKit.Examples.Messaging.SourceGenerated.IStreamHandler<SearchProductsQuery, ProductSearchResult>>()
                    .Handle(req, ct));
            
            // Register notifications
            builder.Notification<CustomerCreatedEvent>((evt, ct) =>
            {
                var handlers = sp.GetServices<global::PatternKit.Examples.Messaging.SourceGenerated.INotificationHandler<CustomerCreatedEvent>>();
                return FanoutAsync(handlers, evt, ct);
            });
            
            builder.Notification<OrderPlacedEvent>((evt, ct) =>
            {
                var handlers = sp.GetServices<global::PatternKit.Examples.Messaging.SourceGenerated.INotificationHandler<OrderPlacedEvent>>();
                return FanoutAsync(handlers, evt, ct);
            });
            
            builder.Notification<PaymentProcessedEvent>((evt, ct) =>
            {
                var handlers = sp.GetServices<global::PatternKit.Examples.Messaging.SourceGenerated.INotificationHandler<PaymentProcessedEvent>>();
                return FanoutAsync(handlers, evt, ct);
            });
            
            return builder.Build();
        });
        
        return services;
        
        static async ValueTask FanoutAsync<T>(
            IEnumerable<global::PatternKit.Examples.Messaging.SourceGenerated.INotificationHandler<T>> handlers, 
            T notification, 
            CancellationToken ct) where T : INotification
        {
            foreach (var handler in handlers)
                await handler.Handle(notification, ct);
        }
    }
    
    /// <summary>
    /// Adds command handlers from the specified assemblies.
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(
        this IServiceCollection services, 
        Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Select(t => new
            {
                Type = t,
                Interfaces = t.GetInterfaces()
            })
            .SelectMany(x => x.Interfaces.Select(i => new { HandlerType = x.Type, Interface = i }))
            .Where(x => x.Interface.IsGenericType)
            .ToList();
        
        var commandHandlerType = typeof(global::PatternKit.Examples.Messaging.SourceGenerated.ICommandHandler<,>);
        var notificationHandlerType = typeof(global::PatternKit.Examples.Messaging.SourceGenerated.INotificationHandler<>);
        var streamHandlerType = typeof(global::PatternKit.Examples.Messaging.SourceGenerated.IStreamHandler<,>);
        
        // Register command handlers
        foreach (var handler in handlerTypes.Where(x =>
            x.Interface.IsGenericType && x.Interface.GetGenericTypeDefinition() == commandHandlerType))
        {
            services.AddTransient(handler.Interface, handler.HandlerType);
        }
        
        // Register notification handlers
        foreach (var handler in handlerTypes.Where(x =>
            x.Interface.IsGenericType && x.Interface.GetGenericTypeDefinition() == notificationHandlerType))
        {
            services.AddTransient(handler.Interface, handler.HandlerType);
        }
        
        // Register stream handlers
        foreach (var handler in handlerTypes.Where(x =>
            x.Interface.IsGenericType && x.Interface.GetGenericTypeDefinition() == streamHandlerType))
        {
            services.AddTransient(handler.Interface, handler.HandlerType);
        }
        
        return services;
    }
    
    /// <summary>
    /// Adds a specific pipeline behavior.
    /// </summary>
    public static IServiceCollection AddBehavior(
        this IServiceCollection services,
        System.Type behaviorType)
    {
        services.AddTransient(behaviorType);
        return services;
    }
    
    /// <summary>
    /// Adds a specific pipeline behavior with generic type parameters.
    /// </summary>
    public static IServiceCollection AddBehavior<TRequest, TResponse, TBehavior>(
        this IServiceCollection services)
        where TRequest : ICommand<TResponse>
        where TBehavior : class, IPipelineBehavior<TRequest, TResponse>
    {
        services.AddTransient<IPipelineBehavior<TRequest, TResponse>, TBehavior>();
        return services;
    }
}

#endregion

#region Demo Runner

/// <summary>
/// Comprehensive production-ready demo showing MediatR-like patterns with source-generated dispatcher.
/// </summary>
public static class ComprehensiveMediatorDemo
{
    public static async Task RunAsync()
    {
        System.Console.WriteLine("=== Source-Generated Mediator - Comprehensive Production Demo ===\n");
        
        // Setup DI container
        var services = new ServiceCollection();
        
        // Register infrastructure
        services.AddSingleton<ILogger, InMemoryLogger>();
        services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.AddSingleton<IProductRepository, InMemoryProductRepository>();
        
        // Register mediator and handlers
        services.AddSourceGeneratedMediator();
        services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly());
        
        // Register behaviors
        services.AddTransient(typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(TransactionBehavior<,>));
        
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ProductionDispatcher>();
        var logger = provider.GetRequiredService<ILogger>();
        var customerRepo = provider.GetRequiredService<ICustomerRepository>();
        var orderRepo = provider.GetRequiredService<IOrderRepository>();
        
        // Demo 1: Create Customer (Command with Event)
        System.Console.WriteLine("\n--- Demo 1: Create Customer ---");
        var customer = await dispatcher.Send<CreateCustomerCommand, Customer>(
            new CreateCustomerCommand("John Doe", "john@example.com", 5000m), 
            default);
        
        customerRepo.Add(customer);
        
        await dispatcher.Publish(
            new CustomerCreatedEvent(customer.Id, customer.Name, customer.Email), 
            default);
        
        // Demo 2: Query Customer
        System.Console.WriteLine("\n--- Demo 2: Query Customer ---");
        var queriedCustomer = await dispatcher.Send<GetCustomerQuery, Customer?>(
            new GetCustomerQuery(customer.Id), 
            default);
        System.Console.WriteLine($"Found customer: {queriedCustomer?.Name}");
        
        // Demo 3: Place Order (Command with multiple events)
        System.Console.WriteLine("\n--- Demo 3: Place Order ---");
        var order = await dispatcher.Send<PlaceOrderCommand, Order>(
            new PlaceOrderCommand(customer.Id, new List<OrderItem>
            {
                new(1, "Laptop", 1, 999.99m),
                new(2, "Mouse", 2, 29.99m)
            }), 
            default);
        
        orderRepo.Add(order);
        
        await dispatcher.Publish(
            new OrderPlacedEvent(order.Id, order.CustomerId, order.Total), 
            default);
        
        // Demo 4: Process Payment
        System.Console.WriteLine("\n--- Demo 4: Process Payment ---");
        var paymentSuccess = await dispatcher.Send<ProcessPaymentCommand, bool>(
            new ProcessPaymentCommand(order.Id, order.Total), 
            default);
        
        await dispatcher.Publish(
            new PaymentProcessedEvent(order.Id, order.Total, paymentSuccess), 
            default);
        
        // Demo 5: Stream Search Results
        System.Console.WriteLine("\n--- Demo 5: Stream Product Search ---");
        await foreach (var product in dispatcher.Stream<SearchProductsQuery, ProductSearchResult>(
            new SearchProductsQuery("o", 3), 
            default))
        {
            System.Console.WriteLine($"  Product: {product.Name} - ${product.Price}");
        }
        
        // Demo 6: Query Orders
        System.Console.WriteLine("\n--- Demo 6: Query Customer Orders ---");
        var orders = await dispatcher.Send<GetOrdersByCustomerQuery, List<Order>>(
            new GetOrdersByCustomerQuery(customer.Id), 
            default);
        System.Console.WriteLine($"Customer has {orders.Count} order(s)");
        
        System.Console.WriteLine("\n=== Demo Complete ===");
        System.Console.WriteLine($"\nTotal operations logged: {logger.GetLogs().Count}");
    }
}

#endregion
