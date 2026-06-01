using BenchmarkDotNet.Attributes;
using PatternKit.Examples.ChangeDataCaptureDemo;
using PatternKit.Messaging.ChangeDataCapture;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "ChangeDataCapture")]
public class ChangeDataCaptureBenchmarks
{
    private static readonly ProductMutation Mutation = new("sku-1", "Desk", 125m, 1);

    [Benchmark(Baseline = true, Description = "Fluent: create CDC pipeline")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ChangeDataCapturePipeline<ProductMutation, ProductChanged> Fluent_Create()
        => ProductCatalogChangeDataCapturePolicies.CreateFluent(
            new InMemoryChangeDataCaptureStore<ProductMutation, ProductChanged>(),
            new InMemoryProductChangePublisher());

    [Benchmark(Description = "Generated: create CDC pipeline")]
    [BenchmarkCategory("Generated", "Construction")]
    public ChangeDataCapturePipeline<ProductMutation, ProductChanged> Generated_Create()
        => GeneratedProductCatalogChangeDataCapture.CreateGenerated(
            static (_, _) => default,
            new InMemoryChangeDataCaptureStore<ProductMutation, ProductChanged>());

    [Benchmark(Description = "Fluent: capture and publish mutation")]
    [BenchmarkCategory("Fluent", "Execution")]
    public async ValueTask<ChangeDataCapturePublishSummary> Fluent_Capture_And_Publish()
    {
        var pipeline = Fluent_Create();
        await pipeline.CaptureAsync(Mutation).ConfigureAwait(false);
        return await pipeline.PublishPendingAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "Generated: capture and publish mutation")]
    [BenchmarkCategory("Generated", "Execution")]
    public async ValueTask<ChangeDataCapturePublishSummary> Generated_Capture_And_Publish()
    {
        var pipeline = Generated_Create();
        await pipeline.CaptureAsync(Mutation).ConfigureAwait(false);
        return await pipeline.PublishPendingAsync().ConfigureAwait(false);
    }
}
