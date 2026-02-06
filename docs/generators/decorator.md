# Decorator Generator

## Overview

The **Decorator Generator** creates GoF-compliant decorator base classes that forward all members to an inner instance. It eliminates the boilerplate of implementing every interface member manually when you only need to override a few. The generator also provides optional composition helpers for building decorator chains.

## When to Use

Use the Decorator generator when you need to:

- **Add behavior to existing types**: Wrap objects with additional functionality (caching, logging, validation)
- **Avoid inheritance explosion**: Combine decorators instead of creating subclass combinations
- **Maintain interface contracts**: Decorators implement the same interface as the wrapped type
- **Compose behaviors at runtime**: Build decorator chains dynamically

## Installation

The generator is included in the `PatternKit.Generators` package:

```bash
dotnet add package PatternKit.Generators
```

## Quick Start

```csharp
using PatternKit.Generators.Decorator;

[GenerateDecorator]
public interface IFileStorage
{
    string ReadFile(string path);
    void WriteFile(string path, string content);
    bool FileExists(string path);
}
```

Generated:
```csharp
public abstract partial class FileStorageDecoratorBase : IFileStorage
{
    protected FileStorageDecoratorBase(IFileStorage inner)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    protected IFileStorage Inner { get; }

    public virtual string ReadFile(string path) => Inner.ReadFile(path);
    public virtual void WriteFile(string path, string content) => Inner.WriteFile(path, content);
    public virtual bool FileExists(string path) => Inner.FileExists(path);
}

public static partial class FileStorageDecorators
{
    public static IFileStorage Compose(
        IFileStorage inner,
        params Func<IFileStorage, IFileStorage>[] decorators)
    {
        // Applies decorators in order
    }
}
```

## Creating Decorators

Inherit from the generated base class and override only the methods you need:

```csharp
public class CachingFileStorage : FileStorageDecoratorBase
{
    private readonly Dictionary<string, string> _cache = new();

    public CachingFileStorage(IFileStorage inner) : base(inner) { }

    public override string ReadFile(string path)
    {
        if (_cache.TryGetValue(path, out var cached))
            return cached;

        var content = base.ReadFile(path);
        _cache[path] = content;
        return content;
    }

    public override void WriteFile(string path, string content)
    {
        _cache.Remove(path); // Invalidate cache
        base.WriteFile(path, content);
    }
}

public class LoggingFileStorage : FileStorageDecoratorBase
{
    public LoggingFileStorage(IFileStorage inner) : base(inner) { }

    public override string ReadFile(string path)
    {
        Console.WriteLine($"Reading: {path}");
        return base.ReadFile(path);
    }
}
```

## Composing Decorators

Use the generated composition helper to build decorator chains:

```csharp
var storage = FileStorageDecorators.Compose(
    new InMemoryFileStorage(),
    inner => new LoggingFileStorage(inner),
    inner => new CachingFileStorage(inner),
    inner => new RetryFileStorage(inner)
);
```

Decorators are applied in array order:
- `decorators[0]` (Logging) is the **outermost** decorator
- `decorators[^1]` (Retry) is the **innermost** decorator

## Attributes

### `[GenerateDecorator]`

Main attribute for marking contracts to generate decorator bases for.

| Property | Type | Default | Description |
|---|---|---|---|
| `BaseTypeName` | `string?` | `{Name}DecoratorBase` | Name of generated base class |
| `HelpersTypeName` | `string?` | `{Name}Decorators` | Name of generated helper class |
| `Composition` | `DecoratorCompositionMode` | `HelpersOnly` | Whether to generate composition helpers |

### `[DecoratorIgnore]`

Marks a contract member to be forwarded but **not virtual**. Use for members that should never be overridden.

```csharp
[GenerateDecorator]
public interface IRepository
{
    [DecoratorIgnore]
    string ConnectionString { get; } // Forwarded but sealed

    Task<Entity> GetAsync(int id);
}
```

## Supported Contract Types

The generator supports:

| Contract Type | Description |
|---|---|
| **Interfaces** | Most common use case |
| **Abstract classes** | Virtual/abstract members are forwarded |

**Not supported in v1:**
- Generic contracts (`IRepository<T>`)
- Nested types
- Events (PKDEC002)
- Indexers (PKDEC002)
- Generic methods on the contract

## Async Support

Async methods are forwarded directly without `async/await` to avoid unnecessary state machine allocation:

```csharp
[GenerateDecorator]
public interface IDataService
{
    Task<Data> GetDataAsync(int id, CancellationToken ct);
}
```

Generated:
```csharp
public virtual Task<Data> GetDataAsync(int id, CancellationToken ct)
    => Inner.GetDataAsync(id, ct);
```

Your decorator can still use `async/await` when needed:

```csharp
public class RetryDataService : DataServiceDecoratorBase
{
    public override async Task<Data> GetDataAsync(int id, CancellationToken ct)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                return await base.GetDataAsync(id, ct);
            }
            catch when (i < 2)
            {
                await Task.Delay(100, ct);
            }
        }
        throw new InvalidOperationException("Retries exhausted");
    }
}
```

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| **PKDEC001** | Error | Unsupported target type (not interface or abstract class) |
| **PKDEC002** | Error | Unsupported member kind (events, indexers, generic methods) |
| **PKDEC003** | Error | Generated type name conflicts with existing type |
| **PKDEC004** | Warning | Member is inaccessible for forwarding |
| **PKDEC005** | Error | Generic contracts are not supported |
| **PKDEC006** | Error | Nested types are not supported |

