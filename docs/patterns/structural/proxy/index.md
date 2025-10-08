# Proxy Pattern — Control Access to Objects

> **TL;DR**
> The Proxy pattern provides a **surrogate or placeholder** for another object to control access to it. Think of it like a security guard, cache layer, or lazy loader that sits between you and the real object.

---

## What is a Proxy? (For Beginners)

Imagine you want to watch a video on YouTube. When you click play, you're not directly accessing Google's servers in California. Instead, you're talking to a **proxy server** that might:
- Cache the video locally so it loads faster
- Check if you're allowed to view it in your country
- Log analytics about what you're watching
- Only load the video when you actually click play (lazy loading)

That's exactly what the Proxy pattern does in code! It wraps a real object (the "subject") and adds extra behavior before, after, or instead of calling the real object.

### Real-World Analogies

| Proxy Type | Real-World Example | What It Does |
|------------|-------------------|--------------|
| **Virtual Proxy** | ATM card instead of carrying cash | Represents expensive resource, created only when needed |
| **Protection Proxy** | Security guard at building entrance | Controls who can access the real object |
| **Remote Proxy** | Hotel concierge | Local representative for remote service |
| **Caching Proxy** | Waiter remembering your usual order | Stores results to avoid repeated work |
| **Logging Proxy** | Security camera | Records all interactions for audit trail |

---

## Why Use Proxy?

The Proxy pattern solves these common problems:

### 1. **Lazy Initialization (Virtual Proxy)**
Don't create expensive objects until you actually need them.

```csharp
// ❌ BAD: Database connection created immediately
var db = new ExpensiveDatabase("connection-string");
// ... might not even use it!

// ✅ GOOD: Database created only when first query runs
var dbProxy = Proxy<string, string>.Create()
    .VirtualProxy(() => {
        var db = new ExpensiveDatabase("connection-string");
        return sql => db.Query(sql);
    })
    .Build();

// Database not created yet...
// ... later when you actually need it:
var result = dbProxy.Execute("SELECT * FROM Users"); // NOW it initializes
```

### 2. **Access Control (Protection Proxy)**
Enforce security rules before allowing operations.

```csharp
// ✅ Only admins can delete users
var deleteProxy = Proxy<User, bool>.Create(user => DeleteUser(user))
    .ProtectionProxy(user => user.IsAdmin)
    .Build();

// Regular user tries to delete
try {
    deleteProxy.Execute(regularUser); // Throws UnauthorizedAccessException
} catch (UnauthorizedAccessException) {
    Console.WriteLine("Access denied!");
}

// Admin can delete
deleteProxy.Execute(adminUser); // Works fine
```

### 3. **Performance Optimization (Caching Proxy)**
Cache expensive results to avoid redundant work.

```csharp
// ✅ Cache expensive calculations
var proxy = Proxy<int, int>.Create(n => ExpensiveFibonacci(n))
    .CachingProxy()
    .Build();

proxy.Execute(100); // Takes 5 seconds to calculate
proxy.Execute(100); // Instant! Returns cached result
proxy.Execute(100); // Still instant!
```

### 4. **Monitoring & Debugging (Logging Proxy)**
Track every call for debugging or compliance.

```csharp
// ✅ Log all payment transactions
var paymentProxy = Proxy<Payment, bool>.Create(p => ProcessPayment(p))
    .LoggingProxy(msg => logger.Log(msg))
    .Build();

// Every payment is automatically logged
paymentProxy.Execute(new Payment(100, "USD")); 
// Logs: "Proxy invoked with input: Payment { Amount = 100, Currency = USD }"
// Logs: "Proxy returned output: True"
```

---

## PatternKit's Proxy Implementation

### Key Features

✅ **Fluent builder API** — Chain multiple concerns  
✅ **Immutable after build** — Thread-safe for concurrent use  
✅ **Allocation-light** — Minimal overhead  
✅ **Type-safe** — Generic `Proxy<TIn, TOut>` with compile-time safety  
✅ **Built-in patterns** — Virtual, Protection, Caching, Logging, and custom interception  

