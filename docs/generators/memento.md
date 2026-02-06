# Memento Generator

## Overview

The **Memento Generator** creates immutable snapshot structs and optional caretaker (history) classes for implementing undo/redo functionality. It eliminates boilerplate by generating `Capture` and `Restore` methods, version tracking, and history management.

## When to Use

Use the Memento generator when you need to:

- **Implement undo/redo**: Track state changes and restore previous states
- **Create snapshots**: Capture object state at specific points in time
- **Support time travel debugging**: Navigate through state history
- **Persist state safely**: Immutable mementos are safe to store and share

## Installation

The generator is included in the `PatternKit.Generators` package:

```bash
dotnet add package PatternKit.Generators
```

## Quick Start

```csharp
using PatternKit.Generators;

[Memento(GenerateCaretaker = true)]
public partial class Document
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public int CursorPosition { get; set; }
}
```

Generated:
```csharp
// Immutable snapshot struct
public readonly partial struct DocumentMemento
{
    public int MementoVersion => /* hash */;
    public string Title { get; }
    public string Content { get; }
    public int CursorPosition { get; }

    public static DocumentMemento Capture(in Document originator);
    public Document RestoreNew();
    public void Restore(Document originator);
}

// History manager (when GenerateCaretaker = true)
public sealed partial class DocumentHistory
{
    public int Count { get; }
    public bool CanUndo { get; }
    public bool CanRedo { get; }
    public Document Current { get; }

    public void Capture(Document state);
    public bool Undo();
    public bool Redo();
    public void Clear(Document initial);
}
```

Usage:
```csharp
// Manual snapshot
var doc = new Document { Title = "Hello", Content = "World", CursorPosition = 5 };
var memento = DocumentMemento.Capture(in doc);

doc.Title = "Changed";
var restored = memento.RestoreNew(); // Back to "Hello"

// With history (undo/redo)
var history = new DocumentHistory(new Document());
history.Capture(new Document { Title = "v1" });
history.Capture(new Document { Title = "v2" });
history.Capture(new Document { Title = "v3" });

history.Undo(); // Current.Title == "v2"
history.Undo(); // Current.Title == "v1"
history.Redo(); // Current.Title == "v2"
```

## Records Support

The generator works seamlessly with records, using positional constructors when available:

```csharp
[Memento(GenerateCaretaker = true, Capacity = 100, SkipDuplicates = true)]
public partial record class EditorState(string Text, int Cursor, int SelectionLength)
{
    public bool HasSelection => SelectionLength > 0;

    public EditorState Insert(string text)
    {
        // Immutable update
        return this with { Text = Text.Insert(Cursor, text), Cursor = Cursor + text.Length };
    }
}
```

The memento `RestoreNew()` method will use the record's primary constructor.

## Attributes

### `[Memento]`

Main attribute for marking types to generate memento support.

| Property | Type | Default | Description |
|---|---|---|---|
| `GenerateCaretaker` | `bool` | `false` | Generate history class for undo/redo |
| `Capacity` | `int` | `0` | Max history entries (0 = unlimited) |
| `InclusionMode` | `MementoInclusionMode` | `IncludeAll` | How to select members |
| `SkipDuplicates` | `bool` | `true` | Skip capture if state equals current |

### `[MementoIgnore]`

Excludes a member from memento capture (when using `IncludeAll` mode):

```csharp
[Memento]
public partial class EditorState
{
    public string Content { get; set; } = "";

    [MementoIgnore]
    public bool IsDirty { get; set; } // Not captured
}
```

### `[MementoInclude]`

Explicitly includes a member (when using `OptIn` mode):

```csharp
[Memento(InclusionMode = MementoInclusionMode.OptIn)]
public partial class EditorState
{
    [MementoInclude]
    public string Content { get; set; } = ""; // Captured

    public DateTime LastModified { get; set; } // Not captured
}
```

### `[MementoStrategy]`

Controls how reference types are captured:

```csharp
[Memento]
public partial class GameState
{
    public int Score { get; set; }

    [MementoStrategy(CaptureStrategy.Clone)]
    public List<string> Items { get; set; } = new(); // Cloned on capture
}
```

| Strategy | Description |
|---|---|
| `ByReference` | Store reference directly (default for value types, strings) |
| `Clone` | Call `ICloneable.Clone()` or use custom cloner |
| `DeepCopy` | Serialize/deserialize for deep copy |

## Caretaker (History) Features

When `GenerateCaretaker = true`, a history class is generated:

### Properties

| Property | Type | Description |
|---|---|---|
| `Count` | `int` | Total states in history |
| `CanUndo` | `bool` | True if undo is possible |
| `CanRedo` | `bool` | True if redo is possible |
| `Current` | `T` | Current state (or default if empty) |

### Methods

| Method | Description |
|---|---|
| `Capture(T state)` | Adds state to history, truncating forward history |
| `Undo()` | Moves to previous state; returns false if at start |
| `Redo()` | Moves to next state; returns false if at end |
| `Clear(T initial)` | Clears history and sets initial state |

### History Behavior

