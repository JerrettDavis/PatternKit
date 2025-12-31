# Mediator Pattern Real-World Examples

Production-ready examples demonstrating the Mediator pattern in real-world scenarios.

---

## Example 1: E-Commerce Order Processing

### The Problem

An e-commerce platform needs to handle order operations (create, update, cancel) with validation, logging, and notifications to multiple downstream systems.

### The Solution

Use Mediator with behaviors for cross-cutting concerns and notifications for event distribution.

### The Code

```csharp
// Messages
public record CreateOrder(int UserId, List<OrderItem> Items);
public record Order(int Id, int UserId, decimal Total, OrderStatus Status);
public record OrderCreated(int OrderId, int UserId, decimal Total);

public class OrderMediator
{
    private readonly Mediator _mediator;

    public OrderMediator(
        IOrderService orderService,
        IInventoryService inventory,
        IEmailService email,
        IAnalyticsService analytics,
        ILogger logger)
    {
        _mediator = Mediator.Create()
            // Behaviors
            .Pre((in object req, CancellationToken _) =>
            {
                logger.LogInformation("Processing {Type}", req.GetType().Name);
                return default;
            })
            .Whole(async (in object req, CancellationToken ct, Mediator.MediatorNext next) =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    return await next(in req, ct);
                }
                finally
                {
                    logger.LogInformation("{Type} completed in {Ms}ms",
                        req.GetType().Name, sw.ElapsedMilliseconds);
                }
            })

            // Commands
            .Command<CreateOrder, Order>(async (in CreateOrder cmd, CancellationToken ct) =>
            {
                // Validate inventory
                foreach (var item in cmd.Items)
                {
                    if (!await inventory.CheckAvailabilityAsync(item.ProductId, item.Quantity, ct))
                        throw new OutOfStockException(item.ProductId);
                }

                // Create order
                var order = await orderService.CreateAsync(cmd, ct);

                // Reserve inventory
                foreach (var item in cmd.Items)
                {
                    await inventory.ReserveAsync(item.ProductId, item.Quantity, ct);
                }

                // Publish notification
                await _mediator.Publish(new OrderCreated(order.Id, cmd.UserId, order.Total), ct);

                return order;
            })

            // Notifications
            .Notification<OrderCreated>(async (in OrderCreated n, CancellationToken ct) =>
            {
                await email.SendOrderConfirmationAsync(n.OrderId, ct);
            })
            .Notification<OrderCreated>(async (in OrderCreated n, CancellationToken ct) =>
            {
                await analytics.TrackAsync("order_created", new
                {
                    n.OrderId,
                    n.UserId,
                    n.Total
                }, ct);
            })

            .Build();
    }

    public Task<Order> CreateOrderAsync(CreateOrder command, CancellationToken ct = default)
        => _mediator.Send<CreateOrder, Order>(command, ct).AsTask();
}
```

### Why This Pattern

- **Separation of concerns**: Order creation, notifications, logging all decoupled
- **Extensible**: Add new notification handlers without modifying order logic
- **Observable**: Behaviors provide timing and logging

---

## Example 2: CQRS API Backend

### The Problem

A REST API needs to separate read and write operations with different validation, caching, and audit requirements.

### The Solution

Use Mediator to implement CQRS with specific behaviors for queries vs commands.

### The Code

