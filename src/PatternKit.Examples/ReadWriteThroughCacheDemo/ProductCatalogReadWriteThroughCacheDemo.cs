using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.ReadWriteThroughCache;
using PatternKit.Generators.ReadWriteThroughCache;

namespace PatternKit.Examples.ReadWriteThroughCacheDemo;

public sealed record CatalogProduct(string Sku, string Name, decimal Price);

public sealed class ProductCatalogReadWriteRepository(params CatalogProduct[] products)
{
    private readonly Dictionary<string, CatalogProduct> _products = products.ToDictionary(static p => p.Sku, StringComparer.OrdinalIgnoreCase);

    public int Reads { get; private set; }
    public int Writes { get; private set; }

    public ValueTask<CatalogProduct?> FindAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        cancellationToken.ThrowIfCancellationRequested();
        Reads++;
        return new(_products.TryGetValue(sku, out var product) ? product : null);
    }

    public ValueTask SaveAsync(CatalogProduct product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        cancellationToken.ThrowIfCancellationRequested();
        Writes++;
        _products[product.Sku] = product;
        return ValueTask.CompletedTask;
    }
}

public sealed record ProductCatalogReadWriteSummary(CatalogProduct? Product, bool Found, bool CacheHit, bool Written, int OriginReads, int OriginWrites);

public sealed class ProductCatalogReadWriteThroughCacheService(
    ProductCatalogReadWriteRepository repository,
    ReadWriteThroughCachePolicy<CatalogProduct> policy)
{
    public async ValueTask<ProductCatalogReadWriteSummary> FindAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        var result = await policy.ReadThroughAsync(sku, ct => repository.FindAsync(sku, ct), cancellationToken).ConfigureAwait(false);
        return new(result.Value, result.Found, result.CacheHit, result.Written, repository.Reads, repository.Writes);
    }

    public async ValueTask<ProductCatalogReadWriteSummary> SaveAsync(CatalogProduct product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        var result = await policy.WriteThroughAsync(product.Sku, product, (value, ct) => repository.SaveAsync(value, ct), cancellationToken).ConfigureAwait(false);
        return new(result.Value, result.Found, result.CacheHit, result.Written, repository.Reads, repository.Writes);
    }
}

public static partial class ProductCatalogReadWriteThroughPolicies
{
    public static ReadWriteThroughCachePolicy<CatalogProduct> CreateFluent()
        => ReadWriteThroughCachePolicy<CatalogProduct>
            .Create("product-catalog-read-write-through")
            .WithTimeToLive(TimeSpan.FromMinutes(5))
            .Build();
}

[GenerateReadWriteThroughCachePolicy(
    typeof(CatalogProduct),
    FactoryMethodName = "CreateGenerated",
    PolicyName = "product-catalog-read-write-through",
    TimeToLiveMilliseconds = 300000)]
public static partial class GeneratedProductCatalogReadWriteThroughPolicy;

public sealed class ProductCatalogReadWriteThroughDemoRunner(ProductCatalogReadWriteThroughCacheService service)
{
    public async ValueTask<IReadOnlyList<ProductCatalogReadWriteSummary>> RunAsync()
    {
        var first = await service.FindAsync("SKU-42").ConfigureAwait(false);
        var second = await service.FindAsync("SKU-42").ConfigureAwait(false);
        var write = await service.SaveAsync(new CatalogProduct("SKU-42", "Trail Jacket", 139m)).ConfigureAwait(false);
        var afterWrite = await service.FindAsync("SKU-42").ConfigureAwait(false);
        return [first, second, write, afterWrite];
    }
}

public static class ProductCatalogReadWriteThroughServiceCollectionExtensions
{
    public static IServiceCollection AddProductCatalogReadWriteThroughDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedProductCatalogReadWriteThroughPolicy.CreateGenerated());
        services.AddSingleton(static _ => new ProductCatalogReadWriteRepository(new CatalogProduct("SKU-42", "Trail Jacket", 129m)));
        services.AddSingleton<ProductCatalogReadWriteThroughCacheService>();
        services.AddSingleton<ProductCatalogReadWriteThroughDemoRunner>();
        return services;
    }
}
