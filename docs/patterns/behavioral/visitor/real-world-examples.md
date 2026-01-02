# Visitor Pattern Real-World Examples

Production-ready examples demonstrating the Visitor pattern in real-world scenarios.

---

## Example 1: Receipt Line Formatter

### The Problem

A point-of-sale system needs to format different tender types (cash, card, gift card, store credit) into receipt lines with consistent formatting but type-specific details.

### The Solution

Use Visitor to dispatch formatting by tender type.

### The Code

```csharp
public abstract record Tender(decimal Amount);
public record Cash(decimal Amount) : Tender(Amount);
public record Card(decimal Amount, string Brand, string Last4) : Tender(Amount);
public record GiftCard(decimal Amount, string Code) : Tender(Amount);
public record StoreCredit(decimal Amount, string CustomerId) : Tender(Amount);

public static Visitor<Tender, string> CreateReceiptFormatter()
{
    return Visitor<Tender, string>.Create()
        .On<Cash>(t => $"Cash          {t.Amount,10:C}")
        .On<Card>(t => $"{t.Brand} ****{t.Last4}  {t.Amount,10:C}")
        .On<GiftCard>(t => $"Gift Card {t.Code,-6}  {t.Amount,10:C}")
        .On<StoreCredit>(t => $"Store Credit  {t.Amount,10:C}")
        .Default(t => $"Other         {t.Amount,10:C}")
        .Build();
}

// Usage
var formatter = CreateReceiptFormatter();
var tenders = new Tender[]
{
    new Cash(20.00m),
    new Card(50.00m, "Visa", "4242"),
    new GiftCard(25.00m, "ABC123")
};

foreach (var tender in tenders)
{
    Console.WriteLine(formatter.Visit(tender));
}
// Output:
// Cash                $20.00
// Visa ****4242       $50.00
// Gift Card ABC123    $25.00
```

### Why This Pattern

- **Type-specific formatting**: Each tender type has custom display logic
- **Centralized**: All formatting in one place
- **Extensible**: Add new tender types without modifying existing code

---

## Example 2: AST Expression Evaluator

### The Problem

A mathematical expression parser produces an AST that needs to be evaluated, pretty-printed, and simplified using different visitors.

### The Solution

Create multiple visitors for different operations on the same AST.

### The Code

```csharp
public abstract record Expr;
public record Num(double Value) : Expr;
public record Add(Expr Left, Expr Right) : Expr;
public record Mul(Expr Left, Expr Right) : Expr;
public record Neg(Expr Operand) : Expr;

// Evaluator visitor
public static Visitor<Expr, double> CreateEvaluator()
{
    Func<Expr, double> eval = null!;
    var visitor = Visitor<Expr, double>.Create()
        .On<Num>(n => n.Value)
        .On<Add>(a => eval(a.Left) + eval(a.Right))
        .On<Mul>(m => eval(m.Left) * eval(m.Right))
        .On<Neg>(n => -eval(n.Operand))
        .Build();

    eval = e => visitor.Visit(e);
    return visitor;
}

// Pretty printer visitor
public static Visitor<Expr, string> CreatePrinter()
{
    Func<Expr, string> print = null!;
    var visitor = Visitor<Expr, string>.Create()
        .On<Num>(n => n.Value.ToString())
        .On<Add>(a => $"({print(a.Left)} + {print(a.Right)})")
        .On<Mul>(m => $"({print(m.Left)} * {print(m.Right)})")
        .On<Neg>(n => $"-{print(n.Operand)}")
        .Build();

    print = e => visitor.Visit(e);
    return visitor;
}

// Usage
var expr = new Add(new Num(3), new Mul(new Num(4), new Num(5)));

var eval = CreateEvaluator();
var printer = CreatePrinter();

Console.WriteLine(printer.Visit(expr)); // (3 + (4 * 5))
Console.WriteLine(eval.Visit(expr));    // 23
```

### Why This Pattern

- **Multiple operations**: Evaluate, print, simplify - all separate visitors
- **Same structure**: All operate on the same AST
- **No AST modification**: Expression classes stay clean

---

## Example 3: API Error Mapper

### The Problem

An API needs to map various exception types to appropriate HTTP responses with consistent error formats.

### The Solution

Use Visitor to dispatch exception handling by type.

### The Code

