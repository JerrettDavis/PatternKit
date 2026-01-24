# Proxy Generator

## Overview

The **Proxy Generator** creates GoF-compliant Proxy pattern implementations that provide controlled access to objects through generated wrapper classes. It eliminates boilerplate by automatically generating proxy types with optional interceptor support for cross-cutting concerns like logging, caching, authentication, and performance monitoring.

The generator produces **self-contained C# code** with **no runtime PatternKit dependency**, making it suitable for AOT and trimming scenarios.

## When to Use

Use the Proxy generator when you need to:

- **Add cross-cutting concerns**: Logging, timing, caching, authentication, circuit breakers
- **Control access**: Add authorization or validation before method execution
- **Lazy initialization**: Defer expensive object creation until first use
- **Remote proxies**: Add network communication layers
- **Aspect-oriented programming**: Inject behavior without modifying the original class

## Installation

The generator is included in the `PatternKit.Generators` package:

```bash
dotnet add package PatternKit.Generators
```

## Quick Start

### Basic Proxy (No Interceptors)

```csharp
using PatternKit.Generators.Proxy;

[GenerateProxy(InterceptorMode = ProxyInterceptorMode.None)]
public partial interface IUserService
{
    User GetUser(Guid id);
    void UpdateUser(User user);
}
```

Generated:
```csharp
public sealed partial class UserServiceProxy : IUserService
{
    private readonly IUserService _inner;

    public UserServiceProxy(IUserService inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public User GetUser(Guid id) => _inner.GetUser(id);
    public void UpdateUser(User user) => _inner.UpdateUser(user);
}
```

### Proxy with Single Interceptor

```csharp
[GenerateProxy] // InterceptorMode.Single is default
public partial interface IUserService
{
    User GetUser(Guid id);
    ValueTask<User> GetUserAsync(Guid id, CancellationToken ct = default);
}
```

Generated proxy class + interceptor interface:
```csharp
public sealed partial class UserServiceProxy : IUserService
{
    private readonly IUserService _inner;
    private readonly IUserServiceInterceptor? _interceptor;

    public UserServiceProxy(IUserService inner, IUserServiceInterceptor? interceptor = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _interceptor = interceptor;
    }

    public User GetUser(Guid id)
    {
        if (_interceptor is null)
            return _inner.GetUser(id);

        var context = new GetUserMethodContext(id);
        try
        {
            _interceptor.Before(context);
            var __result = _inner.GetUser(id);
            context.SetResult(__result);
            _interceptor.After(context);
            return __result;
        }
        catch (Exception __ex)
        {
            _interceptor.OnException(context, __ex);
            throw; // Rethrow is default
        }
    }

    public async ValueTask<User> GetUserAsync(Guid id, CancellationToken ct = default)
    {
        if (_interceptor is null)
            return await _inner.GetUserAsync(id, ct).ConfigureAwait(false);

        var context = new GetUserAsyncMethodContext(id, ct);
        try
        {
            await _interceptor.BeforeAsync(context, ct).ConfigureAwait(false);
            var __result = await _inner.GetUserAsync(id, ct).ConfigureAwait(false);
            context.SetResult(__result);
            await _interceptor.AfterAsync(context, ct).ConfigureAwait(false);
            return __result;
        }
        catch (Exception __ex)
        {
            await _interceptor.OnExceptionAsync(context, __ex, ct).ConfigureAwait(false);
            throw;
        }
    }
}

public interface IUserServiceInterceptor
{
    void Before(MethodContext context);
    void After(MethodContext context);
    void OnException(MethodContext context, Exception ex);
    
    ValueTask BeforeAsync(MethodContext context, CancellationToken ct);
    ValueTask AfterAsync(MethodContext context, CancellationToken ct);
    ValueTask OnExceptionAsync(MethodContext context, Exception ex, CancellationToken ct);
}

public abstract class MethodContext
{
    public abstract string MethodName { get; }
    public abstract object?[]? Arguments { get; }
    public object? Result { get; private set; }
    internal void SetResult(object? result) => Result = result;
}

public sealed class GetUserMethodContext : MethodContext
{
    public Guid Id { get; }
    public GetUserMethodContext(Guid id) { Id = id; }
    public override string MethodName => "GetUser";
    public override object?[]? Arguments => new object?[] { Id };
}
```

### Proxy with Pipeline Interceptors

```csharp
[GenerateProxy(InterceptorMode = ProxyInterceptorMode.Pipeline)]
public partial interface IOrderService
{
    Order CreateOrder(OrderRequest request);
}
```

