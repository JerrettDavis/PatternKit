# Iterator / Flow Pattern API Reference

Complete API documentation for the Iterator/Flow pattern in PatternKit.

## Namespace

```csharp
using PatternKit.Behavioral.Iterator;
```

---

## Flow\<T\>

Fluent functional pipeline for transforming sequences.

```csharp
public sealed class Flow<T> : IEnumerable<T>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `T` | Element type in the flow |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `From(IEnumerable<T>)` | `Flow<T>` | Create flow from enumerable |
| `From(params T[])` | `Flow<T>` | Create flow from array |
| `Empty()` | `Flow<T>` | Create empty flow |

### Transformation Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Map<TOut>(Func<T, TOut>)` | `Flow<TOut>` | Transform each element |
| `Filter(Func<T, bool>)` | `Flow<T>` | Keep elements matching predicate |
| `FlatMap<TOut>(Func<T, IEnumerable<TOut>>)` | `Flow<TOut>` | One-to-many transformation |
| `Tee(Action<T>)` | `Flow<T>` | Side effect, returns same element |

### Terminal Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `ToList()` | `List<T>` | Materialize to list |
| `ToArray()` | `T[]` | Materialize to array |
| `FirstOption()` | `Option<T>` | Get first element as option |
| `Fold<TAcc>(TAcc, Func<TAcc, T, TAcc>)` | `TAcc` | Reduce to single value |

### Sharing Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Share()` | `SharedFlow<T>` | Enable replay/forking |

### Example

```csharp
var result = Flow<int>.From(Enumerable.Range(1, 10))
    .Map(x => x * 2)
    .Filter(x => x > 10)
    .FlatMap(x => new[] { x, x + 1 })
    .Tee(Console.WriteLine)
    .ToList();
```

---

## SharedFlow\<T\>

Forkable/branchable flow with replay capability.

```csharp
public sealed class SharedFlow<T>
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Fork()` | `Flow<T>` | Create independent reader |
| `Fork(int count)` | `Flow<T>[]` | Create multiple independent readers |
| `Branch(Func<T, bool>)` | `(Flow<T> True, Flow<T> False)` | Partition by predicate |
| `AsFlow()` | `Flow<T>` | Convert back to regular flow |

### Transformation Shortcuts

| Method | Returns | Description |
|--------|---------|-------------|
| `Map<TOut>(Func<T, TOut>)` | `Flow<TOut>` | Fork then map |
| `Filter(Func<T, bool>)` | `Flow<T>` | Fork then filter |

### Example

```csharp
var shared = Flow<int>.From(Enumerable.Range(1, 10)).Share();

// Multiple independent consumers
var doubled = shared.Fork().Map(x => x * 2).ToList();
var filtered = shared.Fork().Filter(x => x > 5).ToList();

// Partition
var (evens, odds) = shared.Branch(x => x % 2 == 0);
var evenSum = evens.Fold(0, (a, x) => a + x);
var oddSum = odds.Fold(0, (a, x) => a + x);
```

---

## FlowExtensions

Extension methods for Flow operations.

```csharp
public static class FlowExtensions
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Fold<T, TAcc>(this Flow<T>, TAcc, Func<TAcc, T, TAcc>)` | `TAcc` | Reduce flow to single value |
| `FirstOption<T>(this Flow<T>)` | `Option<T>` | Get first element |

---

## ReplayableSequence\<T\>

Multi-cursor random access sequence.

```csharp
public sealed class ReplayableSequence<T>
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Number of buffered elements |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `CreateCursor()` | `Cursor` | Create new cursor at start |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `From(IEnumerable<T>)` | `ReplayableSequence<T>` | Create from enumerable |

### Example

```csharp
var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 100));

var cursor1 = seq.CreateCursor();
var cursor2 = seq.CreateCursor();

