# Command Pattern Guide

This guide covers everything you need to know about using the Command pattern in PatternKit.

## Overview

Command encapsulates an action and its inverse (undo) as a first-class object. This enables operations to be queued, logged, composed into macros, and reversed. PatternKit's implementation is allocation-light, using ValueTask for efficient sync/async execution.

## Getting Started

### Installation

The Command pattern is included in the core PatternKit package:

```csharp
using PatternKit.Behavioral.Command;
```

### Basic Usage

Create a command with Do and optional Undo:

```csharp
public sealed class Counter { public int Value; }

var increment = Command<Counter>.Create()
    .Do(c => c.Value++)
    .Undo(c => c.Value--)
    .Build();

var counter = new Counter();
await increment.Execute(counter);  // Value: 1
await increment.Execute(counter);  // Value: 2

if (increment.TryUndo(counter, out var undoTask))
    await undoTask;                // Value: 1
```

## Core Concepts

### Do and Undo

Every command requires a Do action. Undo is optional:

```csharp
// Command with undo
var addItem = Command<List<string>>.Create()
    .Do(list => list.Add("item"))
    .Undo(list => list.Remove("item"))
    .Build();

// Command without undo
var logMessage = Command<string>.Create()
    .Do(msg => Console.WriteLine(msg))
    .Build();

logMessage.HasUndo; // false
```

### TryUndo Pattern

Check if undo is available before calling:

```csharp
if (command.HasUndo)
{
    // Safe to call TryUndo
}

// Or use the try pattern directly
if (command.TryUndo(context, out var undoTask))
{
    await undoTask;
}
else
{
    Console.WriteLine("No undo available");
}
```

### Async Commands

Commands support async operations with cancellation:

```csharp
var saveFile = Command<FileContext>.Create()
    .Do(async (in FileContext ctx, CancellationToken ct) =>
    {
        await File.WriteAllTextAsync(ctx.Path, ctx.Content, ct);
    })
    .Undo(async (in FileContext ctx, CancellationToken ct) =>
    {
        if (File.Exists(ctx.Path))
            File.Delete(ctx.Path);
    })
    .Build();

await saveFile.Execute(fileContext, cancellationToken);
```

### The `in` Parameter

Commands use `in` parameters to avoid copying large structs:

```csharp
// For value types, use `in` to avoid copies
.Do((in LargeStruct ctx, CancellationToken ct) =>
{
    // ctx is passed by readonly reference
    return default; // ValueTask
})

// For reference types, it's optional but consistent
.Do((in MyClass ctx, CancellationToken ct) => { ... })
```

## Macro Commands

Macros combine multiple commands into an ordered sequence:

```csharp
var compile = Command<BuildCtx>.Create()
    .Do(c => c.Log.Add("compile"))
    .Undo(c => c.Log.Add("undo-compile"))
    .Build();

var test = Command<BuildCtx>.Create()
    .Do(c => c.Log.Add("test"))
    .Undo(c => c.Log.Add("undo-test"))
    .Build();

var package = Command<BuildCtx>.Create()
    .Do(c => c.Log.Add("package"))
    .Build(); // No undo

var pipeline = Command<BuildCtx>.Macro()
    .Add(compile)
    .Add(test)
    .Add(package)
    .Build();

var ctx = new BuildCtx(new List<string>());
await pipeline.Execute(ctx);
// Log: ["compile", "test", "package"]

if (pipeline.TryUndo(ctx, out var undo))
    await undo;
// Log adds: ["undo-test", "undo-compile"] (reverse order, skips package)
```

### Conditional Macro Steps

Add commands conditionally:

```csharp
bool runTests = Environment.GetEnvironmentVariable("SKIP_TESTS") == null;
bool createDocs = true;

var pipeline = Command<BuildCtx>.Macro()
    .Add(compileCommand)
    .AddIf(runTests, testCommand)
    .AddIf(createDocs, docsCommand)
    .Add(packageCommand)
    .Build();
```

## Common Patterns

### Optimistic UI

Apply changes immediately, undo if server rejects:

```csharp
var addToCart = Command<CartContext>.Create()
    .Do(ctx =>
    {
        ctx.LocalCart.Add(ctx.Item);
        ctx.UiRefresh();
    })
    .Undo(ctx =>
    {
        ctx.LocalCart.Remove(ctx.Item);
        ctx.UiRefresh();
    })
    .Build();

// Apply optimistically
await addToCart.Execute(cartContext);

// Server validates
var serverResult = await api.AddToCartAsync(cartContext.Item);
if (!serverResult.Success)
{
    // Rollback
    if (addToCart.TryUndo(cartContext, out var undo))
        await undo;
}
```

