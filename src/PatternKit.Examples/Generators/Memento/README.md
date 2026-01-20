# Memento Pattern Source Generator

The Memento pattern source generator provides a powerful, compile-time solution for capturing and restoring object state with full undo/redo support. It works seamlessly with **classes, structs, record classes, and record structs**.

## Features

- ✨ **Zero-boilerplate** memento generation
- 🔄 **Undo/redo** support via optional caretaker
- 📸 **Immutable snapshots** with deterministic versioning
- 🎯 **Type-safe** restore operations
- ⚡ **Compile-time** code generation (no reflection)
- 🛡️ **Safe by default** with warnings for mutable reference captures
- 🎨 **Flexible** member selection (include-all or explicit)

## Quick Start

### Basic Memento (Snapshot Only)

```csharp
using PatternKit.Generators;

[Memento]
public partial record class EditorState(string Text, int Cursor);
```

**Generated Code:**
```csharp
public readonly partial struct EditorStateMemento
{
    public int MementoVersion => 12345678;
    public string Text { get; }
    public int Cursor { get; }

    public static EditorStateMemento Capture(in EditorState originator);
    public EditorState RestoreNew();
}
```

**Usage:**
```csharp
var state = new EditorState("Hello", 5);
var memento = EditorStateMemento.Capture(in state);

// Later...
var restored = memento.RestoreNew();
```

### With Undo/Redo Caretaker

```csharp
[Memento(GenerateCaretaker = true, Capacity = 100)]
public partial record class EditorState(string Text, int Cursor);
```

**Generated Caretaker:**
```csharp
public sealed partial class EditorStateHistory
{
    public int Count { get; }
    public bool CanUndo { get; }
    public bool CanRedo { get; }
    public EditorState Current { get; }

    public EditorStateHistory(EditorState initial);
    public void Capture(EditorState state);
    public bool Undo();
    public bool Redo();
    public void Clear(EditorState initial);
}
```

**Usage:**
```csharp
var history = new EditorStateHistory(new EditorState("", 0));

history.Capture(new EditorState("Hello", 5));
history.Capture(new EditorState("Hello World", 11));

if (history.CanUndo)
{
    history.Undo(); // Back to "Hello"
    var current = history.Current;
}

if (history.CanRedo)
{
    history.Redo(); // Forward to "Hello World"
}
```

## Supported Types

### Record Class (Immutable-Friendly)

```csharp
[Memento(GenerateCaretaker = true)]
public partial record class EditorState(string Text, int Cursor);
```

- Primary restore method: `RestoreNew()` (returns new instance)
- Caretaker stores state instances
- Perfect for immutable design

### Record Struct

```csharp
[Memento]
public partial record struct Point(int X, int Y);
```

- Value semantics with snapshot support
- Efficient for small state objects

### Class (Mutable)

```csharp
[Memento(GenerateCaretaker = true)]
public partial class GameState
{
    public int Health { get; set; }
    public int Score { get; set; }
}
```

- Supports both `Restore(originator)` (in-place) and `RestoreNew()`
- Useful for large, complex state objects

### Struct (Mutable)

```csharp
[Memento]
public partial struct Counter
{
    public int Value { get; set; }
}
```

- Efficient value-type snapshots

## Configuration Options

### Attribute Parameters

```csharp
[Memento(
    GenerateCaretaker = true,    // Generate undo/redo caretaker
    Capacity = 100,               // Max history entries (0 = unlimited)
    InclusionMode = MementoInclusionMode.IncludeAll,  // or ExplicitOnly
    SkipDuplicates = true         // Skip consecutive equal states
)]
public partial record class MyState(...);
```

### Member Selection

#### Include All (Default)

```csharp
[Memento]
public partial class Document
{
    public string Text { get; set; }  // ✓ Included
    
    [MementoIgnore]
    public string TempData { get; set; }  // ✗ Excluded
}
```

#### Explicit Only

```csharp
[Memento(InclusionMode = MementoInclusionMode.ExplicitOnly)]
public partial class Document
{
    [MementoInclude]
    public string Text { get; set; }  // ✓ Included
    
    public string InternalId { get; set; }  // ✗ Excluded
}
```

### Capture Strategies

```csharp
[Memento]
public partial class Document
{
    public string Text { get; set; }  // Safe (immutable)
    
    [MementoStrategy(MementoCaptureStrategy.ByReference)]
    public List<string> Tags { get; set; }  // ⚠️ Warning: mutable reference
}
```

**Available Strategies:**
- `ByReference` - Shallow copy (safe for value types and strings)
- `Clone` - Deep clone via ICloneable or with-expression
- `DeepCopy` - Generator-emitted deep copy
- `Custom` - User-provided custom capture logic

## Caretaker Behavior

### Undo/Redo Semantics

```csharp
var history = new EditorStateHistory(initial);

history.Capture(state1);  // [initial, state1] cursor=1
history.Capture(state2);  // [initial, state1, state2] cursor=2

history.Undo();           // [initial, state1, state2] cursor=1
history.Undo();           // [initial, state1, state2] cursor=0

history.Redo();           // [initial, state1, state2] cursor=1

history.Capture(state3);  // [initial, state1, state3] cursor=2 (state2 removed)
```

### Capacity Management