---

## Common Proxy Patterns

### Virtual Proxy (Lazy Initialization)

**When to use:** You have expensive objects (database connections, large files, network resources) that you don't always need.

**How it works:** The proxy delays creating the real object until the first method call.

```csharp
var imageProxy = Proxy<string, Image>.Create()
    .VirtualProxy(() => {
        Console.WriteLine("Loading 50MB image from disk...");
        return path => Image.Load(path);
    })
    .Build();

// Image not loaded yet
Console.WriteLine("Proxy created");

// NOW the image loads
var img = imageProxy.Execute("large-image.png");
```

**Thread safety:** PatternKit's virtual proxy uses double-checked locking, so it's safe to call from multiple threads simultaneously.

---

### Protection Proxy (Access Control)

**When to use:** You need to control who can access certain operations based on permissions, roles, or business rules.

**How it works:** The proxy checks a condition before delegating to the real subject. If the condition fails, it throws `UnauthorizedAccessException`.

```csharp
// Only allow premium users to access feature
var featureProxy = Proxy<User, FeatureResult>.Create(
        user => ExpensiveFeature(user))
    .ProtectionProxy(user => user.IsPremium)
    .Build();

// Free user
try {
    featureProxy.Execute(freeUser);
} catch (UnauthorizedAccessException) {
    Console.WriteLine("Upgrade to premium!");
}

// Premium user
var result = featureProxy.Execute(premiumUser); // Works!
```

**Real-world use cases:**
- Role-based access control (RBAC)
- Rate limiting API calls
- Feature flags
- Age verification
- Geographic restrictions

---

### Caching Proxy (Memoization)

**When to use:** You have expensive operations (calculations, database queries, API calls) that are called repeatedly with the same inputs.

**How it works:** The proxy stores a dictionary of `input → output`. On the first call, it invokes the real subject and caches the result. Subsequent calls return the cached value.

```csharp
var apiProxy = Proxy<string, ApiResponse>.Create(
        endpoint => CallExpensiveApi(endpoint))
    .CachingProxy()
    .Build();

apiProxy.Execute("/users/123"); // Hits API (slow)
apiProxy.Execute("/users/123"); // Returns cached (instant)
apiProxy.Execute("/users/456"); // Hits API (new endpoint)
apiProxy.Execute("/users/123"); // Still cached (instant)
```

**Important:** The cache uses the default equality comparer for `TIn`. For reference types, override `Equals()` and `GetHashCode()`, or provide a custom comparer:

```csharp
.CachingProxy(StringComparer.OrdinalIgnoreCase) // Case-insensitive cache
```

**Cache never expires.** For time-based expiration, use custom interception.

---

### Logging Proxy (Audit Trail)

**When to use:** You need to track all invocations for debugging, compliance, or analytics.

**How it works:** The proxy logs before and after calling the real subject.

```csharp
var logs = new List<string>();

var orderProxy = Proxy<Order, bool>.Create(
        order => PlaceOrder(order))
    .LoggingProxy(logs.Add)
    .Build();

orderProxy.Execute(new Order(item: "Widget", qty: 5));

// logs now contains:
// "Proxy invoked with input: Order { Item = Widget, Qty = 5 }"
// "Proxy returned output: True"
```

**Integration with logging frameworks:**
```csharp
.LoggingProxy(msg => _logger.LogInformation(msg))
```

---

### Remote Proxy (Network Optimization)

**When to use:** You're calling remote services (REST APIs, gRPC, databases) and want to add caching, retry logic, or logging.

**How it works:** Combine multiple proxy concerns by composing proxies.

```csharp
// Inner proxy: Add logging
var innerProxy = Proxy<int, string>.Create(id => CallRemoteService(id))
    .Intercept((id, next) => {
        _logger.Log($"Calling remote service for ID {id}");
        var result = next(id);
        _logger.Log($"Received response");
        return result;
    })
    .Build();

// Outer proxy: Add caching
var cachedRemoteProxy = Proxy<int, string>.Create(
        id => innerProxy.Execute(id))
    .CachingProxy()
    .Build();

// First call: Logs + hits network
cachedRemoteProxy.Execute(42);

// Second call: Returns cached (no logging, no network)
cachedRemoteProxy.Execute(42);
```

