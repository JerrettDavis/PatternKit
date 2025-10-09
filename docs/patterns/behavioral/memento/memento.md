# Memento<TState>

A **generic snapshot + restore history engine**: capture immutable versions of a mutable state value and move backward (Undo), forward (Redo), or jump to an arbitrary saved version. Optimized for **fluent configuration, low allocations, and thread‑safety**.

> Classic GoF Memento splits *Originator* (creates snapshots) and *Caretaker* (stores snapshots). Here, `Memento<TState>` is the caretaker + history core you compose around your own originator (the mutable state you pass in / apply to).

---
## What it gives you

* **Time travel**: `Save` → mutate → `Undo` / `Redo`.
* **Version IDs**: monotonically increasing `int` for each retained snapshot.
* **Tagged checkpoints**: optional human labels (`tag`) for milestone navigation.
* **Capacity bound (optional)**: FIFO eviction of oldest snapshots when a limit is set.
* **Duplicate suppression**: opt-in equality comparer to skip logically identical successive states.
* **Thread‑safe**: internal monitor around mutating operations; read APIs copy out immutable snapshot structs.
* **Custom cloning & applying**: deep clone reference graphs or partially apply snapshots to live state.

---
## TL;DR example
```csharp
using PatternKit.Behavioral.Memento;

// Mutable originator state
public sealed class Document { public string Text = string.Empty; public int Caret; }

var history = Memento<Document>.Create()
    .CloneWith(static (in Document d) => new Document { Text = d.Text, Caret = d.Caret })
    .Equality(new RefDocValueComparer()) // skip if Text + Caret unchanged
    .Capacity(100)                       // keep last 100 edits
    .Build();

var doc = new Document();

history.Save(in doc, tag: "init"); // version 1

doc.Text = "Hello"; doc.Caret = 5; history.Save(in doc, tag: "greeting"); // version 2

doc.Text = "Hello, world"; doc.Caret = 12; history.Save(in doc);            // version 3

doc.Text = "Hello, brave new world"; doc.Caret = 23; history.Save(in doc);  // version 4

history.Undo(ref doc); // back to version 3
history.Redo(ref doc); // forward to version 4
// Jump directly (if retained):
var ok = history.Restore(2, ref doc); // doc now "Hello", caret 5
```

---
## API surface
```csharp
var m = Memento<TState>.Create()
    .CloneWith(static (in TState s) => /* deep or value copy */)
    .ApplyWith(static (ref TState live, TState snap) => /* selective apply */)
    .Equality(/* IEqualityComparer<TState> */)   // optional
    .Capacity(64)                                // optional, 0 = unbounded
    .Build();

int v1 = m.Save(in currentState, tag: "initial");
bool undo = m.Undo(ref currentState);
bool redo = m.Redo(ref currentState);
bool restored = m.Restore(v1, ref currentState);
int currentVersion = m.CurrentVersion; // 0 if empty
IReadOnlyList<Memento<TState>.Snapshot> all = m.History; // copy (safe to enumerate)
```

### Snapshot struct
`struct Snapshot { int Version; TState State; DateTime TimestampUtc; string? Tag; }`

* `Version`: monotonically increasing (never reused, even after eviction).
* `State`: the cloned snapshot payload you supplied via `CloneWith`.
* `TimestampUtc`: capture time (UTC).
* `Tag`: optional label; `HasTag` convenience property.

---
## Configuration knobs

| Method | Purpose | Default |
| ------ | ------- | ------- |
| `CloneWith(Cloner)` | Provide deep clone or copy logic. | Value copy (`s => s`) |
| `ApplyWith(Applier)` | Custom restore (partial merge / diff). | Assignment (`target = snap`) |
| `Equality(IEqualityComparer<T>)` | Skip snapshot if logically equal to previous. | None (always save) |
| `Capacity(int)` | Retain at most N latest snapshots (FIFO eviction). | 0 (unbounded) |

> Supply a *deep* clone for mutable reference graphs (lists, trees) or further mutations will retroactively affect saved snapshots.

