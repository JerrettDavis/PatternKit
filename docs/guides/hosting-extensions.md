# Hosting Extensions

`PatternKit.Hosting.Extensions` provides reusable `Microsoft.Extensions.DependencyInjection` registrations for PatternKit runtime primitives. Use it when an application wants PatternKit policies, queues, and stores as normal services without importing `PatternKit.Examples`.

Install the runtime and hosting package:

```bash
dotnet add package PatternKit.Core
dotnet add package PatternKit.Hosting.Extensions
```

Register messaging primitives:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Hosting.DependencyInjection;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;

var services = new ServiceCollection();

services
    .AddPatternKitMessageChannel<OrderCommand>(
        "orders",
        channel => channel.WithCapacity(100, MessageChannelBackpressurePolicy.Reject))
    .AddPatternKitMessageStore<OrderCommand>(
        "order-audit",
        store => store.IdentifyBy(static (message, _) => message.Payload.OrderId))
    .AddPatternKitGuaranteedDelivery<OrderCommand>(
        queue => queue
            .Name("orders-guaranteed-delivery")
            .LeaseDuration(TimeSpan.FromSeconds(30))
            .MaxDeliveryAttempts(5));

using var provider = services.BuildServiceProvider(validateScopes: true);
var channel = provider.GetRequiredService<MessageChannel<OrderCommand>>();

channel.Send(Message<OrderCommand>.Create(new("order-100", 125m)));

public sealed record OrderCommand(string OrderId, decimal Total);
```

Register cloud resilience policies:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Hosting.DependencyInjection;

var services = new ServiceCollection();

services
    .AddPatternKitRetryPolicy<ServiceReply>(
        "inventory-retry",
        retry => retry
            .WithMaxAttempts(3)
            .HandleResult(static reply => !reply.Available))
    .AddPatternKitCircuitBreakerPolicy<ServiceReply>(
        "inventory-breaker",
        breaker => breaker
            .WithFailureThreshold(2)
            .WithBreakDuration(TimeSpan.FromSeconds(20))
            .HandleResult(static reply => !reply.Available))
    .AddPatternKitBulkheadPolicy<ServiceReply>(
        "inventory-bulkhead",
        bulkhead => bulkhead
            .WithMaxConcurrency(8)
            .WithMaxQueueLength(32))
    .AddPatternKitRateLimitPolicy<ServiceReply>(
        "inventory-rate-limit",
        rateLimit => rateLimit
            .WithPermitLimit(60)
            .WithWindow(TimeSpan.FromMinutes(1)))
    .AddPatternKitQueueLoadLevelingPolicy<ServiceReply>(
        "inventory-leveling",
        queue => queue
            .WithMaxConcurrentWorkers(4)
            .WithMaxQueueLength(500))
    .AddPatternKitPriorityQueue<InventoryWork, int>(
        static work => work.Priority,
        "inventory-priority");

public sealed record ServiceReply(bool Available);
public sealed record InventoryWork(string Sku, int Priority);
```

All helpers accept a `ServiceLifetime`; singleton is the default because most PatternKit runtime primitives hold useful state such as queues, windows, counters, or circuit state.

```csharp
services.AddPatternKitMessageChannel<OrderCommand>(
    "scoped-orders",
    lifetime: ServiceLifetime.Scoped);
```

The package is intentionally separate from `PatternKit.Examples`. Example registrations remain useful for demos and documentation, while `PatternKit.Hosting.Extensions` is the production-oriented integration surface for existing ASP.NET Core, worker service, and generic host applications.
