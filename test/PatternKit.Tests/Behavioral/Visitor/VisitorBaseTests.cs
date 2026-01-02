using PatternKit.Behavioral.Visitor;

namespace PatternKit.Tests.Behavioral.Visitor;

#region VisitorBase Tests (GoF Visitor Pattern with Double Dispatch)

/// <summary>
/// Tests for VisitorBase - the GoF Visitor pattern implementation with double dispatch.
/// </summary>
public sealed class VisitorBaseTests
{
    #region Test Types

    // Element hierarchy implementing IVisitable
    private abstract class Expression : IVisitable
    {
        public abstract TResult Accept<TResult>(IVisitor<TResult> visitor);
    }

    private sealed class NumberExpr : Expression
    {
        public int Value { get; init; }
        public override TResult Accept<TResult>(IVisitor<TResult> visitor)
            => visitor is EvaluatorVisitor ev ? (TResult)(object)ev.Visit(this) : visitor.VisitDefault(this);
    }

    private sealed class AddExpr : Expression
    {
        public required Expression Left { get; init; }
        public required Expression Right { get; init; }
        public override TResult Accept<TResult>(IVisitor<TResult> visitor)
            => visitor is EvaluatorVisitor ev ? (TResult)(object)ev.Visit(this) : visitor.VisitDefault(this);
    }

    private sealed class UnknownExpr : Expression
    {
        public override TResult Accept<TResult>(IVisitor<TResult> visitor)
            => visitor.VisitDefault(this);
    }

    // Concrete visitor implementing VisitorBase
    private sealed class EvaluatorVisitor : VisitorBase<Expression, int>
    {
        public int Visit(NumberExpr n) => n.Value;
        public int Visit(AddExpr a) => Visit(a.Left) + Visit(a.Right);
    }

    // Visitor that handles unknown types
    private sealed class SafeEvaluatorVisitor : VisitorBase<Expression, int>
    {
        public int Visit(NumberExpr n) => n.Value;
        public int Visit(AddExpr a) => Visit(a.Left) + Visit(a.Right);
        public override int VisitDefault(IVisitable element) => -1; // Return default instead of throwing
    }

    #endregion

    [Fact]
    public void VisitorBase_Visit_NumberExpr()
    {
        var visitor = new EvaluatorVisitor();
        var expr = new NumberExpr { Value = 42 };

        var result = visitor.Visit(expr);

        Assert.Equal(42, result);
    }

    [Fact]
    public void VisitorBase_Visit_AddExpr()
    {
        var visitor = new EvaluatorVisitor();
        var expr = new AddExpr
        {
            Left = new NumberExpr { Value = 10 },
            Right = new NumberExpr { Value = 20 }
        };

        var result = visitor.Visit(expr);

        Assert.Equal(30, result);
    }

    [Fact]
    public void VisitorBase_Visit_NestedAddExpr()
    {
        var visitor = new EvaluatorVisitor();
        var expr = new AddExpr
        {
            Left = new AddExpr
            {
                Left = new NumberExpr { Value = 1 },
                Right = new NumberExpr { Value = 2 }
            },
            Right = new NumberExpr { Value = 3 }
        };

        var result = visitor.Visit(expr);

        Assert.Equal(6, result);
    }

    [Fact]
    public void VisitorBase_VisitDefault_ThrowsNotSupported()
    {
        var visitor = new EvaluatorVisitor();
        var unknown = new UnknownExpr();

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Visit(unknown));
        Assert.Contains("UnknownExpr", ex.Message);
    }

    [Fact]
    public void VisitorBase_VisitDefault_Override_ReturnsCustomValue()
    {
        var visitor = new SafeEvaluatorVisitor();
        var unknown = new UnknownExpr();

        var result = visitor.Visit(unknown);

        Assert.Equal(-1, result);
    }
}

#endregion

#region ActionVisitorBase Tests

/// <summary>
/// Tests for ActionVisitorBase - the GoF Visitor pattern for void operations.
/// </summary>
public sealed class ActionVisitorBaseTests
{
    #region Test Types

    private abstract class Node : IActionVisitable
    {
        public abstract void Accept(IActionVisitor visitor);
    }

    private sealed class TextNode : Node
    {
        public required string Text { get; init; }
        public override void Accept(IActionVisitor visitor)
        {
            if (visitor is PrintVisitor pv) pv.Visit(this);
            else visitor.VisitDefault(this);
        }
    }

    private sealed class ContainerNode : Node
    {
        public required Node[] Children { get; init; }
        public override void Accept(IActionVisitor visitor)
        {
            if (visitor is PrintVisitor pv) pv.Visit(this);
            else visitor.VisitDefault(this);
        }
    }

    private sealed class UnknownNode : Node
    {
        public override void Accept(IActionVisitor visitor)
            => visitor.VisitDefault(this);
    }

    private sealed class PrintVisitor : ActionVisitorBase<Node>
    {
        private readonly List<string> _output = new();
        public IReadOnlyList<string> Output => _output;

        public void Visit(TextNode n) => _output.Add(n.Text);
        public void Visit(ContainerNode c)
        {
            _output.Add("[");
            foreach (var child in c.Children)
                Visit(child);
            _output.Add("]");
        }
    }

    private sealed class SafePrintVisitor : ActionVisitorBase<Node>
    {
        private readonly List<string> _output = new();
        public IReadOnlyList<string> Output => _output;

        public void Visit(TextNode n) => _output.Add(n.Text);
        public override void VisitDefault(IActionVisitable element) => _output.Add("unknown");
    }

    #endregion

    [Fact]
    public void ActionVisitorBase_Visit_TextNode()
    {
        var visitor = new PrintVisitor();
        var node = new TextNode { Text = "Hello" };

        visitor.Visit(node);

        Assert.Single(visitor.Output);
        Assert.Equal("Hello", visitor.Output[0]);
    }

    [Fact]
    public void ActionVisitorBase_Visit_ContainerNode()
    {
        var visitor = new PrintVisitor();
        var node = new ContainerNode
        {
            Children = new Node[]
            {
                new TextNode { Text = "A" },
                new TextNode { Text = "B" }
            }
        };

        visitor.Visit(node);

        Assert.Equal(new[] { "[", "A", "B", "]" }, visitor.Output);
    }

    [Fact]
    public void ActionVisitorBase_VisitDefault_ThrowsNotSupported()
    {
        var visitor = new PrintVisitor();
        var unknown = new UnknownNode();

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Visit(unknown));
        Assert.Contains("UnknownNode", ex.Message);
    }

    [Fact]
    public void ActionVisitorBase_VisitDefault_Override_HandlesUnknown()
    {
        var visitor = new SafePrintVisitor();
        var unknown = new UnknownNode();

        visitor.Visit(unknown);

        Assert.Single(visitor.Output);
        Assert.Equal("unknown", visitor.Output[0]);
    }
}

#endregion
