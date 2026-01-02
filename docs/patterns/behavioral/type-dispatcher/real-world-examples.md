# TypeDispatcher Pattern Real-World Examples

Production-ready examples demonstrating the TypeDispatcher pattern in real-world scenarios.

---

## Example 1: Payment Processing System

### The Problem

A payment processing system needs to handle different payment methods (credit cards, PayPal, crypto, bank transfers) with different fee structures, validation rules, and processing logic.

### The Solution

Use TypeDispatcher to route payments to type-specific handlers that calculate fees and process transactions.

### The Code

```csharp
// Payment type hierarchy
public abstract record Payment(decimal Amount, string CustomerId);
public record CashPayment(decimal Amount, string CustomerId) : Payment(Amount, CustomerId);
public record CardPayment(decimal Amount, string CustomerId, string CardNumber, string CVV) : Payment(Amount, CustomerId);
public record PayPalPayment(decimal Amount, string CustomerId, string PayPalEmail) : Payment(Amount, CustomerId);
public record CryptoPayment(decimal Amount, string CustomerId, string WalletAddress, string Currency) : Payment(Amount, CustomerId);
public record BankTransfer(decimal Amount, string CustomerId, string AccountNumber, string RoutingNumber) : Payment(Amount, CustomerId);

// Fee calculator
var feeCalculator = TypeDispatcher<Payment, decimal>.Create()
    .On<CashPayment>(_ => 0m)  // No fee for cash
    .On<CardPayment>(p => Math.Max(0.30m, p.Amount * 0.029m))  // 2.9% min $0.30
    .On<PayPalPayment>(p => p.Amount * 0.034m + 0.30m)  // 3.4% + $0.30
    .On<CryptoPayment>(p => p.Amount * 0.01m)  // 1% flat
    .On<BankTransfer>(p => p.Amount >= 1000 ? 0m : 5m)  // Free over $1000, else $5
    .Default(_ => throw new NotSupportedException("Unknown payment type"))
    .Build();

// Async processor
var processor = AsyncTypeDispatcher<Payment, TransactionResult>.Create()
    .On<CashPayment>(async (p, ct) =>
    {
        await RegisterCashSaleAsync(p, ct);
        return TransactionResult.Success(Guid.NewGuid().ToString());
    })
    .On<CardPayment>(async (p, ct) =>
    {
        var result = await stripeGateway.ChargeAsync(p.CardNumber, p.Amount, ct);
        return result.Success
            ? TransactionResult.Success(result.TransactionId)
            : TransactionResult.Failed(result.Error);
    })
    .On<PayPalPayment>(async (p, ct) =>
    {
        var result = await paypalGateway.CreatePaymentAsync(p.PayPalEmail, p.Amount, ct);
        return TransactionResult.Pending(result.ApprovalUrl);
    })
    .On<CryptoPayment>(async (p, ct) =>
    {
        var result = await cryptoGateway.InitiatePaymentAsync(p.WalletAddress, p.Amount, p.Currency, ct);
        return TransactionResult.Pending(result.PaymentAddress);
    })
    .On<BankTransfer>(async (p, ct) =>
    {
        var result = await achProcessor.InitiateTransferAsync(p.AccountNumber, p.RoutingNumber, p.Amount, ct);
        return TransactionResult.Pending(result.TransferReference);
    })
    .Build();

// Usage
public async Task<PaymentResponse> ProcessPaymentAsync(Payment payment, CancellationToken ct)
{
    var fee = feeCalculator.Dispatch(payment);
    var total = payment.Amount + fee;

    var result = await processor.DispatchAsync(payment, ct);

    return new PaymentResponse(result, total, fee);
}
```

### Why This Pattern

- **Clean separation**: Each payment type has isolated processing logic
- **Easy extension**: New payment methods are added without touching existing code
- **Fee flexibility**: Fee calculations can be changed per payment type independently
- **Type safety**: Handlers receive strongly-typed payment objects

---

## Example 2: Document Rendering

### The Problem

A content management system needs to render different document types (articles, galleries, videos, embedded content) into HTML with different templates and processing logic.

### The Solution

Use TypeDispatcher to select the appropriate renderer for each content type.

### The Code

