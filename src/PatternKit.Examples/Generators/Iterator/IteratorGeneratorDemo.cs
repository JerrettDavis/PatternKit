using PatternKit.Generators.Iterator;

namespace PatternKit.Examples.Generators.Iterator;

#region State-Machine Iterator Example

/// <summary>
/// Demonstrates a state-machine iterator that yields Fibonacci numbers.
/// The generator creates a struct Enumerator and TryMoveNext from the step method.
/// </summary>
[Iterator]
public partial class FibonacciIterator
{
    private readonly int _count;

    /// <summary>
    /// Creates a Fibonacci iterator that yields the specified number of values.
    /// </summary>
    public FibonacciIterator(int count) => _count = count;

    /// <summary>
    /// State holds (a, b, index) packed into a tuple.
    /// </summary>
    [IteratorStep]
    private bool TryStep(ref (long a, long b, int index) state, out long item)
    {
        if (state.index >= _count)
        {
            item = default;
            return false;
        }

        if (state.index == 0)
        {
            item = 0;
            state = (0, 1, 1);
            return true;
        }

        if (state.index == 1)
        {
            item = 1;
            state = (0, 1, 2);
            return true;
        }

        item = state.a + state.b;
        state = (state.b, item, state.index + 1);
        return true;
    }
}

#endregion

#region Traversal Iterator Example

/// <summary>
/// A simple tree node for traversal demonstration.
/// </summary>
public class FileSystemNode
{
    /// <summary>Name of the file or directory.</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether this is a directory (has children) or a file.</summary>
    public bool IsDirectory { get; set; }

    /// <summary>Child nodes (files and subdirectories).</summary>
    public List<FileSystemNode> Children { get; set; } = new();

    /// <inheritdoc/>
    public override string ToString() => IsDirectory ? $"[{Name}/]" : Name;
}

/// <summary>
/// Demonstrates tree traversal generation. The generator produces DFS and BFS
/// implementations from the partial method declarations.
/// </summary>
[TraversalIterator]
public static partial class FileSystemTraversal
{
    [TraversalChildren]
    private static IEnumerable<FileSystemNode> GetChildren(FileSystemNode node) => node.Children;

    /// <summary>
    /// Traverses the file system tree in depth-first order.
    /// </summary>
    [DepthFirst]
    public static partial List<FileSystemNode> DepthFirst(FileSystemNode root);

    /// <summary>
    /// Traverses the file system tree in breadth-first order.
    /// </summary>
    [BreadthFirst]
    public static partial List<FileSystemNode> BreadthFirst(FileSystemNode root);
}

#endregion

/// <summary>
/// Runs the Iterator generator demonstration showing state-machine and traversal iterators.
/// </summary>
public static class IteratorGeneratorDemo
{
    /// <summary>
    /// Executes all iterator demonstrations and returns logged output lines.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();

        // --- Fibonacci Iterator ---
        log.Add("=== State-Machine Iterator: Fibonacci ===");

        var fib = new FibonacciIterator(10);
        var values = new List<long>();
        foreach (var value in fib)
        {
            values.Add(value);
        }
        log.Add($"  First 10 Fibonacci: {string.Join(", ", values)}");

        // Also demonstrate TryMoveNext directly
        var fib2 = new FibonacciIterator(5);
        var state = (a: 0L, b: 0L, index: 0);
        var directValues = new List<long>();
        while (fib2.TryMoveNext(ref state, out var item))
        {
            directValues.Add(item);
        }
        log.Add($"  TryMoveNext (5): {string.Join(", ", directValues)}");

        // --- Traversal Iterator ---
        log.Add("");
        log.Add("=== Traversal Iterator: File System ===");

        var root = new FileSystemNode
        {
            Name = "root", IsDirectory = true,
            Children =
            {
                new FileSystemNode
                {
                    Name = "src", IsDirectory = true,
                    Children =
                    {
                        new FileSystemNode { Name = "main.cs" },
                        new FileSystemNode { Name = "utils.cs" }
                    }
                },
                new FileSystemNode
                {
                    Name = "docs", IsDirectory = true,
                    Children =
                    {
                        new FileSystemNode { Name = "readme.md" }
                    }
                },
                new FileSystemNode { Name = "build.sh" }
            }
        };

        var dfsResult = FileSystemTraversal.DepthFirst(root);
        log.Add($"  DFS: {string.Join(" -> ", dfsResult.Select(n => n.ToString()))}");

        var bfsResult = FileSystemTraversal.BreadthFirst(root);
        log.Add($"  BFS: {string.Join(" -> ", bfsResult.Select(n => n.ToString()))}");

        return log;
    }
}