---

### Smart Reference (Reference Counting)

**When to use:** You need to track how many objects reference a resource and clean up when the last reference is released.

```csharp
var refCount = 0;

var resourceProxy = Proxy<string, Resource>.Create(
        name => AcquireResource(name))
    .Before(_ => Interlocked.Increment(ref refCount))
    .After((_, resource) => {
        if (Interlocked.Decrement(ref refCount) == 0) {
            resource.Dispose();
        }
    })
    .Build();
```

---

## Custom Interception

For advanced scenarios, use `.Intercept()` for full control:

```csharp
var retryProxy = Proxy<string, string>.Create(
        request => UnreliableService(request))
    .Intercept((input, next) => {
        for (int i = 0; i < 3; i++) {
            try {
                return next(input);
            } catch (Exception) when (i < 2) {
                Thread.Sleep(1000 * (i + 1)); // Exponential backoff
            }
        }
        throw new Exception("Max retries exceeded");
    })
    .Build();
```

**What you can do in an interceptor:**
- ✅ Modify input before calling subject
- ✅ Skip calling subject entirely (short-circuit)
- ✅ Modify output before returning
- ✅ Add error handling, retry logic, circuit breakers
- ✅ Measure execution time
- ✅ Implement custom caching strategies

---

## Proxy vs Decorator

Both patterns wrap objects, but they have different intents:

| Aspect | Proxy | Decorator |
|--------|-------|-----------|
| **Intent** | Control **access** to the subject | **Enhance** functionality of the subject |
| **Subject** | May not exist yet (virtual proxy) | Must exist at construction |
| **Delegation** | May skip calling subject entirely | Always calls the wrapped component |
| **Use case** | Lazy loading, security, caching | Add responsibilities (logging, validation, formatting) |
| **Examples** | Virtual proxy, protection proxy | Add encryption, compression, validation |

**Simple rule:** If you're asking "Should I call the real object?", use Proxy. If you're asking "How should I enhance the result?", use Decorator.

---

## Building a Mock Framework with Proxy

One of the most powerful uses of Proxy is building test doubles. Here's a simplified mocking framework:

```csharp
public class Mock<TIn, TOut>
{
    private List<TIn> _invocations = new();
    private Func<TIn, TOut> _behavior = _ => default!;

    public Mock<TIn, TOut> Returns(TOut value) {
        _behavior = _ => value;
        return this;
    }

    public Mock<TIn, TOut> Setup(Func<TIn, bool> predicate, TOut result) {
        var oldBehavior = _behavior;
        _behavior = input => predicate(input) ? result : oldBehavior(input);
        return this;
    }

    public Proxy<TIn, TOut> Build() {
        return Proxy<TIn, TOut>.Create(_behavior)
            .Intercept((input, next) => {
                _invocations.Add(input);
                return next(input);
            })
            .Build();
    }

    public void Verify(Func<TIn, bool> predicate, int times = 1) {
        var count = _invocations.Count(predicate);
        if (count != times)
            throw new Exception($"Expected {times} calls, got {count}");
    }
}
```

**Usage:**
```csharp
var emailMock = new Mock<(string to, string subject), bool>()
    .Setup(x => x.to.Contains("@spam.com"), false)
    .Returns(true);

var emailProxy = emailMock.Build();

emailProxy.Execute(("user@example.com", "Hello")); // true
emailProxy.Execute(("bad@spam.com", "Spam"));      // false

emailMock.Verify(x => x.to == "user@example.com", times: 1); // ✓
```

This is exactly how libraries like **Moq** and **NSubstitute** work under the hood!

---

## Performance Considerations