```
[A] → [B] → [C] → [D]    // Capture A, B, C, D
                   ↑
              Current (D)

Undo():
[A] → [B] → [C] → [D]
             ↑
        Current (C)

Undo():
[A] → [B] → [C] → [D]
       ↑
  Current (B)

Capture(E):   // Truncates forward history!
[A] → [B] → [E]
             ↑
        Current (E)
```

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| **PKMEM001** | Error | Type must be declared as `partial` |
| **PKMEM002** | Warning | Member is inaccessible for capture/restore |
| **PKMEM003** | Warning | Mutable reference type captured by reference (mutations affect all snapshots) |
| **PKMEM004** | Error | Clone strategy requested but no clone mechanism available |
| **PKMEM005** | Error | Cannot generate RestoreNew for record (no accessible constructor) |
| **PKMEM006** | Info | Init-only/readonly members prevent in-place restore |

## Best Practices

### 1. Use Immutable Types When Possible
Records with `with` expressions make state management cleaner:

```csharp
[Memento(GenerateCaretaker = true)]
public partial record class AppState(int Counter, string Status)
{
    public AppState Increment() => this with { Counter = Counter + 1 };
}
```

### 2. Set Capacity for Long-Running Applications
Prevent memory issues by limiting history size:

```csharp
[Memento(GenerateCaretaker = true, Capacity = 1000)]
public partial class DocumentState { }
```

### 3. Skip Duplicates for Frequent Updates
Avoid cluttering history with identical states:

```csharp
[Memento(GenerateCaretaker = true, SkipDuplicates = true)]
public partial record struct Position(int X, int Y);
```

### 4. Handle Reference Types Carefully
Mutable reference types can cause issues if modified after capture:

```csharp
[Memento]
public partial class GameState
{
    // ⚠️ Warning: List mutations affect all mementos
    public List<Item> Inventory { get; set; } = new();

    // ✅ Better: Use immutable collection or Clone strategy
    [MementoStrategy(CaptureStrategy.Clone)]
    public List<Item> Inventory { get; set; } = new();
}
```

## Examples

### Text Editor with Undo

```csharp
[Memento(GenerateCaretaker = true, Capacity = 100, SkipDuplicates = true)]
public partial record class EditorState(string Text, int Cursor, int SelectionLength)
{
    public static EditorState Empty() => new("", 0, 0);

    public EditorState Insert(string text) =>
        this with { Text = Text.Insert(Cursor, text), Cursor = Cursor + text.Length };

    public EditorState Backspace() =>
        Cursor == 0 ? this : this with { Text = Text.Remove(Cursor - 1, 1), Cursor = Cursor - 1 };
}

public sealed class TextEditor
{
    private readonly EditorStateHistory _history;

    public TextEditor() => _history = new(EditorState.Empty());

    public EditorState Current => _history.Current;
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    public void Apply(Func<EditorState, EditorState> operation)
    {
        _history.Capture(operation(Current));
    }

    public bool Undo() => _history.Undo();
    public bool Redo() => _history.Redo();
}

// Usage
var editor = new TextEditor();
editor.Apply(s => s.Insert("Hello"));
editor.Apply(s => s.Insert(" World"));
Console.WriteLine(editor.Current.Text); // "Hello World"

editor.Undo();
Console.WriteLine(editor.Current.Text); // "Hello"
```

### Game State Snapshots

```csharp
[Memento]
public partial class GameState
{
    public int Score { get; set; }
    public int Level { get; set; }
    public int Lives { get; set; } = 3;
    public string PlayerName { get; set; } = "";
}

// Quick save/load
var game = new GameState { Score = 1000, Level = 5, PlayerName = "Player1" };
var quickSave = GameStateMemento.Capture(in game);

// Player dies...
game.Lives--;
game.Score -= 100;

// Quick load
var restored = quickSave.RestoreNew();
Console.WriteLine($"Restored: Score={restored.Score}, Lives={restored.Lives}");
```

### Application State with Limited History

```csharp
[Memento(GenerateCaretaker = true, Capacity = 50)]
public partial record class AppSettings(
    string Theme,
    int FontSize,
    bool DarkMode)
{
    public static AppSettings Default => new("Default", 14, false);
}

public class SettingsManager
{
    private readonly AppSettingsHistory _history;

    public SettingsManager() => _history = new(AppSettings.Default);

    public AppSettings Current => _history.Current;

    public void UpdateTheme(string theme)
        => _history.Capture(Current with { Theme = theme });

    public void UpdateFontSize(int size)
        => _history.Capture(Current with { FontSize = size });

    public void Reset()
        => _history.Clear(AppSettings.Default);
}
```

## Troubleshooting

### PKMEM001: Type must be partial

**Cause:** Target type is not marked `partial`.

**Fix:**
```csharp
// ❌ Wrong
[Memento]
public class State { }

// ✅ Correct
[Memento]
public partial class State { }
```

### PKMEM003: Mutable reference capture warning

**Cause:** A mutable reference type (List, Dictionary, etc.) is captured by reference.

**Fix:** Use `[MementoStrategy(Clone)]` or immutable types:
```csharp
// Option 1: Clone strategy
[MementoStrategy(CaptureStrategy.Clone)]
public List<Item> Items { get; set; }

// Option 2: Use immutable collection
public ImmutableList<Item> Items { get; set; } = ImmutableList<Item>.Empty;
```

### PKMEM006: Init-only restriction

**Cause:** Init-only properties prevent in-place restore.

**Note:** This is informational. Use `RestoreNew()` instead of `Restore()`:
```csharp
var memento = StateMemento.Capture(in state);
var restored = memento.RestoreNew(); // Creates new instance
// memento.Restore(state); // Not available for init-only types
```

## See Also

- [Patterns: Memento](../patterns/behavioral/memento/index.md)
- [Command Pattern](../patterns/behavioral/command/index.md) — Often used with Memento for undo
