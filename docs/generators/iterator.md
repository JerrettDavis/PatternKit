# Iterator Pattern Generator

The Iterator Pattern Generator automatically creates enumerator infrastructure from a user-provided step function. It supports two models: **state-machine** iterators (struct Enumerator with TryMoveNext) and **traversal** iterators (DFS/BFS tree walking).

## Overview

The generator produces:

- **TryMoveNext method** that delegates to the user's step function
- **Struct Enumerator** with Current, MoveNext, and GetEnumerator (foreach-compatible)
- **DFS traversal** using an explicit stack (no recursion)
- **BFS traversal** using a queue
- **Zero allocation** struct enumerator for hot paths

## Quick Start

### 1. State-Machine Iterator

Define a step function that advances state and yields items:

```csharp
using PatternKit.Generators.Iterator;

[Iterator]
public partial class RangeIterator
{
    private readonly int _max;

    public RangeIterator(int max) => _max = max;

    [IteratorStep]
    private bool TryStep(ref int state, out int item)
    {
        if (state < _max)
        {
            item = state;
            state++;
            return true;
        }
        item = default;
        return false;
    }
}
```

Generated usage:

```csharp
var range = new RangeIterator(5);

// foreach (uses generated struct Enumerator)
foreach (var i in range)
    Console.WriteLine(i); // 0, 1, 2, 3, 4

// Manual TryMoveNext
int state = 0;
while (range.TryMoveNext(ref state, out var item))
    Console.WriteLine(item);
```

### 2. Traversal Iterator

Define a children provider and partial traversal methods:

```csharp
[TraversalIterator]
public static partial class TreeWalker
{
    [TraversalChildren]
    private static IEnumerable<Node> GetChildren(Node node) => node.Children;

    [DepthFirst]
    public static partial List<Node> Dfs(Node root);

    [BreadthFirst]
    public static partial List<Node> Bfs(Node root);
}
```

Generated usage:

```csharp
var allNodes = TreeWalker.Dfs(rootNode);   // depth-first
var levels = TreeWalker.Bfs(rootNode);     // breadth-first
```

## Attributes

| Attribute | Target | Description |
|---|---|---|
| `[Iterator]` | Class/Struct | Marks the type as an iterator host |
| `[IteratorStep]` | Method | Marks the step function |
| `[TraversalIterator]` | Static Class | Marks the type as a traversal host |
| `[DepthFirst]` | Partial Method | Generates DFS implementation |
| `[BreadthFirst]` | Partial Method | Generates BFS implementation |
| `[TraversalChildren]` | Method | Provides children for traversal |

### IteratorAttribute Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `GenerateEnumerator` | `bool` | `true` | Generate struct Enumerator |
| `GenerateTryMoveNext` | `bool` | `true` | Generate TryMoveNext method |

### Step Method Signature

```csharp
bool TryStep(ref TState state, out T item)
```

- **TState**: Any type used to track iteration state
- **T**: The type of items yielded by the iterator
- Returns `true` to yield an item, `false` to end iteration

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| PKIT001 | Error | Type marked with `[Iterator]` must be partial |
| PKIT002 | Error | No step method found (exactly one `[IteratorStep]` required) |
| PKIT003 | Error | Multiple step methods found |
| PKIT004 | Error | Invalid step method signature |
| PKIT005 | Error | Traversal host must be a static partial class |
| PKIT006 | Error | Invalid traversal method signature |
| PKIT007 | Error | No `[TraversalChildren]` method found |

## Best Practices

- **Use value tuples for state** when you need multiple fields: `ref (int index, bool done) state`
- **Keep step functions pure** - side effects make iterators hard to reason about
- **Prefer struct state types** for zero-allocation iteration
- **Use DFS for tree processing** where parent context matters (e.g., path building)
- **Use BFS for level-order** operations (e.g., finding shortest paths)
- **Note**: DFS reverses child order on the stack to maintain left-to-right visitation

## See Also

- [Iterator Generator Example](../examples/iterator-generator-demo.md)
- [Chain Pattern Generator](chain.md) (sequential handler processing)
