# Iterator Generator Demo

## Goal

Demonstrate both state-machine and traversal iterator models using the `[Iterator]` and `[TraversalIterator]` source generators. The example shows a Fibonacci sequence iterator and a file system tree traversal.

## Key Idea

The Iterator generator creates enumerator infrastructure at compile time. For **state-machine** iterators, you provide a step function and get a struct Enumerator with foreach support. For **traversal** iterators, you declare partial methods and get DFS/BFS implementations.

## State-Machine Iterator: Fibonacci

A step function advances state and yields items:

```csharp
[Iterator]
public partial class FibonacciIterator
{
    private readonly int _count;
    public FibonacciIterator(int count) => _count = count;

    [IteratorStep]
    private bool TryStep(ref (long a, long b, int index) state, out long item)
    {
        if (state.index >= _count) { item = default; return false; }
        if (state.index <= 1) { item = state.index; state = (0, 1, state.index + 1); return true; }
        item = state.a + state.b;
        state = (state.b, item, state.index + 1);
        return true;
    }
}
```

Usage:

```csharp
var fib = new FibonacciIterator(10);
foreach (var value in fib)
    Console.Write($"{value} "); // 0 1 1 2 3 5 8 13 21 34
```

## Traversal Iterator: File System

Partial methods get DFS and BFS implementations:

```csharp
[TraversalIterator]
public static partial class FileSystemTraversal
{
    [TraversalChildren]
    private static IEnumerable<FileSystemNode> GetChildren(FileSystemNode node) => node.Children;

    [DepthFirst]
    public static partial List<FileSystemNode> DepthFirst(FileSystemNode root);

    [BreadthFirst]
    public static partial List<FileSystemNode> BreadthFirst(FileSystemNode root);
}
```

## Mental Model

**State-machine**: Think of a conveyor belt. Each call to `TryStep` moves the belt forward one position and produces the next item.

**Traversal**: Think of exploring a building. DFS goes room-by-room down each hallway before backtracking. BFS visits all rooms on the current floor before going deeper.

```
State-machine:  state0 -> step -> item0, state1 -> step -> item1, state2 -> step -> done

DFS (stack):    root -> src -> main.cs -> utils.cs -> docs -> readme.md -> build.sh
BFS (queue):    root -> src -> docs -> build.sh -> main.cs -> utils.cs -> readme.md
```

## Test References

- Generator tests: `test/PatternKit.Generators.Tests/IteratorGeneratorTests.cs`
- Example tests: `test/PatternKit.Examples.Tests/Generators/Iterator/IteratorGeneratorDemoTests.cs`
