# Testing Patterns Guide

Strategies for effectively testing PatternKit patterns in your applications.

---

## Testing Philosophy

PatternKit patterns are designed to be testable:
1. **Immutable after Build()** — deterministic behavior
2. **Delegate-based** — easily mockable
3. **Composable** — test layers independently
4. **Side-effect free** — patterns transform data, don't modify state

---

## Testing by Pattern Category

### Creational Patterns

#### Factory Testing

Test that correct types are created for given keys:

```csharp
public class FactoryTests
{
    private readonly Factory<string, IShape> _factory;

    public FactoryTests()
    {
        _factory = Factory<string, IShape>.Create()
            .Map("circle", () => new Circle())
            .Map("square", () => new Square())
            .Build();
    }

    [Fact]
    public void Create_WithValidKey_ReturnsCorrectType()
    {
        var circle = _factory.Create("circle");
        var square = _factory.Create("square");

        Assert.IsType<Circle>(circle);
        Assert.IsType<Square>(square);
    }

    [Fact]
    public void Create_WithInvalidKey_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            _factory.Create("triangle"));
    }

    [Fact]
    public void Create_ReturnsNewInstanceEachTime()
    {
        var first = _factory.Create("circle");
        var second = _factory.Create("circle");

        Assert.NotSame(first, second);
    }
}
```

#### Prototype Testing

Test cloning and mutations:

```csharp
public class PrototypeTests
{
    private readonly Prototype<string, Order> _templates;

    public PrototypeTests()
    {
        _templates = Prototype<string, Order>.Create()
            .Map("standard", new Order { Type = "Standard", Priority = 1 },
                 o => o.Clone())
            .Mutate("standard", o => o.Priority = 5)
            .Build();
    }

    [Fact]
    public void Create_ClonesTemplate_NotSameInstance()
    {
        var order1 = _templates.Create("standard");
        var order2 = _templates.Create("standard");

        Assert.NotSame(order1, order2);
    }

    [Fact]
    public void Create_AppliesMutation()
    {
        var order = _templates.Create("standard");

        Assert.Equal(5, order.Priority);
    }

    [Fact]
    public void Create_WithCustomizer_AppliesCustomization()
    {
        var order = _templates.Create("standard", o => o.CustomerId = "CUST-001");

        Assert.Equal("CUST-001", order.CustomerId);
        Assert.Equal(5, order.Priority); // Mutation still applied
    }
}
```

---

### Structural Patterns

#### Decorator Testing

Test each decorator layer independently, then composed:

```csharp
public class DecoratorTests
{
    [Fact]
    public void Core_ProcessesInput()
    {
        var decorator = Decorator<int, int>.Create(x => x * 2)
            .Build();

        Assert.Equal(10, decorator.Execute(5));
    }

    [Fact]
    public void Before_ExecutesBeforeCore()
    {
        var log = new List<string>();

        var decorator = Decorator<int, int>.Create(x =>
            {
                log.Add("core");
                return x * 2;
            })
            .Before(x => log.Add("before"))
            .Build();

        decorator.Execute(5);

        Assert.Equal(new[] { "before", "core" }, log);
    }

    [Fact]
    public void After_TransformsResult()
    {
        var decorator = Decorator<int, int>.Create(x => x * 2)
            .After((x, result) => result + 1)
            .Build();

        Assert.Equal(11, decorator.Execute(5)); // (5 * 2) + 1
    }

    [Fact]
    public void Around_WrapsExecution()
    {
        var log = new List<string>();

        var decorator = Decorator<int, int>.Create(x =>
            {
                log.Add("core");
                return x * 2;
            })
            .Around((x, next) =>
            {
                log.Add("before-around");
                var result = next(x);
                log.Add("after-around");
                return result;
            })
            .Build();

        decorator.Execute(5);

        Assert.Equal(new[] { "before-around", "core", "after-around" }, log);
    }

    [Fact]
    public void MultipleDecorators_ExecuteInOrder()
    {
        var log = new List<string>();

        var decorator = Decorator<int, int>.Create(x => x)
            .Before(x => log.Add("before1"))
            .Before(x => log.Add("before2"))
            .After((x, r) => { log.Add("after1"); return r; })
            .After((x, r) => { log.Add("after2"); return r; })
            .Build();

        decorator.Execute(5);

        // Before: in order, After: reverse order
        Assert.Equal(new[] { "before1", "before2", "after2", "after1" }, log);
    }
}
```

#### Proxy Testing

Test access control, caching, and lazy initialization:

