using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.ChangeDataCapture;
using PatternKit.Messaging.ChangeDataCapture;

namespace PatternKit.Examples.ChangeDataCaptureDemo;

public sealed record ProductMutation(string Sku, string Name, decimal Price, long Version);
public sealed record ProductChanged(long Sequence, string Sku, string Name, decimal Price, long Version);

public interface IProductChangePublisher
{
    ValueTask PublishAsync(ProductChanged changed, CancellationToken cancellationToken = default);
}

public sealed class InMemoryProductChangePublisher : IProductChangePublisher
{
    private readonly List<ProductChanged> _published = [];

    public IReadOnlyList<ProductChanged> Published => _published;

    public ValueTask PublishAsync(ProductChanged changed, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _published.Add(changed);
        return default;
    }
}

public sealed class ProductCatalogChangeDataCaptureService(ChangeDataCapturePipeline<ProductMutation, ProductChanged> pipeline)
{
    public async ValueTask<ChangeDataCapturePublishSummary> UpsertAsync(ProductMutation mutation, CancellationToken cancellationToken = default)
    {
        await pipeline.CaptureAsync(mutation, cancellationToken).ConfigureAwait(false);
        return await pipeline.PublishPendingAsync(cancellationToken).ConfigureAwait(false);
    }
}

public static class ProductCatalogChangeDataCapturePolicies
{
    public static ChangeDataCapturePipeline<ProductMutation, ProductChanged> CreateFluent(
        IChangeDataCaptureStore<ProductMutation, ProductChanged> store,
        IProductChangePublisher publisher)
        => ChangeDataCapturePipeline<ProductMutation, ProductChanged>.Create("product-catalog-cdc")
            .UseStore(store)
            .MapWith(Map)
            .PublishWith((changed, ct) => publisher.PublishAsync(changed, ct))
            .Build();

    public static ProductChanged Map(ProductMutation mutation, long sequence)
        => new(sequence, mutation.Sku, mutation.Name, mutation.Price, mutation.Version);
}

[GenerateChangeDataCapture(
    typeof(ProductMutation),
    typeof(ProductChanged),
    FactoryMethodName = nameof(CreateGenerated),
    MapperMethodName = nameof(MapGenerated),
    PipelineName = "product-catalog-cdc")]
public static partial class GeneratedProductCatalogChangeDataCapture
{
    public static ProductChanged MapGenerated(ProductMutation mutation, long sequence)
        => new(sequence, mutation.Sku, mutation.Name, mutation.Price, mutation.Version);
}

public static class ProductCatalogChangeDataCaptureServiceCollectionExtensions
{
    public static IServiceCollection AddProductCatalogChangeDataCaptureDemo(this IServiceCollection services)
    {
        services.AddSingleton<IChangeDataCaptureStore<ProductMutation, ProductChanged>, InMemoryChangeDataCaptureStore<ProductMutation, ProductChanged>>();
        services.AddSingleton<InMemoryProductChangePublisher>();
        services.AddSingleton<IProductChangePublisher>(sp => sp.GetRequiredService<InMemoryProductChangePublisher>());
        services.AddSingleton(sp => ProductCatalogChangeDataCapturePolicies.CreateFluent(
            sp.GetRequiredService<IChangeDataCaptureStore<ProductMutation, ProductChanged>>(),
            sp.GetRequiredService<IProductChangePublisher>()));
        services.AddSingleton<ProductCatalogChangeDataCaptureService>();
        return services;
    }
}