```csharp
// Content type hierarchy
public abstract record Content(string Id, string Title);
public record Article(string Id, string Title, string Body, string Author) : Content(Id, Title);
public record Gallery(string Id, string Title, List<string> ImageUrls) : Content(Id, Title);
public record Video(string Id, string Title, string VideoUrl, int DurationSeconds) : Content(Id, Title);
public record Embed(string Id, string Title, string EmbedCode) : Content(Id, Title);
public record Podcast(string Id, string Title, string AudioUrl, string Description) : Content(Id, Title);

// HTML renderer
var renderer = TypeDispatcher<Content, string>.Create()
    .On<Article>(a => $"""
        <article>
            <h1>{HttpUtility.HtmlEncode(a.Title)}</h1>
            <p class="author">By {HttpUtility.HtmlEncode(a.Author)}</p>
            <div class="body">{a.Body}</div>
        </article>
        """)
    .On<Gallery>(g => $"""
        <div class="gallery">
            <h2>{HttpUtility.HtmlEncode(g.Title)}</h2>
            <div class="images">
                {string.Join("\n", g.ImageUrls.Select(url =>
                    $"<img src=\"{HttpUtility.HtmlAttributeEncode(url)}\" loading=\"lazy\" />"))}
            </div>
        </div>
        """)
    .On<Video>(v => $"""
        <div class="video-container">
            <h2>{HttpUtility.HtmlEncode(v.Title)}</h2>
            <video src="{HttpUtility.HtmlAttributeEncode(v.VideoUrl)}"
                   data-duration="{v.DurationSeconds}" controls></video>
        </div>
        """)
    .On<Embed>(e => $"""
        <div class="embed">
            <h2>{HttpUtility.HtmlEncode(e.Title)}</h2>
            <div class="embed-content">{e.EmbedCode}</div>
        </div>
        """)
    .On<Podcast>(p => $"""
        <div class="podcast">
            <h2>{HttpUtility.HtmlEncode(p.Title)}</h2>
            <p>{HttpUtility.HtmlEncode(p.Description)}</p>
            <audio src="{HttpUtility.HtmlAttributeEncode(p.AudioUrl)}" controls></audio>
        </div>
        """)
    .Default(c => $"<div class='unknown'>Unknown content: {HttpUtility.HtmlEncode(c.Title)}</div>")
    .Build();

// Usage
public string RenderPage(List<Content> contents)
{
    var html = new StringBuilder();
    html.AppendLine("<main>");

    foreach (var content in contents)
    {
        html.AppendLine(renderer.Dispatch(content));
    }

    html.AppendLine("</main>");
    return html.ToString();
}
```

### Why This Pattern

- **Template per type**: Each content type has its own rendering logic
- **HTML safety**: Type-specific handlers can apply appropriate escaping
- **Composable**: Render any mix of content types in a single page

---

## Example 3: Event Sourcing Handler

### The Problem

An event-sourced system needs to apply different domain events to aggregate state, with each event type modifying the state differently.

### The Solution

Use ActionTypeDispatcher to apply events to aggregate state.

### The Code

```csharp
// Domain events
public abstract record OrderEvent(Guid OrderId, DateTime OccurredAt);
public record OrderCreated(Guid OrderId, DateTime OccurredAt, string CustomerId, List<OrderLine> Lines)
    : OrderEvent(OrderId, OccurredAt);
public record OrderItemAdded(Guid OrderId, DateTime OccurredAt, string ProductId, int Quantity, decimal Price)
    : OrderEvent(OrderId, OccurredAt);
public record OrderItemRemoved(Guid OrderId, DateTime OccurredAt, string ProductId)
    : OrderEvent(OrderId, OccurredAt);
public record OrderShipped(Guid OrderId, DateTime OccurredAt, string TrackingNumber)
    : OrderEvent(OrderId, OccurredAt);
public record OrderDelivered(Guid OrderId, DateTime OccurredAt, DateTime DeliveredAt)
    : OrderEvent(OrderId, OccurredAt);
public record OrderCancelled(Guid OrderId, DateTime OccurredAt, string Reason)
    : OrderEvent(OrderId, OccurredAt);

// Order aggregate
public class OrderAggregate
{
    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = "";
    public OrderStatus Status { get; private set; }
    public List<OrderLine> Lines { get; private set; } = new();
    public string? TrackingNumber { get; private set; }
    public DateTime? DeliveredAt { get; private set; }

    private static readonly ActionTypeDispatcher<OrderEvent> _eventApplier;

    static OrderAggregate()
    {
        // Note: In real code, you'd use instance methods, not a static dispatcher
        // This is simplified for demonstration
    }

    // Create a per-instance event applier
    public ActionTypeDispatcher<OrderEvent> CreateApplier()
    {
        return ActionTypeDispatcher<OrderEvent>.Create()
            .On<OrderCreated>(e =>
            {
                Id = e.OrderId;
                CustomerId = e.CustomerId;
                Lines = new List<OrderLine>(e.Lines);
                Status = OrderStatus.Created;
            })
            .On<OrderItemAdded>(e =>
            {
                Lines.Add(new OrderLine(e.ProductId, e.Quantity, e.Price));
            })
            .On<OrderItemRemoved>(e =>
            {
                Lines.RemoveAll(l => l.ProductId == e.ProductId);
            })
            .On<OrderShipped>(e =>
            {
                TrackingNumber = e.TrackingNumber;
                Status = OrderStatus.Shipped;
            })
            .On<OrderDelivered>(e =>
            {
                DeliveredAt = e.DeliveredAt;
                Status = OrderStatus.Delivered;
            })
            .On<OrderCancelled>(e =>
            {
                Status = OrderStatus.Cancelled;
            })
            .Build();
    }

    public void Apply(OrderEvent @event)
    {
        CreateApplier().Dispatch(@event);
    }

    public static OrderAggregate FromEvents(IEnumerable<OrderEvent> events)
    {
        var aggregate = new OrderAggregate();
        var applier = aggregate.CreateApplier();

        foreach (var @event in events)
        {
            applier.Dispatch(@event);
        }

        return aggregate;
    }
}
```

