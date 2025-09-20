# Command — Command<TCtx>

A minimal, allocation-light Command pattern:

- Encapsulates an action (Do) and optional Undo over a context TCtx
- Executes synchronously or asynchronously via ValueTask
- Composes multiple commands into a macro that executes in order and undoes in reverse
- Thread-safe after Build(); uses in parameters for structs

---

## Quick start

```csharp
using PatternKit.Behavioral.Command;

// Single command
var cmd = Command<MyCtx>.Create()
    .Do(static (in MyCtx c, CancellationToken _) => { Console.WriteLine($"hello {c.Name}"); return default; })
    .Undo(static (in MyCtx c, CancellationToken _) => { Console.WriteLine($"undo {c.Name}"); return default; })
    .Build();

var ctx = new MyCtx("Ada");
await cmd.Execute(ctx);             // hello Ada
await cmd.TryUndo(ctx, out var t);  // t completes; prints "undo Ada"

// Macro
var a = Command<MyCtx>.Create().Do(static (in MyCtx c, _) => { Console.Write("A"); return default; }).Build();
var b = Command<MyCtx>.Create().Do(static (in MyCtx c, _) => { Console.Write("B"); return default; }).Build();

var macro = Command<MyCtx>.Macro().Add(a).Add(b).Build();
await macro.Execute(ctx); // prints AB
```

```csharp
public readonly record struct MyCtx(string Name);
```

---

## API (at a glance)

```csharp
public sealed class Command<TCtx>
{
    public delegate ValueTask Exec(in TCtx ctx, CancellationToken ct);

    public static Builder Create();
    public ValueTask Execute(in TCtx ctx, CancellationToken ct);
    public ValueTask Execute(in TCtx ctx); // ct = default

    public bool TryUndo(in TCtx ctx, CancellationToken ct, out ValueTask undoTask);
    public bool TryUndo(in TCtx ctx, out ValueTask undoTask); // ct = default
    public bool HasUndo { get; }

    public sealed class Builder
    {
        public Builder Do(Exec handler);            // required
        public Builder Do(Action<TCtx> handler);    // sync adapter
        public Builder Undo(Exec handler);          // optional
        public Builder Undo(Action<TCtx> handler);  // sync adapter
        public Command<TCtx> Build();
    }

    public static MacroBuilder Macro();

    public sealed class MacroBuilder
    {
        public MacroBuilder Add(Command<TCtx> cmd);
        public MacroBuilder AddIf(bool condition, Command<TCtx> cmd);
        public Command<TCtx> Build(); // executes in order; Undo runs in reverse
    }
}
```

### Design notes

- Uses in parameters to avoid copies of struct contexts.
- ValueTask everywhere keeps the sync-fast path allocation-free.
- Macro execution is optimized for the fast path: it returns immediately if all steps complete synchronously; otherwise it awaits only when needed.

### Error behavior

- Execute/Undo propagate exceptions from user handlers.
- TryUndo returns false if no Undo was configured.

---

## Testing

See PatternKit.Tests/Behavioral/Command/CommandTests.cs for TinyBDD scenarios covering Do/Undo and macro ordering.