Generated:
```csharp
public sealed partial class OrderServiceProxy : IOrderService
{
    private readonly IOrderService _inner;
    private readonly IReadOnlyList<IOrderServiceInterceptor> _interceptors;

    public OrderServiceProxy(
        IOrderService inner, 
        IReadOnlyList<IOrderServiceInterceptor>? interceptors = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _interceptors = interceptors ?? Array.Empty<IOrderServiceInterceptor>();
    }

    public Order CreateOrder(OrderRequest request)
    {
        if (_interceptors.Count == 0)
            return _inner.CreateOrder(request);

        var context = new CreateOrderMethodContext(request);
        try
        {
            // Before: ascending order (0 -> N)
            for (int i = 0; i < _interceptors.Count; i++)
                _interceptors[i].Before(context);

            var __result = _inner.CreateOrder(request);
            context.SetResult(__result);

            // After: descending order (N -> 0)
            for (int i = _interceptors.Count - 1; i >= 0; i--)
                _interceptors[i].After(context);

            return __result;
        }
        catch (Exception __ex)
        {
            // OnException: descending order (N -> 0)
            for (int i = _interceptors.Count - 1; i >= 0; i--)
                _interceptors[i].OnException(context, __ex);
            throw;
        }
    }
}
```

**Pipeline Ordering:**
- **Before**: Called in ascending order (`interceptors[0]` is outermost)
- **After**: Called in descending order (unwinding the stack)
- **OnException**: Called in descending order (unwinding the stack)

## Attributes

### `[GenerateProxy]`

Main attribute for marking interfaces or abstract classes for proxy generation.

**Target:** `interface` or `abstract class` (only top-level, non-generic)

**Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ProxyTypeName` | `string?` | `{ContractName}Proxy` | Name of the generated proxy class |
| `InterceptorMode` | `ProxyInterceptorMode` | `Single` | Interceptor support mode |
| `GenerateAsync` | `bool?` | Auto-detected | Generate async interceptor methods |
| `ForceAsync` | `bool` | `false` | Force async even if no async members detected |
| `Exceptions` | `ProxyExceptionPolicy` | `Rethrow` | Exception handling policy |

**Example:**
```csharp
[GenerateProxy(
    ProxyTypeName = "UserServiceLoggingProxy",
    InterceptorMode = ProxyInterceptorMode.Pipeline,
    Exceptions = ProxyExceptionPolicy.Rethrow)]
public partial interface IUserService { }
```

### `[ProxyIgnore]`

Marks a method or property to exclude from proxy generation.

**Target:** Methods, Properties

**Example:**
```csharp
[GenerateProxy]
public interface IUserService
{
    User GetUser(Guid id);
    
    [ProxyIgnore]
    void InternalMethod(); // Not included in proxy
}
```

## Interceptor Modes

### `ProxyInterceptorMode.None`

Pure delegation with no interceptor support. Lightest weight option.

**Use when:** You just need a simple wrapper without any cross-cutting concerns.

```csharp
[GenerateProxy(InterceptorMode = ProxyInterceptorMode.None)]
public partial interface ICalculator
{
    int Add(int a, int b);
}
```

### `ProxyInterceptorMode.Single`

Accepts a single interceptor instance. Good for simple scenarios.

**Use when:** You have one interceptor or want to compose interceptors manually.

```csharp
[GenerateProxy] // Single is default
public partial interface IUserService { }

// Usage:
var logger = new LoggingInterceptor();
var proxy = new UserServiceProxy(realService, logger);
```

### `ProxyInterceptorMode.Pipeline`

Accepts a list of interceptors with deterministic ordering.

**Use when:** You need multiple interceptors with clear execution order.

```csharp
[GenerateProxy(InterceptorMode = ProxyInterceptorMode.Pipeline)]
public partial interface IOrderService { }

// Usage:
var interceptors = new List<IOrderServiceInterceptor>
{
    new AuthenticationInterceptor(),  // [0] - outermost
    new LoggingInterceptor(),         // [1]
    new TimingInterceptor(),          // [2]
    new CachingInterceptor()          // [3] - innermost
};
var proxy = new OrderServiceProxy(realService, interceptors);
```

**Execution Flow:**
```
Request  →  Auth.Before → Log.Before → Time.Before → Cache.Before
         →  Real Method
         ←  Cache.After  ← Time.After ← Log.After  ← Auth.After   Response