```csharp
public class ProxyTests
{
    [Fact]
    public void VirtualProxy_DelaysInitialization()
    {
        var initialized = false;

        var proxy = Proxy<int, int>.Create()
            .VirtualProxy(() =>
            {
                initialized = true;
                return x => x * 2;
            })
            .Build();

        Assert.False(initialized);

        proxy.Execute(5);

        Assert.True(initialized);
    }

    [Fact]
    public void ProtectionProxy_AllowsAuthorizedAccess()
    {
        var proxy = Proxy<int, int>.Create(x => x * 2)
            .ProtectionProxy(x => x > 0)
            .Build();

        var result = proxy.Execute(5);

        Assert.Equal(10, result);
    }

    [Fact]
    public void ProtectionProxy_DeniesUnauthorizedAccess()
    {
        var proxy = Proxy<int, int>.Create(x => x * 2)
            .ProtectionProxy(x => x > 0)
            .Build();

        Assert.Throws<UnauthorizedAccessException>(() =>
            proxy.Execute(-1));
    }

    [Fact]
    public void CachingProxy_ReturnsCachedResult()
    {
        var callCount = 0;

        var proxy = Proxy<int, int>.Create(x =>
            {
                callCount++;
                return x * 2;
            })
            .CachingProxy()
            .Build();

        proxy.Execute(5);
        proxy.Execute(5);
        proxy.Execute(5);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void CachingProxy_CachesPerInput()
    {
        var callCount = 0;

        var proxy = Proxy<int, int>.Create(x =>
            {
                callCount++;
                return x * 2;
            })
            .CachingProxy()
            .Build();

        proxy.Execute(5);
        proxy.Execute(10);
        proxy.Execute(5);

        Assert.Equal(2, callCount);
    }
}
```

---

### Behavioral Patterns

#### Strategy Testing

Test condition matching and handler execution:

```csharp
public class StrategyTests
{
    [Theory]
    [InlineData(150, 0)]      // Free shipping over 100
    [InlineData(50, 10)]      // Standard shipping
    [InlineData(-10, 10)]     // Default
    public void Execute_SelectsCorrectStrategy(decimal orderTotal, decimal expectedShipping)
    {
        var strategy = Strategy<decimal, decimal>.Create()
            .When(total => total > 100).Then(_ => 0m)
            .When(total => total > 0).Then(_ => 10m)
            .Default(_ => 10m)
            .Build();

        var result = strategy.Execute(orderTotal);

        Assert.Equal(expectedShipping, result);
    }

    [Fact]
    public void Execute_FirstMatchWins()
    {
        var strategy = Strategy<int, string>.Create()
            .When(x => x > 0).Then(_ => "positive")
            .When(x => x > 10).Then(_ => "greater than 10")
            .Default(_ => "other")
            .Build();

        // x=15 matches first condition, doesn't reach second
        Assert.Equal("positive", strategy.Execute(15));
    }

    [Fact]
    public void TryExecute_ReturnsFalseWhenNoMatch()
    {
        var strategy = TryStrategy<int, int>.Create()
            .When(x => x > 0).Then(x => x * 2)
            // No default
            .Build();

        var matched = strategy.TryExecute(-1, out var result);

        Assert.False(matched);
        Assert.Equal(default, result);
    }
}
```

#### Chain Testing

Test handler sequence and early termination:

```csharp
public class ChainTests
{
    [Fact]
    public void Execute_StopsOnFirstMatch()
    {
        var handlersCalled = new List<int>();

        var chain = ResultChain<int, string>.Create()
            .When(x => x == 1)
                .Then(x => { handlersCalled.Add(1); return "one"; })
            .When(x => x == 2)
                .Then(x => { handlersCalled.Add(2); return "two"; })
            .Finally((in int x, out string? r, ResultChain<int, string>.Context ctx) =>
            {
                handlersCalled.Add(0);
                r = "default";
                return true;
            })
            .Build();

        chain.Execute(1);

        Assert.Equal(new[] { 1 }, handlersCalled);
    }

    [Fact]
    public void Execute_CallsFinallyWhenNoMatch()
    {
        var finallyCalled = false;

        var chain = ResultChain<int, string>.Create()
            .When(x => x == 1).Then(_ => "one")
            .Finally((in int x, out string? r, ResultChain<int, string>.Context ctx) =>
            {
                finallyCalled = true;
                r = "default";
                return true;
            })
            .Build();

        chain.Execute(999);

        Assert.True(finallyCalled);
    }

    [Fact]
    public void TryExecute_ReturnsFalseWhenUnhandled()
    {
        var chain = ResultChain<int, string>.Create()
            .When(x => x == 1).Then(_ => "one")
            // No Finally
            .Build();

        var handled = chain.TryExecute(999, out var result);

        Assert.False(handled);
    }
}
```