### Editor Actions

Implement undo/redo for document editing:

```csharp
public class Editor
{
    private readonly Stack<Command<Document>> _undoStack = new();
    private readonly Stack<Command<Document>> _redoStack = new();
    private Document _document;

    public async Task ExecuteCommand(Command<Document> cmd)
    {
        await cmd.Execute(_document);
        if (cmd.HasUndo)
        {
            _undoStack.Push(cmd);
            _redoStack.Clear();
        }
    }

    public async Task Undo()
    {
        if (_undoStack.TryPop(out var cmd))
        {
            if (cmd.TryUndo(_document, out var undo))
            {
                await undo;
                _redoStack.Push(cmd);
            }
        }
    }

    public async Task Redo()
    {
        if (_redoStack.TryPop(out var cmd))
        {
            await cmd.Execute(_document);
            _undoStack.Push(cmd);
        }
    }
}
```

### Transaction-Like Behavior

Execute a macro and rollback on failure:

```csharp
var transaction = Command<DbContext>.Macro()
    .Add(insertOrderCommand)
    .Add(updateInventoryCommand)
    .Add(chargePaymentCommand)
    .Build();

try
{
    await transaction.Execute(dbContext);
    await dbContext.CommitAsync();
}
catch (Exception ex)
{
    // Rollback application state
    if (transaction.TryUndo(dbContext, out var undo))
        await undo;

    await dbContext.RollbackAsync();
    throw;
}
```

### Command Queue

Queue and process commands:

```csharp
public class CommandQueue<TCtx>
{
    private readonly Queue<Command<TCtx>> _pending = new();
    private readonly Stack<Command<TCtx>> _executed = new();

    public void Enqueue(Command<TCtx> cmd) => _pending.Enqueue(cmd);

    public async Task ProcessAllAsync(TCtx ctx, CancellationToken ct)
    {
        while (_pending.TryDequeue(out var cmd))
        {
            await cmd.Execute(ctx, ct);
            if (cmd.HasUndo)
                _executed.Push(cmd);
        }
    }

    public async Task UndoAllAsync(TCtx ctx, CancellationToken ct)
    {
        while (_executed.TryPop(out var cmd))
        {
            if (cmd.TryUndo(ctx, ct, out var undo))
                await undo;
        }
    }
}
```

## Error Handling

### Exception Propagation

Exceptions from Do or Undo propagate to the caller:

```csharp
var riskyCommand = Command<Context>.Create()
    .Do(ctx =>
    {
        if (ctx.IsInvalid)
            throw new InvalidOperationException("Invalid context");
        ctx.Process();
    })
    .Build();

try
{
    await riskyCommand.Execute(context);
}
catch (InvalidOperationException ex)
{
    // Handle the error
}
```

### Macro Failure Behavior

Macros stop on first failure. Previously executed commands are NOT automatically undone:

```csharp
var macro = Command<Ctx>.Macro()
    .Add(step1)  // Executes
    .Add(step2)  // Fails!
    .Add(step3)  // Never runs
    .Build();

try
{
    await macro.Execute(context);
}
catch (Exception)
{
    // step1 was executed but not undone automatically
    // You must explicitly undo if needed:
    if (macro.TryUndo(context, out var undo))
        await undo; // Undoes step1
}
```

## Performance Tips

1. **Use ValueTask**: Commands return ValueTask for allocation-free sync completion
2. **Avoid closures**: Use static lambdas when possible
3. **Reuse commands**: Build once, execute many times
4. **Struct contexts**: Use `in` parameter to avoid copies

```csharp
// Good: static lambda, no allocations
.Do(static (in ctx, _) => { ctx.Action(); return default; })

// Avoid: capturing external state
var threshold = 10;
.Do((in ctx, _) => { if (ctx.Value > threshold) ... }) // Closure!
```

## Best Practices

1. **Make undo idempotent**: Safe to call multiple times
2. **Keep commands focused**: One logical operation per command
3. **Test both paths**: Verify Do and Undo independently
4. **Document side effects**: Make clear what each command modifies
5. **Consider atomicity**: Macro undo is not transactional

## FAQ

**Q: Can I reuse a command instance?**
A: Yes! Commands are immutable after Build() and can be executed many times.

**Q: What if my undo can fail?**
A: Exceptions propagate from Undo. Handle them at the call site.

**Q: Can I create commands dynamically?**
A: Yes. Build commands at runtime based on configuration or state.

**Q: How does this differ from event sourcing?**
A: Commands are imperative (do something). Events are records of what happened. They often work together.