```

## Exception Handling

### `ProxyExceptionPolicy.Rethrow` (Default)

OnException is called, then the exception is rethrown.

```csharp
[GenerateProxy(Exceptions = ProxyExceptionPolicy.Rethrow)]
public partial interface IUserService { }
```

**Behavior:**
```csharp
try
{
    // ... method execution
}
catch (Exception ex)
{
    _interceptor.OnException(context, ex);
    throw; // Exception propagates to caller
}
```

### `ProxyExceptionPolicy.Swallow`

OnException is called, but the exception is **not** rethrown. **Use with extreme caution.**

```csharp
[GenerateProxy(Exceptions = ProxyExceptionPolicy.Swallow)]
public partial interface IResilientService { }
```

**Behavior:**
```csharp
try
{
    // ... method execution
}
catch (Exception ex)
{
    _interceptor.OnException(context, ex);
    // Exception is swallowed - method returns default value
}
```

⚠️ **Warning:** Swallow mode can hide errors and cause unexpected behavior. Only use when:
- You have explicit error recovery logic in your interceptor
- The contract allows for null/default return values
- You log exceptions thoroughly

## Async Support

The generator automatically detects async members and generates async interceptor methods.

**Auto-detection triggers:**
- Any method returns `Task` or `ValueTask` (with or without generic type)
- Any method has a `CancellationToken` parameter

**Generated async methods use `ValueTask` for efficiency:**
```csharp
public interface IUserServiceInterceptor
{
    // Sync
    void Before(MethodContext context);
    void After(MethodContext context);
    void OnException(MethodContext context, Exception ex);

    // Async (generated when async members detected)
    ValueTask BeforeAsync(MethodContext context, CancellationToken ct);
    ValueTask AfterAsync(MethodContext context, CancellationToken ct);
    ValueTask OnExceptionAsync(MethodContext context, Exception ex, CancellationToken ct);
}
```

**Force Async:**
```csharp
[GenerateProxy(ForceAsync = true)]
public partial interface IFutureProofService
{
    void DoWork(); // No async yet, but interceptor will have async methods
}
```

## Supported Members

### Methods

✅ **Supported:**
- Void methods
- Methods with return values
- Async methods (`Task`, `ValueTask`, `Task<T>`, `ValueTask<T>`)
- Methods with `CancellationToken` parameters
- Methods with default parameters
- Generic methods (with constraints)

❌ **Not Supported (v1):**
- `ref` / `out` / `in` parameters (generates diagnostic)
- Events (generates PKPRX002)

### Properties

✅ **Supported:**
- Get-only properties
- Set-only properties
- Get/set properties
- Auto-properties
- Expression-bodied properties

**Generated forwarding:**
```csharp
public string Name 
{ 
    get => _inner.Name; 
    set => _inner.Name = value; 
}
```

⚠️ **Note:** Properties do **not** invoke interceptors. They are simple forwarders. To intercept property access, use explicit getter/setter methods instead.

### Abstract Classes

For abstract classes, only `virtual` and `abstract` members are proxied.

```csharp
[GenerateProxy]
public abstract partial class UserServiceBase
{
    // Proxied
    public abstract User GetUser(Guid id);
    public virtual void UpdateUser(User user) { }
    
    // NOT proxied (sealed/non-virtual)
    public void InternalMethod() { }
}
```

## Diagnostics

The generator provides actionable diagnostics for invalid usage:

| ID | Severity | Description |
|----|----------|-------------|
| **PKPRX001** | Error | Type marked `[GenerateProxy]` must be `partial` |
| **PKPRX002** | Error | Unsupported member kind (e.g., events not supported in v1) |
| **PKPRX003** | Warning | Member not accessible for proxy generation |
| **PKPRX004** | Error | Proxy type name conflicts with existing type |
| **PKPRX005** | Warning | Async member detected but async interception disabled |

### PKPRX001: Must Be Partial

```csharp
[GenerateProxy]
public interface IUserService { } // ❌ Error: must be partial

[GenerateProxy]
public partial interface IUserService { } // ✅ Correct
```

### PKPRX002: Unsupported Member

```csharp
[GenerateProxy]
public partial interface IUserService
{
    event EventHandler<UserEventArgs> UserChanged; // ❌ Error: Events not supported
}
```

**Fix:** Remove the event or use `[ProxyIgnore]` to exclude it.

### PKPRX003: Inaccessible Member

Generated when a member cannot be accessed from the proxy type (e.g., protected members on interface members).

### PKPRX004: Name Conflict

```csharp
[GenerateProxy(ProxyTypeName = "UserService")] // ❌ Conflicts with existing type
public partial interface IUserService { }