```csharp
// Marker interfaces
public interface IQuery<TResult> { }
public interface ICommand<TResult> { }

// Queries
public record GetProduct(int Id) : IQuery<Product?>;
public record SearchProducts(string Query, int Page, int PageSize) : IQuery<PagedResult<Product>>;

// Commands
public record UpdateProduct(int Id, string Name, decimal Price) : ICommand<bool>;
public record DeleteProduct(int Id) : ICommand<bool>;

public class CqrsMediator
{
    private readonly Mediator _mediator;

    public CqrsMediator(
        IProductRepository repo,
        ICache cache,
        IAuditLog audit,
        ICurrentUser currentUser)
    {
        _mediator = Mediator.Create()
            // Query caching behavior
            .Whole(async (in object req, CancellationToken ct, Mediator.MediatorNext next) =>
            {
                if (req is IQuery<object> query)
                {
                    var cacheKey = $"query:{req.GetType().Name}:{req.GetHashCode()}";
                    if (cache.TryGet(cacheKey, out var cached))
                        return cached;

                    var result = await next(in req, ct);
                    cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
                    return result;
                }
                return await next(in req, ct);
            })

            // Command audit behavior
            .Post((in object req, object? res, CancellationToken ct) =>
            {
                if (req is ICommand<object>)
                {
                    audit.Log(new AuditEntry
                    {
                        UserId = currentUser.Id,
                        Action = req.GetType().Name,
                        Data = req,
                        Result = res,
                        Timestamp = DateTime.UtcNow
                    });
                }
                return default;
            })

            // Query handlers
            .Command<GetProduct, Product?>((in GetProduct q, CancellationToken ct) =>
                new ValueTask<Product?>(repo.GetById(q.Id)))

            .Command<SearchProducts, PagedResult<Product>>(
                async (in SearchProducts q, CancellationToken ct) =>
                    await repo.SearchAsync(q.Query, q.Page, q.PageSize, ct))

            // Command handlers
            .Command<UpdateProduct, bool>(async (in UpdateProduct cmd, CancellationToken ct) =>
            {
                var product = repo.GetById(cmd.Id);
                if (product == null) return false;

                product.Name = cmd.Name;
                product.Price = cmd.Price;
                await repo.SaveAsync(ct);

                // Invalidate cache
                cache.Remove($"query:{nameof(GetProduct)}:{cmd.Id.GetHashCode()}");
                return true;
            })

            .Build();
    }

    public ValueTask<TResult?> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken ct)
        where TQuery : IQuery<TResult>
        => _mediator.Send<TQuery, TResult>(query, ct);

    public ValueTask<TResult?> ExecuteAsync<TCommand, TResult>(TCommand command, CancellationToken ct)
        where TCommand : ICommand<TResult>
        => _mediator.Send<TCommand, TResult>(command, ct);
}

// Controller usage
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly CqrsMediator _mediator;

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> Get(int id)
    {
        var product = await _mediator.QueryAsync<GetProduct, Product?>(new GetProduct(id), HttpContext.RequestAborted);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, UpdateProductDto dto)
    {
        var success = await _mediator.ExecuteAsync<UpdateProduct, bool>(
            new UpdateProduct(id, dto.Name, dto.Price),
            HttpContext.RequestAborted);
        return success ? NoContent() : NotFound();
    }
}
```

### Why This Pattern

- **Clear separation**: Queries cached, commands audited
- **Consistent interface**: Same mediator for all operations
- **Cross-cutting**: Caching and auditing don't pollute handlers

---

## Example 3: Real-Time Dashboard with Streaming

### The Problem

A monitoring dashboard needs to stream metrics from multiple sources with aggregation and filtering.

### The Solution

Use Mediator streaming for live data feeds with behavior-based filtering.

### The Code

```csharp
// Stream requests
public record GetMetrics(string[] Sources, TimeSpan Window);
public record Metric(string Source, string Name, double Value, DateTime Timestamp);

public class DashboardMediator
{
    private readonly Mediator _mediator;

    public DashboardMediator(IMetricsCollector collector, IAlertService alerts)
    {
        _mediator = Mediator.Create()
            // Alerting behavior
            .Post((in object req, object? res, CancellationToken _) =>
            {
                if (res is BoxedAsyncEnumerable)
                {
                    // Stream completed, check aggregate alerts
                    alerts.CheckAggregateAlerts();
                }
                return default;
            })

            // Metrics stream
            .Stream<GetMetrics, Metric>(async (in GetMetrics q, CancellationToken ct) =>
            {
                var endTime = DateTime.UtcNow;
                var startTime = endTime - q.Window;

                await foreach (var metric in collector.StreamMetricsAsync(
                    q.Sources, startTime, endTime, ct))
                {
                    // Real-time alerting
                    if (metric.Value > GetThreshold(metric.Name))
                    {
                        await alerts.TriggerAsync(new MetricAlert
                        {
                            MetricName = metric.Name,
                            Value = metric.Value,
                            Threshold = GetThreshold(metric.Name)
                        }, ct);
                    }

                    yield return metric;
                }
            })

            .Build();
    }

    public IAsyncEnumerable<Metric> StreamMetricsAsync(
        string[] sources, TimeSpan window, CancellationToken ct)
        => _mediator.Stream<GetMetrics, Metric>(new GetMetrics(sources, window), ct);
}

// SignalR Hub usage
public class MetricsHub : Hub
{
    private readonly DashboardMediator _mediator;

    public async IAsyncEnumerable<MetricDto> StreamMetrics(
        string[] sources,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var metric in _mediator.StreamMetricsAsync(
            sources, TimeSpan.FromMinutes(5), ct))
        {
            yield return new MetricDto
            {
                Source = metric.Source,
                Name = metric.Name,
                Value = metric.Value,
                Timestamp = metric.Timestamp.ToString("O")
            };
        }
    }
}
```