```csharp
public record ProblemDetails(int Status, string Title, string Detail);

public static Visitor<Exception, ProblemDetails> CreateErrorMapper()
{
    return Visitor<Exception, ProblemDetails>.Create()
        .On<ValidationException>(ex => new ProblemDetails(
            400,
            "Validation Error",
            string.Join("; ", ex.Errors)))

        .On<NotFoundException>(ex => new ProblemDetails(
            404,
            "Not Found",
            ex.Message))

        .On<UnauthorizedException>(ex => new ProblemDetails(
            401,
            "Unauthorized",
            "Authentication required"))

        .On<ForbiddenException>(ex => new ProblemDetails(
            403,
            "Forbidden",
            "Insufficient permissions"))

        .On<ConflictException>(ex => new ProblemDetails(
            409,
            "Conflict",
            ex.Message))

        .Default(ex => new ProblemDetails(
            500,
            "Internal Server Error",
            "An unexpected error occurred"))
        .Build();
}

// Usage in middleware
public class ErrorHandlingMiddleware
{
    private static readonly Visitor<Exception, ProblemDetails> _mapper = CreateErrorMapper();

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var problem = _mapper.Visit(ex);
            context.Response.StatusCode = problem.Status;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
```

### Why This Pattern

- **Consistent mapping**: All exceptions handled uniformly
- **Type-specific responses**: Each exception type gets appropriate status code
- **Default fallback**: Unknown exceptions get 500

---

## Example 4: Domain Event Processor

### The Problem

A domain-driven system needs to process different event types with specific handlers while maintaining a clean event bus.

### The Solution

Use AsyncActionVisitor to dispatch events to appropriate handlers.

### The Code

```csharp
public abstract record DomainEvent(Guid Id, DateTime OccurredAt);
public record OrderPlaced(Guid Id, DateTime OccurredAt, string OrderId, decimal Total) : DomainEvent(Id, OccurredAt);
public record OrderShipped(Guid Id, DateTime OccurredAt, string OrderId, string TrackingNumber) : DomainEvent(Id, OccurredAt);
public record OrderDelivered(Guid Id, DateTime OccurredAt, string OrderId) : DomainEvent(Id, OccurredAt);
public record OrderCancelled(Guid Id, DateTime OccurredAt, string OrderId, string Reason) : DomainEvent(Id, OccurredAt);

public class EventProcessor
{
    private readonly AsyncActionVisitor<DomainEvent> _handler;

    public EventProcessor(
        IEmailService email,
        ISmsService sms,
        IAnalyticsService analytics)
    {
        _handler = AsyncActionVisitor<DomainEvent>.Create()
            .On<OrderPlaced>(async (e, ct) =>
            {
                await email.SendOrderConfirmationAsync(e.OrderId, ct);
                await analytics.TrackOrderAsync(e.OrderId, e.Total, ct);
            })

            .On<OrderShipped>(async (e, ct) =>
            {
                await email.SendShippingNotificationAsync(e.OrderId, e.TrackingNumber, ct);
                await sms.SendTrackingLinkAsync(e.OrderId, e.TrackingNumber, ct);
            })

            .On<OrderDelivered>(async (e, ct) =>
            {
                await email.SendDeliveryConfirmationAsync(e.OrderId, ct);
                await analytics.TrackDeliveryAsync(e.OrderId, ct);
            })

            .On<OrderCancelled>(async (e, ct) =>
            {
                await email.SendCancellationNoticeAsync(e.OrderId, e.Reason, ct);
            })

            .Default(async (e, ct) =>
            {
                // Log unhandled events
                Console.WriteLine($"Unhandled event: {e.GetType().Name}");
            })
            .Build();
    }

    public async Task ProcessAsync(DomainEvent @event, CancellationToken ct)
    {
        await _handler.VisitAsync(@event, ct);
    }
}

// Usage
var processor = new EventProcessor(emailService, smsService, analyticsService);

var events = eventStore.GetPendingEvents();
foreach (var @event in events)
{
    await processor.ProcessAsync(@event, cancellationToken);
}
```

### Why This Pattern

- **Event-specific logic**: Each event type has dedicated handling
- **Async support**: I/O operations handled naturally
- **Centralized processing**: All event routing in one place
- **DI-friendly**: Services injected into constructor

---

## Key Takeaways

1. **Order matters**: Register specific types before base types
2. **Provide defaults**: Handle unexpected types gracefully
3. **Use TryVisit**: When no-match is expected, avoid exceptions
4. **Async for I/O**: Use AsyncVisitor/AsyncActionVisitor for database/network calls
5. **Compose with DI**: Register built visitors as singletons

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