#### TypeDispatcher Testing

Test type-based routing:

```csharp
public class TypeDispatcherTests
{
    private readonly TypeDispatcher<Shape, double> _dispatcher;

    public TypeDispatcherTests()
    {
        _dispatcher = TypeDispatcher<Shape, double>.Create()
            .On<Circle>(c => Math.PI * c.Radius * c.Radius)
            .On<Rectangle>(r => r.Width * r.Height)
            .Default(_ => 0)
            .Build();
    }

    [Fact]
    public void Dispatch_Circle_CalculatesArea()
    {
        var circle = new Circle(5);

        var area = _dispatcher.Dispatch(circle);

        Assert.Equal(Math.PI * 25, area, precision: 5);
    }

    [Fact]
    public void Dispatch_Rectangle_CalculatesArea()
    {
        var rect = new Rectangle(4, 5);

        var area = _dispatcher.Dispatch(rect);

        Assert.Equal(20, area);
    }

    [Fact]
    public void Dispatch_UnknownType_UsesDefault()
    {
        var unknown = new Triangle(3, 4, 5);

        var area = _dispatcher.Dispatch(unknown);

        Assert.Equal(0, area);
    }
}
```

#### Observer Testing

Test subscriptions and notifications:

```csharp
public class ObserverTests
{
    [Fact]
    public void Publish_NotifiesAllSubscribers()
    {
        var received = new List<int>();
        var observer = Observer<int>.Create().Build();

        observer.Subscribe(x => received.Add(x));
        observer.Subscribe(x => received.Add(x * 10));

        observer.Publish(5);

        Assert.Equal(new[] { 5, 50 }, received);
    }

    [Fact]
    public void Publish_FilteredSubscriber_OnlyReceivesMatching()
    {
        var received = new List<int>();
        var observer = Observer<int>.Create().Build();

        observer.Subscribe(
            x => x > 10,
            x => received.Add(x));

        observer.Publish(5);
        observer.Publish(15);
        observer.Publish(8);
        observer.Publish(20);

        Assert.Equal(new[] { 15, 20 }, received);
    }

    [Fact]
    public void Unsubscribe_StopsNotifications()
    {
        var received = new List<int>();
        var observer = Observer<int>.Create().Build();

        var subscription = observer.Subscribe(x => received.Add(x));

        observer.Publish(1);
        subscription.Dispose();
        observer.Publish(2);

        Assert.Equal(new[] { 1 }, received);
    }
}
```

---

## Mocking Patterns

### Injecting Test Doubles

PatternKit patterns accept delegates, making them easy to mock:

```csharp
public class OrderService
{
    private readonly Strategy<Order, decimal> _pricingStrategy;

    public OrderService(Strategy<Order, decimal> pricingStrategy)
    {
        _pricingStrategy = pricingStrategy;
    }

    public decimal CalculateTotal(Order order) =>
        _pricingStrategy.Execute(order);
}

public class OrderServiceTests
{
    [Fact]
    public void CalculateTotal_UsesInjectedStrategy()
    {
        // Arrange: Create test strategy
        var testStrategy = Strategy<Order, decimal>.Create()
            .Default(_ => 100m)  // Fixed price for testing
            .Build();

        var service = new OrderService(testStrategy);

        // Act
        var total = service.CalculateTotal(new Order());

        // Assert
        Assert.Equal(100m, total);
    }
}
```

### Capturing Calls

Use test doubles to verify interactions:

```csharp
public class DecoratorInteractionTests
{
    [Fact]
    public void Before_ReceivesCorrectInput()
    {
        int? capturedInput = null;

        var decorator = Decorator<int, int>.Create(x => x * 2)
            .Before(x => capturedInput = x)
            .Build();

        decorator.Execute(42);

        Assert.Equal(42, capturedInput);
    }

    [Fact]
    public void After_ReceivesInputAndResult()
    {
        (int input, int result)? captured = null;

        var decorator = Decorator<int, int>.Create(x => x * 2)
            .After((x, r) => { captured = (x, r); return r; })
            .Build();

        decorator.Execute(5);

        Assert.NotNull(captured);
        Assert.Equal(5, captured.Value.input);
        Assert.Equal(10, captured.Value.result);
    }
}
```

---

## Integration Testing

### Testing Composed Patterns

Test the full composition, not just individual patterns:

```csharp
public class PaymentPipelineIntegrationTests
{
    private readonly Decorator<PaymentRequest, PaymentResult> _pipeline;
    private readonly List<string> _auditLog;

    public PaymentPipelineIntegrationTests()
    {
        _auditLog = new List<string>();

        // Build the full pipeline
        var validation = ResultChain<PaymentRequest, PaymentResult>.Create()
            .When(r => r.Amount <= 0)
                .Then(_ => new PaymentResult(false, "Invalid amount"))
            .When(r => string.IsNullOrEmpty(r.CardNumber))
                .Then(_ => new PaymentResult(false, "Missing card"))
            .Build();

        _pipeline = Decorator<PaymentRequest, PaymentResult>.Create(
                request =>
                {
                    if (validation.TryExecute(request, out var error))
                        return error;

                    // Simulate payment processing
                    return new PaymentResult(true, "Approved");
                })
            .Before(r => _auditLog.Add($"Processing: {r.Amount}"))
            .After((r, result) =>
            {
                _auditLog.Add($"Result: {result.Success}");
                return result;
            })
            .Build();
    }

    [Fact]
    public void ValidPayment_ProcessesSuccessfully()
    {
        var request = new PaymentRequest(100m, "4111111111111111");

        var result = _pipeline.Execute(request);

        Assert.True(result.Success);
        Assert.Contains("Processing: 100", _auditLog);
        Assert.Contains("Result: True", _auditLog);
    }

    [Fact]
    public void InvalidAmount_FailsValidation()
    {
        var request = new PaymentRequest(-10m, "4111111111111111");

        var result = _pipeline.Execute(request);

        Assert.False(result.Success);
        Assert.Equal("Invalid amount", result.Message);
    }
}
```

---

## Async Pattern Testing

### Testing Async Patterns

```csharp
public class AsyncPatternTests
{
    [Fact]
    public async Task AsyncProxy_ExecutesAsynchronously()
    {
        var proxy = AsyncProxy<int, int>.Create(
                async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x * 2;
                })
            .Build();

        var result = await proxy.ExecuteAsync(5);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncProxy_SupportsCancellation()
    {
        var cts = new CancellationTokenSource();

        var proxy = AsyncProxy<int, int>.Create(
                async (x, ct) =>
                {
                    await Task.Delay(10000, ct); // Long delay
                    return x * 2;
                })
            .Build();

        cts.CancelAfter(50);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            proxy.ExecuteAsync(5, cts.Token).AsTask());
    }

    [Fact]
    public async Task AsyncDecorator_ExecutesInOrder()
    {
        var log = new List<string>();

        var decorator = AsyncDecorator<int, int>.Create(
                async (x, ct) =>
                {
                    log.Add("core");
                    return x * 2;
                })
            .Before(async (x, ct) => log.Add("before"))
            .After(async (x, r, ct) => { log.Add("after"); return r; })
            .Build();

        await decorator.ExecuteAsync(5);

        Assert.Equal(new[] { "before", "core", "after" }, log);
    }
}
```

---

## Test Organization

### Recommended Structure

```
tests/
├── Unit/
│   ├── Patterns/
│   │   ├── StrategyTests.cs
│   │   ├── ChainTests.cs
│   │   ├── DecoratorTests.cs
│   │   └── ...
│   └── Services/
│       └── OrderServiceTests.cs
├── Integration/
│   ├── Pipelines/
│   │   ├── PaymentPipelineTests.cs
│   │   └── OrderProcessingTests.cs
│   └── Compositions/
│       └── FullWorkflowTests.cs
└── Performance/
    └── PatternBenchmarks.cs
```

### Test Naming Convention

```csharp
// Method_Scenario_ExpectedResult
[Fact]
public void Execute_WithValidInput_ReturnsExpectedResult() { }

[Fact]
public void Execute_WithInvalidInput_ThrowsArgumentException() { }

[Fact]
public void TryExecute_WhenNoMatch_ReturnsFalse() { }
```

---

## Best Practices

### Do

- Test each layer/handler independently first
- Use Theory/InlineData for multiple inputs
- Verify side effects (logs, metrics) with captured data
- Test edge cases: null, empty, boundary values
- Test async cancellation scenarios

### Don't

- Test internal implementation details
- Over-mock — use real patterns when simple
- Ignore exception testing
- Skip integration tests for composed patterns

---

## See Also

- [Choosing Patterns](choosing-patterns.md)
- [Composing Patterns](composing-patterns.md)
- [Performance Guide](performance.md)
