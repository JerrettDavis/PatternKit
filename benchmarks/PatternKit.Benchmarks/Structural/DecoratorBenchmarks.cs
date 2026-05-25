using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Decorators;
using PatternKit.Structural.Decorator;

namespace PatternKit.Benchmarks.Structural;

[BenchmarkCategory("Structural", "GoF", "Decorator")]
public class DecoratorBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create decorator")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Decorator<string, string> Fluent_CreateDecorator()
        => Decorator<string, string>
            .Create(static path => $"content:{path}")
            .Before(static path => path.Trim())
            .After(static (path, content) => $"{path}:{content.Length}")
            .Build();

    [Benchmark(Description = "Generated: create decorator")]
    [BenchmarkCategory("Generated", "Construction")]
    public IFileStorage Generated_CreateDecorator()
        => FileStorageDecorators.Compose(
            new InMemoryFileStorage(),
            static inner => new MetricsFileStorage(inner));

    [Benchmark(Description = "Fluent: read decorated content")]
    [BenchmarkCategory("Fluent", "Execution")]
    public string Fluent_ReadDecoratedContent()
        => Fluent_CreateDecorator().Execute("orders.json");

    [Benchmark(Description = "Generated: read decorated content")]
    [BenchmarkCategory("Generated", "Execution")]
    public int Generated_ReadDecoratedContent()
    {
        var storage = Generated_CreateDecorator();
        storage.WriteFile("orders.json", "processed");
        return storage.ReadFile("orders.json").Length;
    }
}

public sealed class MetricsFileStorage(IFileStorage inner) : FileStorageDecoratorBase(inner)
{
    public int Reads { get; private set; }

    public override string ReadFile(string path)
    {
        Reads++;
        return base.ReadFile(path);
    }
}
