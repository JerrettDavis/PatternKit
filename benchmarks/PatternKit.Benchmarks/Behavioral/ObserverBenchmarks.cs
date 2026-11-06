using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.Observer;
using PatternKit.Generators.Observer;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "GoF", "Observer")]
public class ObserverBenchmarks
{
    private static readonly InventoryChanged Event = new("SKU-100", 4);

    [Benchmark(Baseline = true, Description = "Fluent: create observer")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Observer<InventoryChanged> Fluent_CreateObserver()
        => Observer<InventoryChanged>.Create()
            .SwallowErrors()
            .Build();

    [Benchmark(Description = "Generated: create observer")]
    [BenchmarkCategory("Generated", "Construction")]
    public InventoryChangedObserver Generated_CreateObserver()
        => new();

    [Benchmark(Description = "Fluent: publish inventory event")]
    [BenchmarkCategory("Fluent", "Execution")]
    public int Fluent_PublishEvent()
    {
        var total = 0;
        var observer = Fluent_CreateObserver();
        observer.Subscribe((in InventoryChanged evt) => total += evt.Quantity);
        observer.Publish(Event);
        return total;
    }

    [Benchmark(Description = "Generated: publish inventory event")]
    [BenchmarkCategory("Generated", "Execution")]
    public int Generated_PublishEvent()
    {
        var total = 0;
        var observer = new InventoryChangedObserver();
        observer.Subscribe((InventoryChanged evt) => total += evt.Quantity);
        observer.Publish(Event);
        return total;
    }
}

public sealed record InventoryChanged(string Sku, int Quantity);

[Observer(typeof(InventoryChanged), Threading = ObserverThreadingPolicy.SingleThreadedFast)]
public partial class InventoryChangedObserver;