// Cursors can move independently
cursor1.MoveNext(); // at 1
cursor1.MoveNext(); // at 2
cursor2.MoveNext(); // at 1 (independent)
```

---

## ReplayableSequence\<T\>.Cursor

Independent reader with position tracking.

```csharp
public sealed class Cursor
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Current` | `T` | Current element |
| `Position` | `int` | Current position (0-based) |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `MoveNext()` | `bool` | Advance to next element |
| `Reset()` | `void` | Return to start |
| `Peek(int offset)` | `T?` | Look ahead without moving |

---

## WindowSequence\<T\>

Sliding/striding window over a sequence.

```csharp
public sealed class WindowSequence<T> : IEnumerable<IReadOnlyList<T>>
```

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create(IEnumerable<T>, int windowSize)` | `WindowSequence<T>` | Sliding window (stride=1) |
| `Create(IEnumerable<T>, int windowSize, int stride)` | `WindowSequence<T>` | Custom stride |

### Example

```csharp
// Sliding window of 3
var windows = WindowSequence<int>.Create(
    Enumerable.Range(1, 5),
    windowSize: 3);
// yields: [1,2,3], [2,3,4], [3,4,5]

// Striding window (batch)
var batches = WindowSequence<int>.Create(
    Enumerable.Range(1, 10),
    windowSize: 3,
    stride: 3);
// yields: [1,2,3], [4,5,6], [7,8,9], [10]
```

---

## Operator Behavior

### Laziness

All operators are lazy - they don't execute until enumeration:

```csharp
var flow = Flow<int>.From(GetExpensiveData())
    .Map(x => x * 2)
    .Filter(x => x > 10);
// Nothing executed yet

var list = flow.ToList(); // Now executes
```

### Sharing vs Non-Sharing

```csharp
// Without Share: upstream re-executes
var flow = Flow<int>.From(ExpensiveSource());
var list1 = flow.ToList(); // Executes source
var list2 = flow.ToList(); // Executes source again!

// With Share: upstream executes once
var shared = Flow<int>.From(ExpensiveSource()).Share();
var list1 = shared.Fork().ToList(); // Executes source, buffers
var list2 = shared.Fork().ToList(); // Reads from buffer
```

---

## Thread Safety

| Component | Thread-Safe |
|-----------|-------------|
| `Flow<T>` | No - single-threaded enumeration |
| `SharedFlow<T>` | No - single-threaded |
| `ReplayableSequence<T>` | No - single-threaded |
| `Cursor` | No - single-threaded |
| `WindowSequence<T>` | No - single-threaded |

### Design Notes

- Flow is designed for single-threaded functional composition
- For parallel processing, materialize to list first then use PLINQ
- SharedFlow buffers elements; dispose references when done

---

## Complete Example

```csharp
using PatternKit.Behavioral.Iterator;

// Data processing pipeline
var salesData = Flow<SalesRecord>.From(LoadSalesRecords())
    .Filter(r => r.Date >= startDate && r.Date <= endDate)
    .Map(r => new
    {
        r.ProductId,
        r.Amount,
        Region = GetRegion(r.StoreId)
    })
    .Tee(r => Console.WriteLine($"Processing: {r.ProductId}"));

// Share for multiple aggregations
var shared = salesData.Share();

// Regional totals
var (westSales, eastSales) = shared.Branch(r => r.Region == "West");
var westTotal = westSales.Fold(0m, (acc, r) => acc + r.Amount);
var eastTotal = eastSales.Fold(0m, (acc, r) => acc + r.Amount);

// Product breakdown
var byProduct = shared.Fork()
    .ToList()
    .GroupBy(r => r.ProductId)
    .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

Console.WriteLine($"West: {westTotal:C}, East: {eastTotal:C}");
```

---

## Comparison with Related Types

| Type | Use Case | Key Feature |
|------|----------|-------------|
| `Flow<T>` | Functional chains | Map/Filter/FlatMap |
| `SharedFlow<T>` | Multiple consumers | Fork/Branch |
| `ReplayableSequence<T>` | Random access | Multi-cursor |
| `WindowSequence<T>` | Batch processing | Sliding/striding |

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [Real-World Examples](real-world-examples.md)
