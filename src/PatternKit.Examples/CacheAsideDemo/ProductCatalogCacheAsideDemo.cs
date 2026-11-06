using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.CacheAside;
using PatternKit.Generators.CacheAside;

namespace PatternKit.Examples.CacheAsideDemo;

public sealed record ProductReadModel(string Sku, string Name, decimal Price, bool Active);

public interface IProductCatalogRepository
{
    ValueTask<ProductReadModel?> FindAsync(string sku, CancellationToken cancellationToken = default);
}

public sealed class ScriptedProductCatalogRepository(params ProductReadModel[] products) : IProductCatalogRepository
{
    private readonly Dictionary<string, ProductReadModel> _products = products.ToDictionary(static p => p.Sku, StringComparer.OrdinalIgnoreCase);

    public int Calls { get; private set; }

    public ValueTask<ProductReadModel?> FindAsync(string sku, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return new(_products.TryGetValue(sku, out var product) ? product : null);
    }
}

public sealed class ProductCatalogCacheAsideService(
    IProductCatalogRepository repository,
    CacheAsidePolicy<ProductReadModel> policy)
{
    public async ValueTask<ProductCatalogLookup> FindAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var result = await policy.GetOrLoadAsync(
            sku,
            ct => repository.FindAsync(sku, ct),
            cancellationToken);

        return new ProductCatalogLookup(
            sku,
            result.Value,
            result.Found,
            result.CacheHit);
    }

    public bool Invalidate(string sku) => policy.Invalidate(sku);
}

public sealed record ProductCatalogLookup(
    string Sku,
    ProductReadModel? Product,
    bool Found,
    bool CacheHit);

public static partial class ProductCatalogCacheAsidePolicies
{
    public static CacheAsidePolicy<ProductReadModel> CreateFluentPolicy()
        => CacheAsidePolicy<ProductReadModel>
            .Create("product-catalog")
            .WithTimeToLive(TimeSpan.FromMinutes(5))
            .CacheWhen(static product => product.Active)
            .Build();
}

[GenerateCacheAsidePolicy(
    typeof(ProductReadModel),
    FactoryMethodName = "CreateGeneratedPolicy",
    PolicyName = "product-catalog",
    TimeToLiveMilliseconds = 300000)]
public static partial class GeneratedProductCatalogCacheAsidePolicy
{
    [CacheAsidePredicate]
    private static bool ShouldCache(ProductReadModel product)
        => product.Active;
}

public static class ProductCatalogCacheAsideDemoServiceCollectionExtensions
{
    public static IServiceCollection AddProductCatalogCacheAsideDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedProductCatalogCacheAsidePolicy.CreateGeneratedPolicy());
        services.AddSingleton<ScriptedProductCatalogRepository>(static _ => new(
            new ProductReadModel("SKU-42", "Trail Jacket", 129m, true),
            new ProductReadModel("SKU-99", "Retired Boot", 89m, false)));
        services.AddSingleton<IProductCatalogRepository>(static sp => sp.GetRequiredService<ScriptedProductCatalogRepository>());
        services.AddSingleton<ProductCatalogCacheAsideService>();
        return services;
    }
}