### Why This Pattern

- **Event-specific mutations**: Each event type has clear, isolated logic
- **Replay support**: Same dispatcher replays events for state reconstruction
- **Clear event handling**: Easy to see what each event does to state

---

## Example 4: API Request Routing

### The Problem

A microservice needs to route different types of API requests (queries, commands, events) to appropriate handlers with different response types.

### The Solution

Use AsyncTypeDispatcher to route requests with type-safe handling.

### The Code

```csharp
// Request types
public abstract record ApiRequest;

// Queries (return data)
public abstract record Query<TResult> : ApiRequest;
public record GetUser(string UserId) : Query<UserDto>;
public record GetOrders(string UserId, int Page) : Query<PagedResult<OrderDto>>;
public record SearchProducts(string Query, int Limit) : Query<List<ProductDto>>;

// Commands (return status)
public abstract record Command<TResult> : ApiRequest;
public record CreateUser(CreateUserDto Data) : Command<string>; // Returns user ID
public record UpdateUser(string UserId, UpdateUserDto Data) : Command<bool>;
public record DeleteUser(string UserId) : Command<bool>;

// Handler
var requestHandler = AsyncTypeDispatcher<ApiRequest, object>.Create()
    // Queries
    .On<GetUser>(async (q, ct) =>
        await userService.GetByIdAsync(q.UserId, ct))

    .On<GetOrders>(async (q, ct) =>
        await orderService.GetForUserAsync(q.UserId, q.Page, ct))

    .On<SearchProducts>(async (q, ct) =>
        await searchService.SearchAsync(q.Query, q.Limit, ct))

    // Commands
    .On<CreateUser>(async (c, ct) =>
    {
        var user = await userService.CreateAsync(c.Data, ct);
        return user.Id;
    })

    .On<UpdateUser>(async (c, ct) =>
        await userService.UpdateAsync(c.UserId, c.Data, ct))

    .On<DeleteUser>(async (c, ct) =>
        await userService.DeleteAsync(c.UserId, ct))

    .Build();

// API Controller
[ApiController]
[Route("api")]
public class UnifiedApiController : ControllerBase
{
    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ApiRequest request, CancellationToken ct)
    {
        try
        {
            var result = await requestHandler.DispatchAsync(request, ct);
            return Ok(result);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Errors);
        }
    }
}
```

### Why This Pattern

- **Unified endpoint**: Single entry point for all request types
- **Type-safe routing**: Each request type has a specific handler
- **Extensible**: New request types are easily added

---

## Key Takeaways

1. **Type hierarchies are natural**: Use TypeDispatcher when you have a clear type hierarchy

2. **First-match-wins matters**: Register specific types before general types

3. **Combine sync and async**: Use appropriate variant based on handler needs

4. **Keep handlers focused**: Each handler should do one thing well

5. **Consider alternatives**: For very large type hierarchies, consider Dictionary-based lookup

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
- [VisitorDemo.cs](/src/PatternKit.Examples/VisitorDemo/VisitorDemo.cs) - POS tender handling example
- [PatternShowcase.cs](/src/PatternKit.Examples/PatternShowcase/PatternShowcase.cs) - Payment fee calculation
