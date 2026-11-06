using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.Visitor;
using PatternKit.Generators.Visitors;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "GoF", "Visitor")]
public class VisitorBenchmarks
{
    private static readonly GeneratedNumberNode GeneratedNode = new() { Value = 42 };
    private static readonly FluentNumberNode FluentNode = new(42);

    [Benchmark(Baseline = true, Description = "Fluent: create visitor")]
    [BenchmarkCategory("Fluent", "Construction")]
    public FluentVisitor<FluentDocumentNode, string> Fluent_CreateVisitor()
        => FluentVisitor<FluentDocumentNode, string>.Create()
            .When<FluentNumberNode>(static node => $"number:{node.Value}")
            .When<FluentTextNode>(static node => $"text:{node.Text}")
            .Default(static _ => "unknown")
            .Build();

    [Benchmark(Description = "Generated: create visitor")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedDocumentNodeVisitorBuilder<string> Generated_CreateVisitorBuilder()
        => new();

    [Benchmark(Description = "Fluent: visit document node")]
    [BenchmarkCategory("Fluent", "Execution")]
    public string Fluent_VisitNode()
        => Fluent_CreateVisitor().Visit(FluentNode);

    [Benchmark(Description = "Generated: visit document node")]
    [BenchmarkCategory("Generated", "Execution")]
    public string Generated_VisitNode()
    {
        var visitor = new GeneratedDocumentNodeVisitorBuilder<string>()
            .When<GeneratedNumberNode>(static node => $"number:{node.Value}")
            .When<GeneratedTextNode>(static node => $"text:{node.Text}")
            .Default(static _ => "unknown")
            .Build();
        return GeneratedNode.Accept(visitor);
    }
}

public abstract class FluentDocumentNode : IVisitable
{
    public abstract TResult Accept<TResult>(IVisitor<TResult> visitor);
}

public sealed class FluentNumberNode(int value) : FluentDocumentNode
{
    public int Value { get; } = value;

    public override TResult Accept<TResult>(IVisitor<TResult> visitor)
        => visitor is FluentVisitor<FluentDocumentNode, TResult> fluent
            ? fluent.Handle(this)
            : visitor.VisitDefault(this);
}

public sealed class FluentTextNode(string text) : FluentDocumentNode
{
    public string Text { get; } = text;

    public override TResult Accept<TResult>(IVisitor<TResult> visitor)
        => visitor is FluentVisitor<FluentDocumentNode, TResult> fluent
            ? fluent.Handle(this)
            : visitor.VisitDefault(this);
}

[GenerateVisitor(GenerateActions = false, GenerateAsync = false)]
public abstract partial class GeneratedDocumentNode;

public partial class GeneratedNumberNode : GeneratedDocumentNode
{
    public int Value { get; init; }
}

public partial class GeneratedTextNode : GeneratedDocumentNode
{
    public string Text { get; init; } = string.Empty;
}
