# Chain of Responsibility Pattern Real-World Examples

Production-ready examples demonstrating the Chain of Responsibility pattern in real-world scenarios.

---

## Example 1: HTTP Request Pipeline

### The Problem

A web API needs to process incoming requests through multiple stages: logging, authentication, rate limiting, validation, and finally the business logic. Each stage may either continue processing or short-circuit with an error response.

### The Solution

Use ActionChain to build a middleware pipeline where each stage can continue or stop the chain.

### The Code

```csharp
public class HttpContext
{
    public HttpRequest Request { get; init; }
    public HttpResponse Response { get; set; } = new();
    public ClaimsPrincipal? User { get; set; }
    public bool IsHandled { get; set; }
}

public static AsyncActionChain<HttpContext> CreateRequestPipeline(
    ILogger logger,
    IAuthService auth,
    IRateLimiter rateLimiter)
{
    return AsyncActionChain<HttpContext>.Create()
        // Stage 1: Request logging
        .Use(async (ctx, ct, next) =>
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            ctx.Request.Headers["X-Request-Id"] = requestId;

            logger.LogInformation("Request {Id}: {Method} {Path}",
                requestId, ctx.Request.Method, ctx.Request.Path);

            var sw = Stopwatch.StartNew();
            await next(ctx, ct);
            sw.Stop();

            logger.LogInformation("Request {Id} completed in {Ms}ms with {Status}",
                requestId, sw.ElapsedMilliseconds, ctx.Response.StatusCode);
        })

        // Stage 2: Rate limiting
        .When(ctx => !rateLimiter.IsAllowed(ctx.Request.ClientIp))
            .ThenStop(async (ctx, ct) =>
            {
                ctx.Response.StatusCode = 429;
                ctx.Response.Body = "Too many requests";
                ctx.IsHandled = true;
            })

        // Stage 3: Authentication (for protected routes)
        .When(ctx => ctx.Request.Path.StartsWith("/api/") &&
                     !ctx.Request.Path.StartsWith("/api/public/"))
            .ThenContinue(async (ctx, ct) =>
            {
                var token = ctx.Request.Headers.GetValueOrDefault("Authorization");
                if (string.IsNullOrEmpty(token))
                {
                    ctx.Response.StatusCode = 401;
                    ctx.Response.Body = "Authentication required";
                    ctx.IsHandled = true;
                    return;
                }

                var user = await auth.ValidateTokenAsync(token, ct);
                if (user is null)
                {
                    ctx.Response.StatusCode = 401;
                    ctx.Response.Body = "Invalid token";
                    ctx.IsHandled = true;
                    return;
                }

                ctx.User = user;
            })

        // Stage 4: Skip if already handled
        .When(ctx => ctx.IsHandled)
            .ThenStop(ctx => { /* Already handled, stop chain */ })

        // Stage 5: Route to handler
        .Finally(async (ctx, ct) =>
        {
            await RouteRequestAsync(ctx, ct);
        })
        .Build();
}

// Usage
var pipeline = CreateRequestPipeline(logger, authService, rateLimiter);
await pipeline.ExecuteAsync(httpContext, cancellationToken);
```

### Why This Pattern

- **Separation of concerns**: Each stage handles one aspect (logging, auth, rate limiting)
- **Composable**: Easy to add/remove/reorder middleware
- **Short-circuit**: Early exit on errors prevents unnecessary processing
- **Cross-cutting**: Logging wraps the entire pipeline

---

## Example 2: Order Validation Pipeline

### The Problem

An e-commerce system needs to validate orders through multiple business rules before processing. Rules include: non-empty cart, valid quantities, customer eligibility, payment validation, and inventory checks.

### The Solution

Use ActionChain to create a validation pipeline that accumulates errors or short-circuits on critical failures.

### The Code

