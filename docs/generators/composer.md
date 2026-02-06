# Composer Generator

## Overview

The **Composer Generator** creates deterministic pipeline compositions from ordered step methods. It eliminates boilerplate by automatically generating `Invoke` and `InvokeAsync` methods that compose your pipeline steps in order, wrapping each step around the next until reaching a terminal handler.

## When to Use

Use the Composer generator when you need to:

- **Build middleware-style pipelines**: Wrap handlers with pre/post logic
- **Create deterministic execution order**: Steps execute in well-defined order based on `Order` values
- **Support async pipelines**: Automatically generates async variants when needed
- **Avoid runtime reflection**: Pipeline is composed at compile time

## Installation

The generator is included in the `PatternKit.Generators` package:

```bash
dotnet add package PatternKit.Generators
```

## Quick Start

```csharp
using PatternKit.Generators.Composer;

[Composer]
public partial class RequestPipeline
{
    [ComposeStep(0)]
    public TOut Logging<TIn, TOut>(in TIn input, Func<TIn, TOut> next)
    {
        Console.WriteLine($"Processing: {input}");
        var result = next(input);
        Console.WriteLine($"Result: {result}");
        return result;
    }

    [ComposeStep(1)]
    public TOut Validation<TIn, TOut>(in TIn input, Func<TIn, TOut> next)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        return next(input);
    }

    [ComposeTerminal]
    public string Handle(in string input) => input.ToUpperInvariant();
}
```

Generated:
```csharp
public partial class RequestPipeline
{
    public string Invoke(in string input)
    {
        Func<string, string> pipeline = (arg) => Handle(in arg);
        pipeline = (arg) => Validation(in arg, pipeline);
        pipeline = (arg) => Logging(in arg, pipeline);
        return pipeline(input);
    }
}
```

Usage:
```csharp
var pipeline = new RequestPipeline();
var result = pipeline.Invoke("hello"); // Logs, validates, then processes
```

## Step Method Signature

Pipeline steps must follow this signature pattern:

### Synchronous Steps
```csharp
TOut StepName(in TIn input, Func<TIn, TOut> next)
```

- `input`: The pipeline input, passed by `in` reference
- `next`: Delegate to call the next step in the pipeline
- Returns: The output type

### Async Steps
```csharp
ValueTask<TOut> StepNameAsync(TIn input, Func<TIn, ValueTask<TOut>> next, CancellationToken ct)
```

- `input`: The pipeline input (not `in` for async)
- `next`: Async delegate to call the next step
- `ct`: CancellationToken for cooperative cancellation

## Terminal Method Signature

The terminal is the final handler that doesn't call `next`:

### Synchronous Terminal
```csharp
TOut TerminalName(in TIn input)
```

### Async Terminal
```csharp
ValueTask<TOut> TerminalNameAsync(TIn input, CancellationToken ct)
```

## Attributes

### `[Composer]`

Main attribute for marking pipeline host types.

| Property | Type | Default | Description |
|---|---|---|---|
| `InvokeMethodName` | `string` | `"Invoke"` | Name of generated sync method |
| `InvokeAsyncMethodName` | `string` | `"InvokeAsync"` | Name of generated async method |
| `GenerateAsync` | `bool?` | `null` | Explicit async control; null = infer from steps |
| `ForceAsync` | `bool` | `false` | Force async generation even if all steps are sync |
| `WrapOrder` | `ComposerWrapOrder` | `OuterFirst` | Determines wrapping order |

### `[ComposeStep(order)]`

Marks a method as a pipeline step.

| Property | Type | Default | Description |
|---|---|---|---|
| `Order` | `int` | (required) | Position in pipeline composition |
| `Name` | `string?` | Method name | Optional name for diagnostics |

### `[ComposeTerminal]`

Marks a method as the pipeline terminal (final handler).

### `[ComposeIgnore]`

Excludes a method from pipeline composition.

## Wrap Order

The `WrapOrder` determines how steps compose:

### OuterFirst (Default)
Lower `Order` values wrap higher values:
```
Order=0 → Order=1 → Order=2 → Terminal
```
Step 0 executes first, wrapping all others.

### InnerFirst
Higher `Order` values wrap lower values:
```
Order=2 → Order=1 → Order=0 → Terminal
```
Step with highest order executes first.

## Async Support

The generator automatically detects async methods and generates appropriate async pipelines:

```csharp
[Composer]
public partial class AsyncPipeline
{
    [ComposeStep(0)]
    public async ValueTask<TOut> TimingAsync<TIn, TOut>(
        TIn input, 
        Func<TIn, ValueTask<TOut>> next, 
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var result = await next(input);
        Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms");
        return result;
    }

    [ComposeTerminal]
    public async ValueTask<string> ProcessAsync(string input, CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return input.ToUpperInvariant();
    }
}
```

