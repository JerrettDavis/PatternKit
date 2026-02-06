using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

/// <summary>
/// Tests for the Iterator pattern generator (state-machine and traversal models).
/// </summary>
public class IteratorGeneratorTests
{
    #region State-Machine Iterator Tests

    [Fact]
    public void Generates_Iterator_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Iterator;

            namespace TestApp;

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
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Iterator_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("RangeIterator.Iterator.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Enumerator_And_TryMoveNext()
    {
        var source = """
            using PatternKit.Generators.Iterator;

            namespace TestApp;

            [Iterator]
            public partial class CountIterator
            {
                [IteratorStep]
                private bool TryStep(ref int state, out int item)
                {
                    if (state < 3)
                    {
                        item = state;
                        state++;
                        return true;
                    }
                    item = default;
                    return false;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Enumerator_And_TryMoveNext));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("CountIterator"))
            .SourceText.ToString();

        Assert.Contains("public bool TryMoveNext(", generatedSource);
        Assert.Contains("public Enumerator GetEnumerator()", generatedSource);
        Assert.Contains("public struct Enumerator", generatedSource);
        Assert.Contains("public int Current =>", generatedSource);
        Assert.Contains("public bool MoveNext()", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Iterator_For_Struct()
    {
        var source = """
            using PatternKit.Generators.Iterator;

            namespace TestApp;

            [Iterator]
            public partial struct StructIterator
            {
                private readonly int _max;

                public StructIterator(int max) => _max = max;

                [IteratorStep]
                private bool TryStep(ref int state, out int item)
                {
                    if (state < _max) { item = state++; return true; }
                    item = default; return false;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Iterator_For_Struct));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Traversal Iterator Tests

    [Fact]
    public void Generates_DFS_Traversal_Without_Diagnostics()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Iterator;

            namespace TestApp;

            public class TreeNode
            {
                public string Name { get; set; } = "";
                public List<TreeNode> Children { get; set; } = new();
            }

            [TraversalIterator]
            public static partial class TreeTraversal
            {
                [TraversalChildren]
                private static IEnumerable<TreeNode> GetChildren(TreeNode node) => node.Children;

                [DepthFirst]
                public static partial List<TreeNode> DepthFirst(TreeNode root);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_DFS_Traversal_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("TreeTraversal.Traversal.g.cs", names);

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("TreeTraversal"))
            .SourceText.ToString();

        Assert.Contains("Stack<", generatedSource);
        Assert.Contains("GetChildren(node)", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_BFS_Traversal_Without_Diagnostics()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Iterator;

            namespace TestApp;

            public class GraphNode
            {
                public string Id { get; set; } = "";
                public List<GraphNode> Neighbors { get; set; } = new();
            }

            [TraversalIterator]
            public static partial class GraphTraversal
            {
                [TraversalChildren]
                private static IEnumerable<GraphNode> GetNeighbors(GraphNode node) => node.Neighbors;

                [BreadthFirst]
                public static partial List<GraphNode> BreadthFirst(GraphNode root);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_BFS_Traversal_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("GraphTraversal"))
            .SourceText.ToString();

        Assert.Contains("Queue<", generatedSource);
        Assert.Contains("GetNeighbors(node)", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    #endregion

    #region Diagnostic Tests

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var source = """
            using PatternKit.Generators.Iterator;

            namespace TestApp;

            [Iterator]
            public class NotPartialIterator
            {
                [IteratorStep]
                private bool TryStep(ref int state, out int item)
                { item = state++; return state < 5; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Type_Not_Partial));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKIT001");
    }

    [Fact]
    public void Reports_Error_When_No_Step()
    {
        var source = """
            using PatternKit.Generators.Iterator;

            namespace TestApp;

            [Iterator]
            public partial class EmptyIterator
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_No_Step));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKIT002");
    }

    [Fact]
    public void Reports_Error_When_Multiple_Steps()
    {
        var source = """
            using PatternKit.Generators.Iterator;

            namespace TestApp;

            [Iterator]
            public partial class MultiStepIterator
            {
                [IteratorStep]
                private bool Step1(ref int state, out int item) { item = 0; return false; }

                [IteratorStep]
                private bool Step2(ref int state, out int item) { item = 0; return false; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Multiple_Steps));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKIT003");
    }

    [Fact]
    public void Reports_Error_When_Invalid_Step_Signature()
    {
        var source = """
            using PatternKit.Generators.Iterator;

            namespace TestApp;

            [Iterator]
            public partial class BadSignatureIterator
            {
                [IteratorStep]
                private int BadStep(int state) => state + 1;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Invalid_Step_Signature));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKIT004");
    }

    [Fact]
    public void Reports_Error_When_Traversal_Not_Static()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Iterator;

            namespace TestApp;

            public class Node { public List<Node> Kids { get; set; } = new(); }

            [TraversalIterator]
            public partial class NotStaticTraversal
            {
                [TraversalChildren]
                private static IEnumerable<Node> GetKids(Node n) => n.Kids;

                [DepthFirst]
                public static partial List<Node> Dfs(Node root);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Traversal_Not_Static));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKIT005");
    }

    [Fact]
    public void Reports_Error_When_No_Children_Provider()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Iterator;

            namespace TestApp;

            public class Node2 { public List<Node2> Kids { get; set; } = new(); }

            [TraversalIterator]
            public static partial class NoChildrenTraversal
            {
                [DepthFirst]
                public static partial List<Node2> Dfs(Node2 root);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_No_Children_Provider));
        _ = RoslynTestHelpers.Run(comp, new IteratorGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKIT007");
    }

    #endregion
}