### Why This Pattern

- **Streaming support**: Large datasets without memory pressure
- **Real-time processing**: Alerting during stream
- **Composable**: Behaviors wrap stream lifecycle

---

## Example 4: Multi-Tenant SaaS Platform

### The Problem

A SaaS platform needs to route requests to tenant-specific handlers with per-tenant rate limiting, feature flags, and audit logging.

### The Solution

Use Mediator with tenant-aware behaviors that intercept and route all operations.

### The Code

```csharp
public record TenantContext(string TenantId, string Plan, Dictionary<string, bool> Features);

public class TenantMediator
{
    private readonly Mediator _mediator;

    public TenantMediator(
        ITenantResolver tenantResolver,
        IRateLimiter rateLimiter,
        IFeatureFlags features,
        ITenantAudit audit)
    {
        _mediator = Mediator.Create()
            // Tenant resolution
            .Pre(async (in object req, CancellationToken ct) =>
            {
                var tenant = await tenantResolver.ResolveAsync(ct);
                if (tenant == null)
                    throw new UnauthorizedException("Tenant not found");

                // Store in async local for downstream use
                TenantContextHolder.Current = tenant;
            })

            // Rate limiting
            .Pre(async (in object req, CancellationToken ct) =>
            {
                var tenant = TenantContextHolder.Current!;
                var limit = GetRateLimit(tenant.Plan);

                if (!await rateLimiter.TryAcquireAsync(tenant.TenantId, limit, ct))
                    throw new RateLimitExceededException();
            })

            // Feature flag check
            .Pre((in object req, CancellationToken _) =>
            {
                var tenant = TenantContextHolder.Current!;
                var requiredFeature = GetRequiredFeature(req.GetType());

                if (requiredFeature != null &&
                    !tenant.Features.GetValueOrDefault(requiredFeature, false))
                {
                    throw new FeatureNotEnabledException(requiredFeature);
                }
                return default;
            })

            // Audit logging
            .Post((in object req, object? res, CancellationToken ct) =>
            {
                var tenant = TenantContextHolder.Current!;
                audit.LogAsync(new AuditEntry
                {
                    TenantId = tenant.TenantId,
                    Operation = req.GetType().Name,
                    Request = req,
                    Response = res,
                    Timestamp = DateTime.UtcNow
                }, ct);
                return default;
            })

            // Tenant-specific handlers
            .Command<GetTenantSettings, TenantSettings>(
                (in GetTenantSettings q, CancellationToken ct) =>
            {
                var tenant = TenantContextHolder.Current!;
                return settingsService.GetAsync(tenant.TenantId, ct);
            })

            .Command<UpdateTenantSettings, bool>(
                (in UpdateTenantSettings cmd, CancellationToken ct) =>
            {
                var tenant = TenantContextHolder.Current!;
                return settingsService.UpdateAsync(tenant.TenantId, cmd, ct);
            })

            .Build();
    }

    private static int GetRateLimit(string plan) => plan switch
    {
        "enterprise" => 10000,
        "professional" => 1000,
        _ => 100
    };

    private static string? GetRequiredFeature(Type requestType)
    {
        return requestType.GetCustomAttribute<RequiresFeatureAttribute>()?.Feature;
    }
}

[RequiresFeature("advanced-analytics")]
public record GetAdvancedAnalytics(DateTime From, DateTime To);
```

### Why This Pattern

- **Cross-cutting tenant logic**: Resolution, rate limiting, features in behaviors
- **Clean handlers**: Tenant context available, no manual resolution
- **Consistent audit**: All operations automatically logged

---

## Key Takeaways

1. **Behaviors for cross-cutting**: Logging, validation, caching in behaviors
2. **Commands for request/response**: Single handler, typed result
3. **Notifications for events**: Multiple handlers, fire-and-forget
4. **Streaming for large data**: Avoid memory pressure with async enumerable
5. **CQRS natural fit**: Separate query and command paths

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