public class UserService { } // Existing type
```

**Fix:** Use a different `ProxyTypeName`.

### PKPRX005: Async Disabled

```csharp
[GenerateProxy(GenerateAsync = false)]
public partial interface IUserService
{
    Task<User> GetUserAsync(Guid id); // ⚠️ Warning: Async member but async disabled
}
```

**Fix:** Remove `GenerateAsync = false` or set `ForceAsync = true`.

## Implementing Interceptors

### Basic Interceptor

```csharp
public class LoggingInterceptor : IUserServiceInterceptor
{
    private readonly ILogger _logger;

    public LoggingInterceptor(ILogger logger) => _logger = logger;

    public void Before(MethodContext context)
    {
        _logger.LogInformation("Calling {Method} with args: {Args}", 
            context.MethodName, context.Arguments);
    }

    public void After(MethodContext context)
    {
        _logger.LogInformation("{Method} returned: {Result}", 
            context.MethodName, context.Result);
    }

    public void OnException(MethodContext context, Exception ex)
    {
        _logger.LogError(ex, "{Method} failed", context.MethodName);
    }

    // Async versions (if generated)
    public ValueTask BeforeAsync(MethodContext context, CancellationToken ct)
    {
        Before(context);
        return default;
    }

    public ValueTask AfterAsync(MethodContext context, CancellationToken ct)
    {
        After(context);
        return default;
    }

    public ValueTask OnExceptionAsync(MethodContext context, Exception ex, CancellationToken ct)
    {
        OnException(context, ex);
        return default;
    }
}
```

### Performance Timing Interceptor

```csharp
public class TimingInterceptor : IUserServiceInterceptor
{
    private readonly IMetrics _metrics;
    private readonly Dictionary<int, Stopwatch> _timers = new();

    public TimingInterceptor(IMetrics metrics) => _metrics = metrics;

    public void Before(MethodContext context)
    {
        var sw = Stopwatch.StartNew();
        _timers[context.GetHashCode()] = sw;
    }

    public void After(MethodContext context)
    {
        if (_timers.Remove(context.GetHashCode(), out var sw))
        {
            sw.Stop();
            _metrics.RecordDuration(context.MethodName, sw.Elapsed);
        }
    }

    public void OnException(MethodContext context, Exception ex)
    {
        _timers.Remove(context.GetHashCode());
    }

    // Async versions...
    public ValueTask BeforeAsync(MethodContext context, CancellationToken ct)
    {
        Before(context);
        return default;
    }

    public ValueTask AfterAsync(MethodContext context, CancellationToken ct)
    {
        After(context);
        return default;
    }

    public ValueTask OnExceptionAsync(MethodContext context, Exception ex, CancellationToken ct)
    {
        OnException(context, ex);
        return default;
    }
}
```

### Caching Interceptor (Advanced)

```csharp
public class CachingInterceptor : IUserServiceInterceptor
{
    private readonly IMemoryCache _cache;

    public CachingInterceptor(IMemoryCache cache) => _cache = cache;

    public void Before(MethodContext context)
    {
        // Check cache before method execution
        var cacheKey = $"{context.MethodName}:{string.Join(",", context.Arguments ?? Array.Empty<object>())}";
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            context.SetResult(cachedResult);
            // Note: You'd need a way to short-circuit execution
            // This is a simplified example
        }
    }

    public void After(MethodContext context)
    {
        // Cache result after execution
        var cacheKey = $"{context.MethodName}:{string.Join(",", context.Arguments ?? Array.Empty<object>())}";
        _cache.Set(cacheKey, context.Result, TimeSpan.FromMinutes(5));
    }

    public void OnException(MethodContext context, Exception ex) { }

    // Async versions omitted for brevity
}
```

## Real-World Example: Complete Pipeline

```csharp
[GenerateProxy(InterceptorMode = ProxyInterceptorMode.Pipeline)]
public partial interface IOrderService
{
    Task<Order> CreateOrderAsync(OrderRequest request, CancellationToken ct = default);
    Task<Order> GetOrderAsync(Guid orderId, CancellationToken ct = default);
}

// Setup
var authInterceptor = new AuthenticationInterceptor(authService);
var loggingInterceptor = new LoggingInterceptor(logger);
var timingInterceptor = new TimingInterceptor(metrics);
var cachingInterceptor = new CachingInterceptor(cache);

var interceptors = new List<IOrderServiceInterceptor>
{
    authInterceptor,    // [0] - Outermost: auth first
    loggingInterceptor, // [1] - Log after auth
    timingInterceptor,  // [2] - Time inner operations
    cachingInterceptor  // [3] - Innermost: cache closest to real call
};