## Best Practices

### 1. Always Call Base
When overriding, call the base implementation to maintain the decorator chain:

```csharp
public override void Save(Entity entity)
{
    Validate(entity);      // Pre-processing
    base.Save(entity);     // Delegate to inner
    LogSaved(entity);      // Post-processing
}
```

### 2. Keep Decorators Focused
Each decorator should add exactly one concern (single responsibility):

```csharp
// ✅ Good: Single concern per decorator
public class CachingRepository : RepositoryDecoratorBase { }
public class LoggingRepository : RepositoryDecoratorBase { }

// ❌ Bad: Multiple concerns in one decorator
public class CachingAndLoggingRepository : RepositoryDecoratorBase { }
```

### 3. Document Decorator Order
When composition order matters, document it:

```csharp
/// <summary>
/// Recommended order: Logging → Caching → Retry → Base
/// - Logging sees all requests
/// - Caching reduces retries
/// - Retries only for cache misses
/// </summary>
```

### 4. Use Factory Methods for Complex Composition

```csharp
public static class StorageFactory
{
    public static IFileStorage CreateProduction(IFileStorage baseStorage)
        => FileStorageDecorators.Compose(
            baseStorage,
            inner => new LoggingFileStorage(inner),
            inner => new CachingFileStorage(inner),
            inner => new RetryFileStorage(inner, maxRetries: 5)
        );
}
```

## Examples

### Caching Decorator

```csharp
[GenerateDecorator]
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken ct);
    Task SaveAsync(User user, CancellationToken ct);
}

public class CachingUserRepository : UserRepositoryDecoratorBase
{
    private readonly IMemoryCache _cache;

    public CachingUserRepository(IUserRepository inner, IMemoryCache cache)
        : base(inner) => _cache = cache;

    public override async Task<User?> GetByIdAsync(int id, CancellationToken ct)
    {
        var key = $"user:{id}";
        if (_cache.TryGetValue(key, out User? cached))
            return cached;

        var user = await base.GetByIdAsync(id, ct);
        if (user != null)
            _cache.Set(key, user, TimeSpan.FromMinutes(5));

        return user;
    }

    public override async Task SaveAsync(User user, CancellationToken ct)
    {
        _cache.Remove($"user:{user.Id}"); // Invalidate
        await base.SaveAsync(user, ct);
    }
}
```

### Logging Decorator

```csharp
public class LoggingUserRepository : UserRepositoryDecoratorBase
{
    private readonly ILogger<LoggingUserRepository> _logger;

    public LoggingUserRepository(IUserRepository inner, ILogger<LoggingUserRepository> logger)
        : base(inner) => _logger = logger;

    public override async Task<User?> GetByIdAsync(int id, CancellationToken ct)
    {
        _logger.LogInformation("Getting user {UserId}", id);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await base.GetByIdAsync(id, ct);
            _logger.LogInformation("Got user {UserId} in {Elapsed}ms", id, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user {UserId}", id);
            throw;
        }
    }
}
```

### Abstract Class Contract

```csharp
[GenerateDecorator]
public abstract class HttpHandler
{
    public abstract Task<Response> HandleAsync(Request request, CancellationToken ct);
    public virtual string Name => GetType().Name;
}

public class TimingHandler : HttpHandlerDecoratorBase
{
    public TimingHandler(HttpHandler inner) : base(inner) { }

    public override async Task<Response> HandleAsync(Request request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var response = await base.HandleAsync(request, ct);
        response.Headers["X-Timing"] = sw.ElapsedMilliseconds.ToString();
        return response;
    }
}
```

## Troubleshooting

### PKDEC001: Unsupported target type

**Cause:** Contract is not an interface or abstract class.

**Fix:** Use `interface` or `abstract class`:
```csharp
// ❌ Wrong
[GenerateDecorator]
public class ConcreteService { }

// ✅ Correct
[GenerateDecorator]
public interface IService { }
```

### PKDEC002: Unsupported member kind

**Cause:** Contract contains events, indexers, or generic methods.

**Fix:** Remove unsupported members or use a wrapper interface:
```csharp
// ❌ Events not supported
public interface INotifier
{
    event EventHandler Changed; // PKDEC002
}

// ✅ Use methods instead
public interface INotifier
{
    void Subscribe(Action<EventArgs> handler);
}
```

### PKDEC005: Generic contracts not supported

**Cause:** Contract has type parameters.

**Fix:** Use non-generic contract or wait for v2:
```csharp
// ❌ Generic contracts not supported
[GenerateDecorator]
public interface IRepository<T> { }

// ✅ Use specific types
[GenerateDecorator]
public interface IUserRepository { }
```

## See Also

- [Proxy Generator](proxy.md) — For access control rather than behavior addition
- [Facade Generator](facade.md) — For simplifying subsystem access
- [Patterns: Decorator](../patterns/structural/decorator/index.md)