### Memory
- **Virtual proxy:** One extra allocation for the factory delegate
- **Caching proxy:** `O(n)` memory where `n` = number of unique inputs
- **Other proxies:** Minimal overhead (one object + delegates)

### Speed
- **Direct proxy:** ~1-2 ns overhead (delegate invocation)
- **Virtual proxy:** First call has lock overhead (~50-100 ns), subsequent calls are fast
- **Caching proxy:** Dictionary lookup (~5-10 ns) vs calling subject
- **Custom interceptor:** Depends on your logic

**Benchmark comparison:**
```
| Method          | Mean     |
|---------------- |---------:|
| DirectCall      | 1.2 ns   |
| DirectProxy     | 2.5 ns   |
| VirtualProxy    | 3.1 ns   | (after initialization)
| CachingProxy    | 8.3 ns   | (cache hit)
| LoggingProxy    | 45.2 ns  | (string allocation)
```

---

## Best Practices

### ✅ DO
- Use virtual proxies for expensive initialization
- Cache immutable or stable data
- Combine proxies for complex scenarios (remote + caching + logging)
- Use protection proxies at boundaries (API controllers, service layers)
- Build once, reuse many times (proxies are immutable)

### ❌ DON'T
- Cache mutable objects (cache will hold stale data)
- Use caching proxy without understanding equality semantics
- Create proxies in hot paths (create once, reuse)
- Mix responsibilities (use decorator pattern instead)
- Forget that caching proxy never expires (for TTL, use custom interception)

---

## Testing Proxy-Based Code

Proxies are inherently testable:

```csharp
[Fact]
public void CachingProxy_ShouldNotCallSubjectTwice()
{
    var callCount = 0;
    var proxy = Proxy<int, int>.Create(x => {
        callCount++;
        return x * 2;
    }).CachingProxy().Build();

    proxy.Execute(5);
    proxy.Execute(5);

    Assert.Equal(1, callCount); // Subject called only once
}
```

---

## Advanced Scenarios

### Composing Multiple Proxies

```csharp
// Layer 1: Retry logic
var retryProxy = Proxy<string, string>.Create(CallApi)
    .Intercept(RetryInterceptor)
    .Build();

// Layer 2: Caching
var cachedProxy = Proxy<string, string>.Create(
        req => retryProxy.Execute(req))
    .CachingProxy()
    .Build();

// Layer 3: Logging
var fullProxy = Proxy<string, string>.Create(
        req => cachedProxy.Execute(req))
    .LoggingProxy(logger.Log)
    .Build();

// Result: Log → Cache → Retry → API
```

### Conditional Proxies

```csharp
var proxy = Proxy<Request, Response>.Create(ProcessRequest)
    .Intercept((req, next) => {
        // Short-circuit for cached responses
        if (_cache.TryGet(req, out var cached))
            return cached;

        // Add timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return next(req);
    })
    .Build();
```

---

## See Also

- [Decorator Pattern](../decorator/index.md) — Enhance objects with new responsibilities
- [Adapter Pattern](../adapter/index.md) — Convert interfaces
- [Facade Pattern](../facade/index.md) — Simplify complex subsystems
- [Examples: Proxy Demonstrations](~/examples/proxy-demo.md) — Complete working examples

---

## Quick Reference

```csharp
// Virtual Proxy (lazy initialization)
.VirtualProxy(() => CreateExpensiveResource())

// Protection Proxy (access control)
.ProtectionProxy(input => HasPermission(input))

// Caching Proxy (memoization)
.CachingProxy()
.CachingProxy(customComparer)

// Logging Proxy (audit trail)
.LoggingProxy(msg => logger.Log(msg))

// Before/After (simple side effects)
.Before(input => Validate(input))
.After((input, output) => LogResult(input, output))

// Custom Interception (full control)
.Intercept((input, next) => {
    // Your logic here
    var result = next(input);
    return result;
})
```

---

**Next:** Check out the [complete working examples](~/examples/proxy-demo.md) including a mock framework, remote proxy with caching, and retry logic.