var proxy = new OrderServiceProxy(realOrderService, interceptors);

// Usage
var order = await proxy.CreateOrderAsync(request, ct);
```

**Execution Flow:**
```
CreateOrderAsync called
├─ [0] Auth.BeforeAsync     → Verify user has permission
├─ [1] Log.BeforeAsync      → Log "Creating order..."
├─ [2] Time.BeforeAsync     → Start stopwatch
├─ [3] Cache.BeforeAsync    → Check cache (miss for create)
├─ Real CreateOrderAsync    → Execute actual method
├─ [3] Cache.AfterAsync     → Cache result
├─ [2] Time.AfterAsync      → Stop stopwatch, record metric
├─ [1] Log.AfterAsync       → Log "Order created: {orderId}"
└─ [0] Auth.AfterAsync      → Audit log
```

## Limitations

### Version 1 Limitations

- ❌ **Generic contracts** not supported
- ❌ **Nested types** not supported  
- ❌ **Events** not supported
- ❌ **ref/out/in parameters** not supported
- ❌ **Properties don't invoke interceptors** (simple forwarding only)

### Workarounds

**Generic contracts:**
```csharp
// Instead of:
[GenerateProxy]
public interface IRepository<T> { } // ❌ Not supported

// Use:
[GenerateProxy]
public partial interface IUserRepository // ✅ Concrete type
{
    User Get(Guid id);
}
```

**Properties:**
```csharp
// To intercept property access, use methods:
[GenerateProxy]
public partial interface IUserService
{
    // Instead of: string Name { get; set; }
    string GetName();
    void SetName(string name);
}
```

## Best Practices

1. **Use Pipeline mode for multiple concerns**
   ```csharp
   [GenerateProxy(InterceptorMode = ProxyInterceptorMode.Pipeline)]
   ```

2. **Order interceptors carefully**
   - Authentication first
   - Logging/auditing second
   - Caching innermost (closest to real call)

3. **Prefer `Rethrow` exception policy**
   - Swallow can hide bugs
   - Only use when you have explicit recovery logic

4. **Keep interceptors focused**
   - One concern per interceptor (SRP)
   - Compose multiple interceptors rather than one complex interceptor

5. **Use async throughout**
   - If any method is async, prefer async interceptor implementations
   - Avoid blocking in async code paths

6. **Test interceptors independently**
   ```csharp
   var context = new GetUserMethodContext(userId);
   interceptor.Before(context);
   // Assert expected behavior
   ```

7. **Consider performance**
   - Interceptors add overhead to every call
   - Use `InterceptorMode.None` if you don't need interception
   - Cache compiled expressions if building dynamic interceptors

## Troubleshooting

### "Type must be partial" (PKPRX001)

**Problem:** Interface/class is not marked `partial`.

**Solution:** Add `partial` keyword:
```csharp
[GenerateProxy]
public partial interface IUserService { }
```

### "Async member detected but async interception disabled" (PKPRX005)

**Problem:** Contract has async methods but `GenerateAsync = false`.

**Solution:** Remove the `GenerateAsync` property or set it to `true`.

### Generated proxy not found

**Problem:** Proxy class doesn't appear in IntelliSense.

**Solution:**
1. Rebuild the project (`dotnet build`)
2. Ensure `PatternKit.Generators` package is referenced
3. Check for generator diagnostics in build output
4. Verify type is `partial`

### Interceptor not being called

**Problem:** Interceptor methods aren't executing.

**Solution:**
1. Ensure you pass interceptor to proxy constructor:
   ```csharp
   var proxy = new UserServiceProxy(inner, interceptor);
   ```
2. Verify `InterceptorMode` is not `None`
3. Check interceptor is not `null`

### Performance issues

**Problem:** Proxies are slow.

**Solution:**
1. Profile your interceptors - they may be doing expensive work
2. Consider using `InterceptorMode.None` for hot paths
3. Cache reflection-based operations in interceptors
4. Use async methods properly (don't block)

## See Also

- [Decorator Generator](decorator.md) - For wrapping objects with additional behavior
- [Facade Generator](facade.md) - For simplifying complex subsystems
- [Factory Generators](factory-method.md) - For object creation patterns
- [Examples](../examples/) - Real-world usage examples

## Feedback

Found an issue or have a feature request? [Open an issue on GitHub](https://github.com/JerrettDavis/PatternKit/issues).