```csharp
public class OrderValidationContext
{
    public Order Order { get; init; }
    public Customer Customer { get; init; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsCriticalFailure { get; set; }
}

public static ActionChain<OrderValidationContext> CreateOrderValidator()
{
    return ActionChain<OrderValidationContext>.Create()
        // Rule 1: Order must have items
        .When(ctx => ctx.Order.Items.Count == 0)
            .ThenStop(ctx =>
            {
                ctx.Errors.Add("Order must contain at least one item");
                ctx.IsCriticalFailure = true;
            })

        // Rule 2: All quantities must be positive
        .When(ctx => ctx.Order.Items.Any(i => i.Quantity <= 0))
            .ThenContinue(ctx =>
            {
                var invalid = ctx.Order.Items.Where(i => i.Quantity <= 0);
                foreach (var item in invalid)
                    ctx.Errors.Add($"Invalid quantity for {item.ProductName}");
            })

        // Rule 3: Check customer eligibility
        .When(ctx => ctx.Customer.Status == CustomerStatus.Suspended)
            .ThenStop(ctx =>
            {
                ctx.Errors.Add("Customer account is suspended");
                ctx.IsCriticalFailure = true;
            })

        // Rule 4: Credit limit check
        .When(ctx => ctx.Order.Total > ctx.Customer.AvailableCredit)
            .ThenContinue(ctx =>
            {
                ctx.Errors.Add($"Order total ${ctx.Order.Total} exceeds available credit ${ctx.Customer.AvailableCredit}");
            })

        // Rule 5: Large order warning
        .When(ctx => ctx.Order.Total > 10000)
            .ThenContinue(ctx =>
            {
                ctx.Warnings.Add("Large order - may require manager approval");
            })

        // Rule 6: Hazmat items require verification
        .When(ctx => ctx.Order.Items.Any(i => i.IsHazmat) && !ctx.Customer.HasHazmatCertification)
            .ThenContinue(ctx =>
            {
                ctx.Errors.Add("Hazardous materials require customer certification");
            })

        // Final: Mark validation complete
        .Finally((in ctx, next) =>
        {
            if (ctx.Errors.Count == 0)
                ctx.Order.Status = OrderStatus.Validated;
            next(in ctx);
        })
        .Build();
}

// Usage
var validator = CreateOrderValidator();
var context = new OrderValidationContext { Order = order, Customer = customer };
validator.Execute(in context);

if (context.Errors.Any())
{
    return ValidationResult.Failed(context.Errors);
}
return ValidationResult.Success(context.Warnings);
```

### Why This Pattern

- **Ordered rules**: Validation happens in logical sequence
- **Early exit**: Critical failures stop immediately
- **Error accumulation**: Non-critical errors collect for batch reporting
- **Extensible**: New rules added without modifying existing ones

---

## Example 3: API Router with Result Production

### The Problem

A REST API needs to route requests to handlers based on HTTP method and path patterns, with support for route parameters and a fallback for unmatched routes.

### The Solution

Use ResultChain to create a router where the first matching route produces the response.

### The Code

```csharp
public record RouteResult(int Status, string Body, string ContentType = "application/json");

public static AsyncResultChain<HttpRequest, RouteResult> CreateApiRouter(
    IUserService users,
    IOrderService orders)
{
    return AsyncResultChain<HttpRequest, RouteResult>.Create()
        // Health check
        .When(r => r.IsGet("/health"))
            .Then(r => new RouteResult(200, "{\"status\":\"healthy\"}"))

        // API version
        .When(r => r.IsGet("/api/version"))
            .Then(r => new RouteResult(200, "{\"version\":\"1.0.0\"}"))

        // Users endpoints
        .When(r => r.IsGet("/api/users"))
            .Then(async (r, ct) =>
            {
                var userList = await users.GetAllAsync(ct);
                return new RouteResult(200, JsonSerializer.Serialize(userList));
            })

        .When(r => r.IsGet("/api/users/") && r.PathSegments.Length == 3)
            .Then(async (r, ct) =>
            {
                var id = r.PathSegments[2];
                var user = await users.GetByIdAsync(id, ct);
                if (user is null)
                    return new RouteResult(404, "{\"error\":\"User not found\"}");
                return new RouteResult(200, JsonSerializer.Serialize(user));
            })

        .When(r => r.IsPost("/api/users"))
            .Then(async (r, ct) =>
            {
                var dto = JsonSerializer.Deserialize<CreateUserDto>(r.Body);
                var user = await users.CreateAsync(dto!, ct);
                return new RouteResult(201, JsonSerializer.Serialize(user));
            })

        // Orders endpoints
        .When(r => r.IsGet("/api/orders"))
            .Then(async (r, ct) =>
            {
                var orderList = await orders.GetAllAsync(ct);
                return new RouteResult(200, JsonSerializer.Serialize(orderList));
            })

        .When(r => r.IsPost("/api/orders"))
            .Then(async (r, ct) =>
            {
                var dto = JsonSerializer.Deserialize<CreateOrderDto>(r.Body);
                var order = await orders.CreateAsync(dto!, ct);
                return new RouteResult(201, JsonSerializer.Serialize(order));
            })

        // 404 fallback
        .Finally(async (r, ct) => new RouteResult(404, "{\"error\":\"Not found\"}"))
        .Build();
}

// Usage
var router = CreateApiRouter(userService, orderService);
var (success, result) = await router.ExecuteAsync(request, ct);
// success is always true because we have a Finally handler
await WriteResponse(result!);
```