---
## Typical usage patterns

### 1. Text editor buffer
```csharp
public sealed class Buffer { public string Text = string.Empty; public int Caret; }
var history = Memento<Buffer>.Create()
    .CloneWith(static (in Buffer b) => new Buffer { Text = b.Text, Caret = b.Caret })
    .Equality(new BufferComparer())
    .Capacity(200)
    .Build();

var buf = new Buffer();
void Commit(string tag = null) => history.Save(in buf, tag: tag);

Commit("start");
buf.Text = "Hello"; buf.Caret = 5; Commit();
buf.Text = "Hello!"; buf.Caret = 6; Commit();

history.Undo(ref buf); // back to "Hello"
```

### 2. View model state with partial apply
```csharp
// Only restore layout; preserve ephemeral runtime metrics.
public sealed class DashboardState { public string LayoutJson = "{}"; public int ActiveUsers; }
var m = Memento<DashboardState>.Create()
    .CloneWith(static (in DashboardState s) => new DashboardState { LayoutJson = s.LayoutJson, ActiveUsers = 0 })
    .ApplyWith(static (ref DashboardState live, DashboardState snap) => live.LayoutJson = snap.LayoutJson)
    .Build();
```

### 3. Capacity eviction semantics
```csharp
var m = Memento<int>.Create().Capacity(3).Build();
for (int i=0;i<5;i++) m.Save(i); // versions 1..5, but only last 3 retained
m.Restore(1, ref Unsafe.NullRef<int>()); // false (evicted)
```

### 4. Duplicate suppression
```csharp
var m = Memento<string>.Create()
    .Equality(StringComparer.Ordinal)
    .Build();

m.Save("A"); // v1
m.Save("A"); // skipped, still v1
m.Save("B"); // v2
```

---
## Undo / Redo rules

1. `Undo` moves the cursor backward if possible and applies the prior snapshot.
2. `Redo` moves it forward if not at the end.
3. Calling `Save` while not at the end **truncates** forward history (like editors after a divergent edit).
4. Capacity eviction only removes the oldest snapshot; current cursor adjusts accordingly.

---
## Thread-safety

All mutating operations (`Save`, `Undo`, `Redo`, `Restore`) lock a private object. Reads that enumerate `History` copy out an array so the caller can iterate without locks. For extremely high-frequency histories you can layer a ring buffer later—this design keeps things simple and predictable first.

---
## When not to use

* You only need a single rollback → a simple `clone` variable is cheaper.
* State is massive and snapshots dwarf business logic → consider diffs / command replay.
* You need transactional grouping across disparate aggregates → look at Command + explicit undo or event sourcing.

---
## Testing (TinyBDD style)
```csharp
[Scenario("Undo / Redo basic traversal")]
[Fact]
public async Task UndoRedo()
{
    var m = Memento<int>.Create().Build();
    var s = 0;
    m.Save(in s); s = 1; m.Save(in s); s = 2; m.Save(in s); // versions 1..3

    await Given("history with 3 versions", () => m)
        .When("undo", _ => { m.Undo(ref s); return s; })
        .Then("state is 1", v => v == 1)
        .When("redo", _ => { m.Redo(ref s); return s; })
        .Then("state is 2", v => v == 2)
        .AssertPassed();
}
```

---
## Design notes

* **Struct snapshots**: Lightweight wrapper holds version metadata + cloned state.
* **Monotonic versions**: Never reused (even after eviction) so you can log them and correlate externally.
* **Linear restore lookup**: History lists are typically small (tens/hundreds). If you need thousands + frequent random restores, add an index map externally.
* **Extensibility**: Wrap `Memento<T>` inside higher-level undo stacks (multi-document, workspaces) or combine with `Command<TCtx>` for rich reversible pipelines.

---
## See also

* [Command](../command/command.md) – encapsulate operations + undo logic directly.
* Strategy / Chain patterns – pair with mementos for conditional editing pipelines.
* Prototype – for creating initial deep copies used by `CloneWith`.