Generated `InvokeAsync`:
```csharp
public ValueTask<string> InvokeAsync(string input, CancellationToken ct = default)
{
    Func<string, ValueTask<string>> pipeline = (arg) => ProcessAsync(arg, ct);
    pipeline = (arg) => TimingAsync(arg, pipeline, ct);
    return pipeline(input);
}
```

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| **PKCOM001** | Error | Type marked `[Composer]` must be `partial` |
| **PKCOM002** | Error | No methods marked with `[ComposeStep]` found |
| **PKCOM003** | Error | Multiple steps have the same Order value |
| **PKCOM004** | Error | Missing `[ComposeTerminal]` method |
| **PKCOM005** | Error | Multiple `[ComposeTerminal]` methods found |
| **PKCOM006** | Error | Invalid step method signature |
| **PKCOM007** | Error | Invalid terminal method signature |
| **PKCOM008** | Error | Async step detected but async generation disabled |
| **PKCOM009** | Warning | Async method missing CancellationToken parameter |

## Best Practices

### 1. Use Consistent Step Order
Define steps with well-spaced order values (0, 10, 20) to allow insertions:

```csharp
[ComposeStep(0)]   // Outer: Logging
[ComposeStep(10)]  // Validation
[ComposeStep(20)]  // Authorization
[ComposeStep(30)]  // Caching
[ComposeTerminal]  // Handler
```

### 2. Keep Pipelines Focused
Each step should have a single responsibility. Split complex pipelines into multiple composers.

### 3. Handle Errors in Steps
Steps can catch, log, and rethrow or transform exceptions:

```csharp
[ComposeStep(0)]
public TOut ErrorHandling<TIn, TOut>(in TIn input, Func<TIn, TOut> next)
{
    try
    {
        return next(input);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Pipeline failed");
        throw;
    }
}
```

### 4. Prefer Struct Pipelines for Performance
For hot paths, use `struct` pipeline hosts to avoid heap allocation:

```csharp
[Composer]
public partial struct FastPipeline { /* ... */ }
```

Note: Struct pipelines use local functions instead of lambdas to avoid capturing `this`.

## Examples

### Middleware Pipeline

```csharp
[Composer]
public partial class HttpMiddleware
{
    [ComposeStep(0, Name = "Logging")]
    public Response Logging(in Request req, Func<Request, Response> next)
    {
        Console.WriteLine($"Request: {req.Path}");
        var response = next(req);
        Console.WriteLine($"Response: {response.StatusCode}");
        return response;
    }

    [ComposeStep(1, Name = "Auth")]
    public Response Authentication(in Request req, Func<Request, Response> next)
    {
        if (!req.IsAuthenticated)
            return new Response { StatusCode = 401 };
        return next(req);
    }

    [ComposeTerminal]
    public Response Handle(in Request req)
        => new Response { StatusCode = 200, Body = "OK" };
}
```

### Transaction Pipeline

```csharp
[Composer]
public partial class TransactionPipeline
{
    private readonly IDbConnection _db;

    public TransactionPipeline(IDbConnection db) => _db = db;

    [ComposeStep(0)]
    public T WithTransaction<TIn, T>(in TIn input, Func<TIn, T> next)
    {
        using var tx = _db.BeginTransaction();
        try
        {
            var result = next(input);
            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    [ComposeTerminal]
    public int SaveOrder(in Order order)
    {
        // Save to database
        return _db.Execute("INSERT INTO Orders ...", order);
    }
}
```

## Troubleshooting

### PKCOM001: Must be partial

**Cause:** Target type is not marked `partial`.

**Fix:**
```csharp
// ❌ Wrong
[Composer]
public class MyPipeline { }

// ✅ Correct
[Composer]
public partial class MyPipeline { }
```

### PKCOM003: Duplicate step order

**Cause:** Multiple steps have the same `Order` value.

**Fix:** Ensure each step has a unique `Order`:
```csharp
[ComposeStep(0)] // ✅
public TOut Step1(...) { }

[ComposeStep(1)] // ✅ Different order
public TOut Step2(...) { }
```

### PKCOM006: Invalid step signature

**Cause:** Step method doesn't match expected signature.

**Fix:** Ensure step methods have:
- First parameter: `in TIn input`
- Second parameter: `Func<TIn, TOut> next`
- Return type matching `TOut`

## See Also

- [Builder Generator](builder.md)
- [Template Method Generator](template-method-generator.md)
- [Facade Generator](facade.md)