### Why This Pattern

- **Declarative routing**: Routes defined clearly in code
- **First match wins**: No ambiguity about which handler runs
- **Async support**: Database calls happen naturally
- **Guaranteed response**: Finally ensures 404 for unmatched routes

---

## Example 4: Payment Processing Pipeline

### The Problem

A payment system needs to process transactions through multiple stages: fraud detection, balance check, payment execution, and notification. Any stage can fail and should produce appropriate results.

### The Solution

Use ResultChain to process payments with stage-specific failure handling.

### The Code

```csharp
public record PaymentResult(
    bool Success,
    string? TransactionId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public static AsyncResultChain<PaymentRequest, PaymentResult> CreatePaymentProcessor(
    IFraudService fraud,
    IAccountService accounts,
    IPaymentGateway gateway,
    INotificationService notifications)
{
    return AsyncResultChain<PaymentRequest, PaymentResult>.Create()
        // Stage 1: Fraud detection
        .When(r => true) // Always run
            .Then(async (r, ct) =>
            {
                var fraudResult = await fraud.AnalyzeAsync(r, ct);
                if (fraudResult.IsHighRisk)
                {
                    return new PaymentResult(false,
                        ErrorCode: "FRAUD_DETECTED",
                        ErrorMessage: "Transaction flagged for fraud review");
                }
                return null!; // Continue to next handler
            })

        // Stage 2: Balance/credit check
        .Use(async (r, ct) =>
        {
            var balance = await accounts.GetBalanceAsync(r.AccountId, ct);
            if (balance < r.Amount)
            {
                return (true, new PaymentResult(false,
                    ErrorCode: "INSUFFICIENT_FUNDS",
                    ErrorMessage: $"Available: {balance:C}, Required: {r.Amount:C}"));
            }
            return (false, default); // Continue
        })

        // Stage 3: Execute payment
        .Use(async (r, ct) =>
        {
            try
            {
                var result = await gateway.ProcessAsync(r, ct);
                if (!result.Success)
                {
                    return (true, new PaymentResult(false,
                        ErrorCode: result.DeclineCode,
                        ErrorMessage: result.DeclineReason));
                }

                // Success! Send notification and return
                await notifications.SendPaymentConfirmationAsync(r.AccountId, result.TransactionId, ct);

                return (true, new PaymentResult(true, TransactionId: result.TransactionId));
            }
            catch (PaymentGatewayException ex)
            {
                return (true, new PaymentResult(false,
                    ErrorCode: "GATEWAY_ERROR",
                    ErrorMessage: ex.Message));
            }
        })

        // Fallback: Should never reach here
        .Finally(async (r, ct) => new PaymentResult(false,
            ErrorCode: "INTERNAL_ERROR",
            ErrorMessage: "Payment processing failed unexpectedly"))
        .Build();
}

// Usage
var processor = CreatePaymentProcessor(fraud, accounts, gateway, notifications);
var (_, result) = await processor.ExecuteAsync(paymentRequest, ct);

if (result.Success)
    return Ok(new { transactionId = result.TransactionId });
else
    return BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
```

### Why This Pattern

- **Stage isolation**: Each stage has clear responsibility
- **Early exit on failure**: Bad transactions don't reach payment gateway
- **Consistent result type**: All paths return PaymentResult
- **Error specificity**: Each failure type has distinct error code

---

## Key Takeaways

1. **Use ActionChain for side effects**: Logging, validation, state mutation
2. **Use ResultChain for routing**: When you need to produce a value
3. **Order matters**: Place guards and quick-exit conditions early
4. **Always provide Finally**: Handle the "nothing matched" case
5. **Leverage async variants**: For I/O-bound operations

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
- [ActionChain.md](./actionchain.md) - Original ActionChain documentation
- [ResultChain.md](./resultchain.md) - Original ResultChain documentation