```csharp
[Memento(GenerateCaretaker = true, Capacity = 3)]
public partial record class State(int Value);
```

When capacity is exceeded, the **oldest** state is evicted (FIFO):

```csharp
var history = new StateHistory(s0);

history.Capture(s1);  // [s0, s1]
history.Capture(s2);  // [s0, s1, s2]
history.Capture(s3);  // [s0, s1, s2, s3] - over capacity!
                      // [s1, s2, s3] - s0 evicted
```

### Duplicate Suppression

```csharp
[Memento(GenerateCaretaker = true, SkipDuplicates = true)]
public partial record class State(int Value);
```

Consecutive equal states (by value equality) are automatically skipped:

```csharp
var history = new StateHistory(new State(0));

history.Capture(new State(0));  // Skipped (duplicate)
history.Capture(new State(1));  // Added
history.Capture(new State(1));  // Skipped (duplicate)
// History: [State(0), State(1)]
```

## Diagnostics

The generator provides comprehensive diagnostics:

| ID | Severity | Description |
|----|----------|-------------|
| **PKMEM001** | Error | Type must be `partial` |
| **PKMEM002** | Warning | Member inaccessible for capture/restore |
| **PKMEM003** | Warning | Unsafe reference capture (mutable reference) |
| **PKMEM004** | Error | Clone strategy missing mechanism |
| **PKMEM005** | Error | Record restore generation failed |
| **PKMEM006** | Info | Init-only restrictions prevent in-place restore |

## Real-World Examples

### Text Editor with Undo/Redo

```csharp
[Memento(GenerateCaretaker = true, Capacity = 100, SkipDuplicates = true)]
public partial record class EditorState(string Text, int Cursor, int SelectionLength)
{
    public EditorState Insert(string text) { /* ... */ }
    public EditorState Backspace() { /* ... */ }
}

var editor = new EditorStateHistory(EditorState.Empty);

// Edit operations
editor.Capture(state.Insert("Hello"));
editor.Capture(state.Insert(" World"));

// Undo/Redo
editor.Undo();  // Back to "Hello"
editor.Redo();  // Forward to "Hello World"
```

### Game Save/Load System

```csharp
[Memento]
public partial class GameState
{
    public int PlayerX { get; set; }
    public int PlayerY { get; set; }
    public int Health { get; set; }
    public int Score { get; set; }
}

// Save game
var saveFile = GameStateMemento.Capture(in gameState);
File.WriteAllBytes("save.dat", Serialize(saveFile));

// Load game
var saveFile = Deserialize<GameStateMemento>(File.ReadAllBytes("save.dat"));
saveFile.Restore(gameState);  // In-place restore for mutable class
```

### Configuration Snapshots

```csharp
[Memento(InclusionMode = MementoInclusionMode.ExplicitOnly)]
public partial class AppConfig
{
    [MementoInclude]
    public string ApiEndpoint { get; set; }
    
    [MementoInclude]
    public int Timeout { get; set; }
    
    // Not included in snapshots
    public string RuntimeToken { get; set; }
}

// Capture configuration
var backup = AppConfigMemento.Capture(in config);

// Restore if validation fails
if (!ValidateConfig(config))
{
    config = backup.RestoreNew();
}
```

## Best Practices

### 1. Use Records for Immutable State

```csharp
// ✓ Good: Immutable record
[Memento(GenerateCaretaker = true)]
public partial record class State(string Value);

// ✗ Avoid: Mutable class when records would work
[Memento(GenerateCaretaker = true)]
public partial class State
{
    public string Value { get; set; }
}
```

### 2. Be Explicit About Mutable References

```csharp
[Memento]
public partial class Document
{
    // ✓ Good: Explicitly acknowledge the strategy
    [MementoStrategy(MementoCaptureStrategy.ByReference)]
    public List<string> Tags { get; set; }
}
```

### 3. Exclude Transient State

```csharp
[Memento]
public partial class Editor
{
    public string Text { get; set; }
    
    // ✓ Good: Exclude runtime-only state
    [MementoIgnore]
    public bool IsDirty { get; set; }
}
```

### 4. Set Appropriate Capacity

```csharp
// ✓ Good: Reasonable capacity for undo/redo
[Memento(GenerateCaretaker = true, Capacity = 100)]

// ✗ Avoid: Unlimited capacity for large states
[Memento(GenerateCaretaker = true, Capacity = 0)]  // Can cause memory issues
```

## Performance Considerations

- **Memento capture**: O(n) where n = number of members
- **Caretaker undo/redo**: O(1)
- **Capacity eviction**: O(1) (removes oldest)
- **Memory**: Each snapshot stores a complete copy of included members

For large objects with frequent snapshots, consider:
- Using `[MementoIgnore]` to exclude large, reconstructible data
- Setting a reasonable `Capacity`
- Using value types (structs/record structs) when appropriate

## See Also

- [EditorStateDemo.cs](./EditorStateDemo.cs) - Full text editor example
- [GameStateDemo.cs](./GameStateDemo.cs) - Game state with save/load
- [PatternKit.Behavioral.Memento](../../../Core/Behavioral/Memento/) - Runtime memento implementation

## License

MIT License - see [LICENSE](../../../../../../LICENSE) for details.
