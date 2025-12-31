# Chain of Responsibility Pattern Guide

This guide covers everything you need to know about using the Chain of Responsibility pattern in PatternKit.

## Overview

Chain of Responsibility creates a pipeline of handlers that process requests in sequence. Each handler can either handle the request, pass it to the next handler, or do both. This pattern is the foundation of middleware systems like ASP.NET Core's request pipeline.

## Getting Started

### Installation

The Chain pattern is included in the core PatternKit package:

```csharp
using PatternKit.Behavioral.Chain;
```

### Basic Usage - ActionChain

Use `ActionChain` when you need side effects without returning a value:

```csharp
var chain = ActionChain<HttpRequest>.Create()
    // Log every request
    .When(r => r.Headers.ContainsKey("X-Request-Id"))
        .ThenContinue(r => Console.WriteLine($"Request: {r.Headers["X-Request-Id"]}"))

    // Block unauthorized admin access
    .When(r => r.Path.StartsWith("/admin") && !r.IsAuthenticated)
        .ThenStop(r => r.Respond(401, "Unauthorized"))

    // Terminal handler - runs if chain wasn't stopped
    .Finally((in r, next) =>
    {
        ProcessRequest(r);
        next(in r);
    })
    .Build();

chain.Execute(request);
```

### Basic Usage - ResultChain

Use `ResultChain` when handlers need to produce a result:

```csharp
var router = ResultChain<Request, Response>.Create()
    .When(r => r.Method == "GET" && r.Path == "/health")
        .Then(r => new Response(200, "OK"))

    .When(r => r.Path.StartsWith("/api/users/"))
        .Then(r => HandleUserRequest(r))

    .Finally((in r, out Response? res, _) =>
    {
        res = new Response(404, "Not Found");
        return true;
    })
    .Build();

if (router.Execute(in request, out var response))
{
    SendResponse(response!);
}
```

## Core Concepts

### Handler Flow Control

Handlers control chain execution through continuation:

```csharp
// ThenContinue: Execute action and always continue
.When(predicate).ThenContinue(action)

// ThenStop: Execute action and stop the chain
.When(predicate).ThenStop(action)

// Use: Full control via next delegate
.Use((in ctx, next) =>
{
    // Pre-processing
    DoSomething(ctx);

    // Decision: continue or stop
    if (ShouldContinue(ctx))
        next(in ctx);
    // else: chain stops here
})
```

### The `in` Parameter

ActionChain uses `in` parameters for performance with value types:

```csharp
// Predicates use `in` for zero-copy access
.When((in r) => r.Path.StartsWith("/admin"))

// Use static lambdas to avoid captures
.When(static (in r) => r.Flag)
```

### Finally Handler

The `Finally` handler runs only if the chain reaches the tail:

```csharp
var chain = ActionChain<Request>.Create()
    .When(r => r.IsBlocked)
        .ThenStop(r => Log("Blocked"))  // Finally won't run

    .When(r => r.IsSpecial)
        .ThenContinue(r => Log("Special"))  // Finally will run

    .Finally((in r, next) =>
    {
        Log("Processing normal request");
        next(in r);
    })
    .Build();
```

## Async Chains

Use async variants for I/O operations:

### AsyncActionChain

```csharp
var chain = AsyncActionChain<Request>.Create()
    .When(r => r.RequiresAuth)
        .ThenStop(async (r, ct) =>
        {
            var isValid = await authService.ValidateAsync(r.Token, ct);
            if (!isValid)
                r.Respond(401, "Invalid token");
        })

    .Finally(async (r, ct) =>
    {
        await processService.HandleAsync(r, ct);
    })
    .Build();

await chain.ExecuteAsync(request, cancellationToken);
```

### AsyncResultChain

```csharp
var router = AsyncResultChain<Request, Response>.Create()
    .When(r => r.Path == "/health")
        .Then(async (r, ct) => new Response(200, "OK"))

    .When(r => r.Path.StartsWith("/users/"))
        .Then(async (r, ct) =>
        {
            var user = await userService.GetAsync(r.UserId, ct);
            return new Response(200, JsonSerializer.Serialize(user));
        })

    .Finally(async (r, ct) => new Response(404, "Not Found"))
    .Build();

var (success, response) = await router.ExecuteAsync(request, ct);
```

## Common Patterns

### Request Validation Pipeline

```csharp
var validator = ActionChain<OrderRequest>.Create()
    .When(r => r.Items.Count == 0)
        .ThenStop(r => r.AddError("Order must have items"))

    .When(r => r.Items.Any(i => i.Quantity <= 0))
        .ThenStop(r => r.AddError("Invalid quantity"))

    .When(r => r.CustomerId == null)
        .ThenStop(r => r.AddError("Customer required"))

    .When(r => r.TotalAmount > 10000 && !r.IsApproved)
        .ThenStop(r => r.AddError("Large orders require approval"))

    .Finally((in r, next) =>
    {
        r.MarkValidated();
        next(in r);
    })
    .Build();
```

### Multi-Stage Processing

