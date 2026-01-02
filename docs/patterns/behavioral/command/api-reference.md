# Command Pattern API Reference

Complete API documentation for the Command pattern in PatternKit.

## Namespace

```csharp
using PatternKit.Behavioral.Command;
```

---

## Command\<TCtx\>

Encapsulates an action with optional undo capability.

```csharp
public sealed class Command<TCtx>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TCtx` | The context type the command operates on |

### Delegates

#### `Exec`

```csharp
public delegate ValueTask Exec(in TCtx ctx, CancellationToken ct);
```

Asynchronous execution delegate. Return `default` for synchronous completion.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `HasUndo` | `bool` | Indicates whether this command has an undo handler |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Execute(in TCtx ctx, CancellationToken ct)` | `ValueTask` | Executes the command |
| `Execute(in TCtx ctx)` | `ValueTask` | Executes with default cancellation token |
| `TryUndo(in TCtx ctx, CancellationToken ct, out ValueTask undoTask)` | `bool` | Attempts to undo; returns false if no undo |
| `TryUndo(in TCtx ctx, out ValueTask undoTask)` | `bool` | Attempts to undo with default token |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new command builder |
| `Macro()` | `MacroBuilder` | Creates a macro (composite) command builder |

### Example

```csharp
var command = Command<Counter>.Create()
    .Do(c => c.Value++)
    .Undo(c => c.Value--)
    .Build();

await command.Execute(counter);

if (command.TryUndo(counter, out var undoTask))
    await undoTask;
```

---

## Command\<TCtx\>.Builder

Fluent builder for configuring a command.

```csharp
public sealed class Builder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Do(Exec handler)` | `Builder` | Sets the async Do handler (required) |
| `Do(Action<TCtx> handler)` | `Builder` | Sets a sync Do handler |
| `Undo(Exec handler)` | `Builder` | Sets the async Undo handler (optional) |
| `Undo(Action<TCtx> handler)` | `Builder` | Sets a sync Undo handler |
| `Build()` | `Command<TCtx>` | Builds the immutable command |

### Exceptions

| Method | Exception | Condition |
|--------|-----------|-----------|
| `Build` | `InvalidOperationException` | No Do handler was provided |

### Example

```csharp
// Async command
var asyncCmd = Command<FileContext>.Create()
    .Do(async (in FileContext ctx, CancellationToken ct) =>
    {
        await File.WriteAllTextAsync(ctx.Path, ctx.Content, ct);
    })
    .Undo(async (in FileContext ctx, CancellationToken ct) =>
    {
        File.Delete(ctx.Path);
    })
    .Build();

// Sync command
var syncCmd = Command<Counter>.Create()
    .Do(c => c.Value++)
    .Undo(c => c.Value--)
    .Build();
```

---

## Command\<TCtx\>.MacroBuilder

Builder for composite commands that execute in sequence.

```csharp
public sealed class MacroBuilder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Add(Command<TCtx> cmd)` | `MacroBuilder` | Adds a sub-command |
| `AddIf(bool condition, Command<TCtx> cmd)` | `MacroBuilder` | Adds a sub-command conditionally |
| `Build()` | `Command<TCtx>` | Builds the macro command |

### Execution Semantics

- **Execute**: Runs sub-commands in registration order
- **Undo**: Runs undo in reverse order, skipping commands without undo
- **Failure**: Stops on first exception; does NOT auto-undo previous commands

### Example

```csharp
var compile = Command<BuildCtx>.Create()
    .Do(c => c.Steps.Add("compile"))
    .Undo(c => c.Steps.Remove("compile"))
    .Build();

var test = Command<BuildCtx>.Create()
    .Do(c => c.Steps.Add("test"))
    .Undo(c => c.Steps.Remove("test"))
    .Build();

var package = Command<BuildCtx>.Create()
    .Do(c => c.Steps.Add("package"))
    .Build(); // No undo

var pipeline = Command<BuildCtx>.Macro()
    .Add(compile)
    .AddIf(runTests, test)
    .Add(package)
    .Build();

// Execute: compile → test (if runTests) → package
await pipeline.Execute(context);

// Undo: test → compile (reverse order, skips package)
if (pipeline.TryUndo(context, out var undo))
    await undo;
```

---

## Thread Safety

| Component | Thread-Safe |
|-----------|-------------|
| `Builder` | No - use from single thread |
| `MacroBuilder` | No - use from single thread |
| `Command<TCtx>` | Yes - immutable after build |

---

## Performance Characteristics

| Aspect | Description |
|--------|-------------|
| Execute (sync) | Allocation-free via ValueTask |
| Execute (async) | Single allocation for state machine |
| Macro (sync) | Allocation-free when all complete synchronously |
| Macro (async) | One allocation per async step |

---

## Complete Example

```csharp
using PatternKit.Behavioral.Command;

// Context type
public class DocumentContext
{
    public List<string> Lines { get; } = new();
    public string? LastDeleted { get; set; }
}

// Create commands
var insertLine = Command<DocumentContext>.Create()
    .Do(ctx => ctx.Lines.Add("New line"))
    .Undo(ctx => ctx.Lines.RemoveAt(ctx.Lines.Count - 1))
    .Build();

var deleteLine = Command<DocumentContext>.Create()
    .Do(ctx =>
    {
        if (ctx.Lines.Count > 0)
        {
            ctx.LastDeleted = ctx.Lines[^1];
            ctx.Lines.RemoveAt(ctx.Lines.Count - 1);
        }
    })
    .Undo(ctx =>
    {
        if (ctx.LastDeleted != null)
        {
            ctx.Lines.Add(ctx.LastDeleted);
            ctx.LastDeleted = null;
        }
    })
    .Build();

// Use commands
var doc = new DocumentContext();

await insertLine.Execute(doc);  // Lines: ["New line"]
await insertLine.Execute(doc);  // Lines: ["New line", "New line"]
await deleteLine.Execute(doc);  // Lines: ["New line"], LastDeleted: "New line"

if (deleteLine.TryUndo(doc, out var undo))
    await undo;                 // Lines: ["New line", "New line"]

// Macro example
var batch = Command<DocumentContext>.Macro()
    .Add(insertLine)
    .Add(insertLine)
    .Add(insertLine)
    .Build();

await batch.Execute(doc);      // Adds 3 lines

if (batch.TryUndo(doc, out var batchUndo))
    await batchUndo;           // Removes 3 lines (reverse order)
```

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [Real-World Examples](real-world-examples.md)
