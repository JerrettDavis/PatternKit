# Text Editor History (Memento Pattern)

A minimal in-memory text editor built on `Memento<TState>` demonstrating:

* Incremental snapshots (`Save` after each edit) with tags
* Undo / Redo navigation
* Branching edits (truncate forward history after divergent change)
* Duplicate suppression (skips snapshots when state unchanged)
* Capacity limiting (optional)
* Batched edits (group multiple operations into one snapshot)

---
## Why this demo

Typical editors need rapid, memory-conscious snapshot history with predictable semantics:

* Undo should step back to prior *logical* state
* Redo should vanish if the user edits after undoing (branching)
* Repeated caret moves / no-op edits shouldn’t bloat history
* Operations should be easy to test deterministically

`Memento<TState>` gives you a small, thread-safe engine to do exactly that.

---
## State model

The editor keeps an immutable `DocumentState` in each snapshot:

```csharp
public readonly struct DocumentState
{
    public string Text { get; }
    public int Caret { get; }
    public int SelectionLength { get; }
    public bool HasSelection => SelectionLength > 0;
}
```

Snapshots are created with a deep-ish clone (new struct copy + string reference). If you had mutable reference graphs, you’d supply a deep cloning function.

---
## Building the history engine

```csharp
var history = Memento<DocumentState>.Create()
    .CloneWith(static (in DocumentState s) => new DocumentState(s.Text, s.Caret, s.SelectionLength))
    .Equality(new StateEquality())   // skip consecutive duplicates
    .Capacity(500)                   // keep last 500 edits
    .Build();
```

Each editing operation mutates the live `_state` then calls `history.Save(in _state, tag)`.

---
## Editing operations covered

| Operation | Description | Snapshot Tag Example |
|----------|-------------|----------------------|
| `Insert(text)` | Inserts or replaces selection with `text`. | `insert:Hello` |
| `ReplaceSelection(text)` | Replaces current selection; falls back to `Insert` if no selection. | `replace:Hi` |
| `MoveCaret(pos)` | Moves caret (clears selection). | `caret:12` |
| `Select(start,len)` | Selects a range; zero length → empty selection. | `select:0-5` |
| `Backspace(count)` | Deletes selection or characters before caret. | `backspace:3` / `delete:sel` |
| `DeleteForward(count)` | Deletes selection or characters after caret. | `del:2` |
| `Batch(tag, func)` | Runs a lambda; if it returns true and state changed → single snapshot. | `batch:indent` |

---
## Branching example

1. Type several words.
2. Undo twice.
3. Type new characters.

The redo stack is truncated automatically (standard editor semantics): you cannot redo into the alternate future.

---
## Running the demo

```csharp
var log = MementoDemo.Run();
foreach (var line in log)
    Console.WriteLine(line);
```

Sample (abridged):
```
v2:insert Hello -> 'Hello' (caret 5)
v3:insert , world -> 'Hello, world' (caret 12)
...
v12:branch insert !!! -> 'Hi brave new, world!!!' (caret 25)
FINAL:'Hi brave new, world!!!' version=12 history=12
```

(Version numbers include the initial `init` snapshot.)

---
## Undo / Redo guarantees

* `Undo()` returns false at earliest retained snapshot.
* `Redo()` returns false at latest snapshot.
* Any editing method after an `Undo` truncates forward snapshots.
* Capacity eviction removes only *oldest* snapshots; version numbers remain monotonic (never reused).

---
## Batch editing

```csharp
editor.Batch("indent-line", ed => {
    var s = ed.State;
    if (!s.Text.Contains('\n')) return false; // no multi-line indent
    var parts = s.Text.Split('\n');
    for (int i = 0; i < parts.Length; i++) parts[i] = "    " + parts[i];
    var joined = string.Join('\n', parts);
    // Replace entire text efficiently
    ed.Select(0, s.Text.Length);
    ed.ReplaceSelection(joined);
    return true; // commit single snapshot
});
```

If `action` returns false or produces no net state change (duplicate), no snapshot is added.

---
## Testing highlights

The accompanying tests (see `MementoDemoTests`) validate:

* Deterministic final text of the canned demo run.
* Branching semantics (redo cleared after divergent edit).
* Capacity trimming still leaves monotonic versions.
* Batch operation produces a single snapshot.

---
## When to go further

For very large documents or extremely high-frequency edits consider:

* **Diff-based snapshots** (store reversed operations instead of full text).
* **Ring-buffer storage** (lock-free, fixed memory usage).
* **Compression** of large text blocks (e.g., per 10th snapshot) if memory pressure is high.

PatternKit’s `Memento<TState>` keeps the core generic and simple so you can layer these later.

---
## See also

* [Memento Pattern Core](../patterns/behavioral/memento/memento.md)
* [Command](../patterns/behavioral/command/command.md) for reversible operations without full state copies
* Strategy / Chain for conditional, rule-based transformations prior to snapshot