```csharp
var processor = ActionChain<Transaction>.Create()
    // Stage 1: Compute subtotal
    .Use((in t, next) =>
    {
        t.ComputeSubtotal();
        next(in t);
    })

    // Stage 2: Apply discounts
    .When(t => t.HasLoyaltyCard)
        .ThenContinue(t => t.ApplyDiscount(0.05m))

    .When(t => t.Total > 100)
        .ThenContinue(t => t.ApplyDiscount(0.02m))

    // Stage 3: Compute tax
    .Finally((in t, next) =>
    {
        t.ComputeTax();
        next(in t);
    })
    .Build();
```

### API Router

```csharp
var router = ResultChain<HttpRequest, HttpResponse>.Create()
    // Static routes
    .When(r => r.IsGet("/"))
        .Then(r => Html("index.html"))

    .When(r => r.IsGet("/health"))
        .Then(r => Ok("healthy"))

    // Dynamic routes
    .When(r => r.IsGet("/users/{id}"))
        .Then(r => GetUser(r.RouteParam("id")))

    .When(r => r.IsPost("/users"))
        .Then(r => CreateUser(r.Body<CreateUserDto>()))

    // Fallback
    .Finally((in r, out var res, _) =>
    {
        res = NotFound();
        return true;
    })
    .Build();
```

### Cross-Cutting Concerns

```csharp
var pipeline = AsyncActionChain<ApiRequest>.Create()
    // Logging
    .Use(async (r, ct, next) =>
    {
        var start = Stopwatch.GetTimestamp();
        await next(r, ct);
        Log($"Request took {Stopwatch.GetElapsedTime(start)}");
    })

    // Exception handling
    .Use(async (r, ct, next) =>
    {
        try
        {
            await next(r, ct);
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            r.SetError(500, "Internal error");
        }
    })

    // Actual processing
    .Finally(async (r, ct) =>
    {
        await ProcessAsync(r, ct);
    })
    .Build();
```

## Combining with Other Patterns

### With Strategy

Use Strategy inside chain handlers:

```csharp
var responseFormatter = Strategy<(Response, string), string>.Create()
    .When((r, format) => format == "json")
        .Then((r, _) => JsonSerializer.Serialize(r))
    .When((r, format) => format == "xml")
        .Then((r, _) => XmlSerializer.Serialize(r))
    .Default((r, _) => r.ToString())
    .Build();

var chain = ResultChain<Request, string>.Create()
    .When(r => r.Path == "/data")
        .Then(r =>
        {
            var data = GetData();
            var format = r.Headers.GetValueOrDefault("Accept", "json");
            return responseFormatter.Execute((data, format));
        })
    .Build();
```

### With TypeDispatcher

Route by type within a chain:

```csharp
var dispatcher = TypeDispatcher<Command, Result>.Create()
    .On<CreateCommand>(c => HandleCreate(c))
    .On<UpdateCommand>(c => HandleUpdate(c))
    .On<DeleteCommand>(c => HandleDelete(c))
    .Build();

var pipeline = ActionChain<CommandContext>.Create()
    .When(c => !c.IsAuthorized)
        .ThenStop(c => c.Fail("Unauthorized"))
    .Finally((in c, next) =>
    {
        c.Result = dispatcher.Dispatch(c.Command);
        next(in c);
    })
    .Build();
```

## Performance Tips

1. **Use `in` parameters**: Avoids copying for value types
2. **Use `static` lambdas**: Prevents closure allocations
3. **Cache chains**: Build once, execute many times
4. **Avoid captures**: Copy locals if needed in handlers

```csharp
// Good: static lambda, no captures
.When(static (in r) => r.Flag)
.ThenContinue(static r => Log(r))

// Avoid: captures outer variable
var threshold = 100;
.When((in r) => r.Amount > threshold) // Allocates closure
```

## Troubleshooting

### Finally never runs

Earlier handlers are stopping the chain:

```csharp
// Problem: ThenStop prevents Finally from running
.When(predicate).ThenStop(action)

// Solution: Use ThenContinue if you want Finally to run
.When(predicate).ThenContinue(action)
```

### Result is always default

No handler produced a result:

```csharp
// Problem: No Finally, so unmatched requests return false
var chain = ResultChain<int, string>.Create()
    .When(x => x > 0).Then(x => "positive")
    .Build();

// Solution: Add a Finally handler
.Finally((in x, out string? r, _) => { r = "unknown"; return true; })
```

## Best Practices

1. **Order matters**: Place most specific/frequent handlers first
2. **Provide Finally**: Always handle the "no match" case
3. **Keep handlers focused**: Each handler should do one thing
4. **Use ThenStop sparingly**: Be explicit about when chains end
5. **Log strategically**: Use chain handlers for cross-cutting logging

## FAQ

**Q: Can I modify the chain after building?**
A: No. Chains are immutable after `Build()`. Create a new chain if needed.

**Q: What's the difference between Use and When?**
A: `Use` adds unconditional handlers. `When` adds conditional handlers with `ThenContinue`/`ThenStop`.

**Q: How does this differ from middleware?**
A: It's the same concept! PatternKit chains are a middleware pattern with a fluent builder API.

**Q: Can I nest chains?**
A: Yes. A handler can execute another chain internally.
