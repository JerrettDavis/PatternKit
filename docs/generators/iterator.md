# Iterator Generator

The Iterator generator emits allocation-light `TryMoveNext` and a struct enumerator wrapper for a partial iterator type.

## Usage

```csharp
using PatternKit.Generators.Iterator;

[Iterator]
public partial struct Counter
{
    private int _current;

    [IteratorStep]
    private bool Step(out int item)
    {
        item = ++_current;
        return item <= 3;
    }
}
```

The generated `TryMoveNext(out T item)` delegates to the annotated step method and updates `Current` when an item is produced. `GetEnumerator()` returns a struct enumerator implementing `IEnumerator<T>`.

## Diagnostics

- `PKIT001`: iterator host must be partial.
- `PKIT002`: no iterator step method found.
- `PKIT003`: multiple iterator step methods found.
- `PKIT004`: iterator step signature invalid.
