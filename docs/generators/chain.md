# Chain Pattern Generator

The Chain Pattern Generator automatically creates handler orchestration methods for chain-of-responsibility and pipeline patterns. It supports two models: **Responsibility** (first-match wins) and **Pipeline** (middleware wrapping).

## Overview

The generator produces:

- **Handle method** that invokes the chain and returns a result
- **TryHandle method** (Responsibility model) that returns a bool indicating success
- **Pipeline wrapping** (Pipeline model) that nests handlers around a terminal
- **Deterministic handler ordering** based on explicit Order values
- **Zero runtime overhead** through source generation

## Quick Start

### 1. Responsibility Model

Mark your class with `[Chain]` and define handlers that return `bool`:

```csharp
using PatternKit.Generators.Chain;

[Chain(Model = ChainModel.Responsibility)]
public partial class RequestRouter
{
    [ChainHandler(0)]
    private bool TryHandleGet(in Request input, out string output)
    {
        if (input.Method == "GET") { output = "OK"; return true; }
        output = default!; return false;
    }

    [ChainHandler(1)]
    private bool TryHandlePost(in Request input, out string output)
    {
        if (input.Method == "POST") { output = "Created"; return true; }
        output = default!; return false;
    }

    [ChainDefault]
    private string HandleNotFound(in Request input) => "Not Found";
}
```

Generated usage:

```csharp
var router = new RequestRouter();
var req = new Request("GET", "/users");
string result = router.Handle(in req);       // "OK"
bool ok = router.TryHandle(in req, out var r); // true, r = "OK"
```

### 2. Pipeline Model

Use `ChainModel.Pipeline` with `Func<TIn, TOut> next` parameters:

```csharp
[Chain(Model = ChainModel.Pipeline)]
public partial class MiddlewarePipeline
{
    [ChainHandler(0)]
    private string Logging(in string input, Func<string, string> next)
        => "[LOG] " + next(input);

    [ChainHandler(1)]
    private string Auth(in string input, Func<string, string> next)
        => input.StartsWith("DENY") ? "403" : next(input);

    [ChainTerminal]
    private string Process(in string input) => "200: " + input;
}
```

Generated usage:

```csharp
var pipeline = new MiddlewarePipeline();
string result = pipeline.Handle(in message);
// Order=0 (Logging) wraps Order=1 (Auth) wraps Terminal (Process)
```

## Attributes

| Attribute | Target | Description |
|---|---|---|
| `[Chain]` | Class/Struct | Marks the type as a chain host |
| `[ChainHandler(order)]` | Method | Marks a method as a handler with execution order |
| `[ChainDefault]` | Method | Fallback handler for Responsibility model |
| `[ChainTerminal]` | Method | Innermost handler for Pipeline model |

### ChainAttribute Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Model` | `ChainModel` | `Responsibility` | Chain execution model |
| `HandleMethodName` | `string` | `"Handle"` | Name of generated handle method |
| `TryHandleMethodName` | `string` | `"TryHandle"` | Name of generated try-handle method |
| `HandleAsyncMethodName` | `string` | `"HandleAsync"` | Name of generated async method |
| `GenerateAsync` | `bool` | `false` | Enable async method generation |
| `ForceAsync` | `bool` | `false` | Force async even with sync handlers |

### ChainModel Enum

| Value | Description |
|---|---|
| `Responsibility` | First handler that returns `true` wins |
| `Pipeline` | Handlers wrap each other; terminal is innermost |

## Handler Signatures

### Responsibility Model

```csharp
bool TryHandleX(in TIn input, out TOut output)
```

### Pipeline Model

```csharp
TOut HandleX(in TIn input, Func<TIn, TOut> next)
```

### Default Handler (Responsibility)

```csharp
TOut DefaultHandler(in TIn input)
```

### Terminal Handler (Pipeline)

```csharp
TOut Terminal(in TIn input)
```

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| PKCH001 | Error | Type marked with `[Chain]` must be partial |
| PKCH002 | Error | No handlers found (at least one `[ChainHandler]` required) |
| PKCH003 | Error | Duplicate handler Order values |
| PKCH004 | Error | Invalid handler method signature |
| PKCH005 | Error | Missing `[ChainTerminal]` for Pipeline model |
| PKCH006 | Error | Multiple `[ChainTerminal]` methods |
| PKCH007 | Warning | Missing `[ChainDefault]` for Responsibility model |
| PKCH008 | Error | Async handler but async generation not enabled |

## Best Practices

- **Use Responsibility model** for dispatch-style chains (request routing, validation)
- **Use Pipeline model** for middleware-style chains (logging, auth, compression)
- **Keep handler order explicit** to make execution flow clear
- **Always provide a `[ChainDefault]`** in Responsibility chains for completeness
- **Order=0 is outermost** in Pipeline model (executes first, wraps everything)

## See Also

- [Chain Generator Example](../examples/chain-generator-demo.md)
- [Composer Pattern Generator](composer.md) (similar pipeline concept)
