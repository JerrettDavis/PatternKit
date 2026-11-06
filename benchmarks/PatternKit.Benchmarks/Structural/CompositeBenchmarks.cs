using BenchmarkDotNet.Attributes;
using PatternKit.Generators.Composite;
using PatternKit.Structural.Composite;

namespace PatternKit.Benchmarks.Structural;

[BenchmarkCategory("Structural", "GoF", "Composite")]
public class CompositeBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create composite")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Composite<string, long> Fluent_CreateComposite()
        => Composite<string, long>
            .Node(static (in string _) => 0L, static (in string _, long total, long child) => total + child)
            .AddChild(Composite<string, long>.Leaf(static (in string _) => 2_048L))
            .AddChild(Composite<string, long>.Leaf(static (in string _) => 4_096L))
            .AddChild(Composite<string, long>.Leaf(static (in string _) => 8_192L))
            .Build();

    [Benchmark(Description = "Generated: create composite")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedFolder Generated_CreateComposite()
        => CreateGeneratedFolder();

    [Benchmark(Description = "Fluent: calculate tree size")]
    [BenchmarkCategory("Fluent", "Execution")]
    public long Fluent_CalculateTreeSize()
        => Fluent_CreateComposite().Execute("");

    [Benchmark(Description = "Generated: calculate tree size")]
    [BenchmarkCategory("Generated", "Execution")]
    public long Generated_CalculateTreeSize()
        => CreateGeneratedFolder().TotalBytes();

    private static GeneratedFolder CreateGeneratedFolder()
    {
        var folder = new GeneratedFolder("root");
        folder.Add(new GeneratedFile("readme.md", 2_048));
        folder.Add(new GeneratedFile("orders.json", 4_096));
        folder.Add(new GeneratedFile("archive.zip", 8_192));
        return folder;
    }
}

[CompositeComponent(GenerateTraversalHelpers = true)]
public partial interface IGeneratedStorageNode
{
    string Name { get; }
}

public sealed class GeneratedFile : GeneratedStorageNodeComponentBase
{
    private readonly long _bytes;

    public GeneratedFile(string name, long bytes)
    {
        Name = name;
        _bytes = bytes;
    }

    public override string Name { get; }

    public long TotalBytes() => _bytes;
}

public sealed class GeneratedFolder : GeneratedStorageNodeCompositeBase
{
    public GeneratedFolder(string name) => Name = name;

    public override string Name { get; }

    public long TotalBytes()
        => Children.Sum(static child => child switch
        {
            GeneratedFile file => file.TotalBytes(),
            GeneratedFolder folder => folder.TotalBytes(),
            _ => 0L
        });
}
