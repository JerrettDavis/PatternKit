using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.CacheStampedeProtection;
using PatternKit.Generators.CacheStampedeProtection;

namespace PatternKit.Examples.CacheStampedeProtectionDemo;

public sealed record ProductAvailabilityRequest(string Sku, string Region);

public sealed record ProductAvailabilitySnapshot(string Sku, string Region, int Available, DateTimeOffset LoadedAt);

public sealed record ProductAvailabilitySummary(ProductAvailabilitySnapshot Snapshot, bool SharedFlight, int OriginLoadCount);

public static partial class ProductCatalogStampedeProtectionPolicies
{
    public static CacheStampedeProtectionPolicy<ProductAvailabilitySnapshot> CreateFluent()
        => CacheStampedeProtectionPolicy<ProductAvailabilitySnapshot>.Create("product-catalog-single-flight").Build();
}

[GenerateCacheStampedeProtection(typeof(ProductAvailabilitySnapshot), FactoryMethodName = "CreateGenerated", PolicyName = "product-catalog-single-flight")]
public static partial class GeneratedProductCatalogStampedeProtectionPolicy;

public sealed class ProductCatalogOrigin
{
    private int _loads;

    public int LoadCount => Volatile.Read(ref _loads);

    public async ValueTask<ProductAvailabilitySnapshot> LoadAsync(ProductAvailabilityRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        Interlocked.Increment(ref _loads);
        await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        return new ProductAvailabilitySnapshot(request.Sku, request.Region, 42, DateTimeOffset.UtcNow);
    }
}

public sealed class ProductCatalogStampedeProtectionService(
    CacheStampedeProtectionPolicy<ProductAvailabilitySnapshot> policy,
    ProductCatalogOrigin origin)
{
    public async ValueTask<ProductAvailabilitySummary> GetAvailabilityAsync(ProductAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var key = $"{request.Region}:{request.Sku}";
        var result = await policy.GetOrLoadAsync(
            key,
            ct => origin.LoadAsync(request, ct),
            cancellationToken).ConfigureAwait(false);

        return new(result.Value, result.SharedFlight, origin.LoadCount);
    }
}

public sealed class ProductCatalogStampedeProtectionDemoRunner(ProductCatalogStampedeProtectionService service)
{
    public async ValueTask<IReadOnlyList<ProductAvailabilitySummary>> RunGeneratedAsync(ProductAvailabilityRequest request)
    {
        return await RunConcurrentLoadsAsync(service, request).ConfigureAwait(false);
    }

    public static async ValueTask<IReadOnlyList<ProductAvailabilitySummary>> RunFluentAsync(ProductAvailabilityRequest request)
    {
        var origin = new ProductCatalogOrigin();
        var service = new ProductCatalogStampedeProtectionService(ProductCatalogStampedeProtectionPolicies.CreateFluent(), origin);
        return await RunConcurrentLoadsAsync(service, request).ConfigureAwait(false);
    }

    public static async ValueTask<IReadOnlyList<ProductAvailabilitySummary>> RunGeneratedStaticAsync(ProductAvailabilityRequest request)
    {
        var origin = new ProductCatalogOrigin();
        var service = new ProductCatalogStampedeProtectionService(GeneratedProductCatalogStampedeProtectionPolicy.CreateGenerated(), origin);
        return await RunConcurrentLoadsAsync(service, request).ConfigureAwait(false);
    }

    private static async ValueTask<IReadOnlyList<ProductAvailabilitySummary>> RunConcurrentLoadsAsync(
        ProductCatalogStampedeProtectionService service,
        ProductAvailabilityRequest request)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = WaitThenLoadAsync(start.Task, service, request);
        var second = WaitThenLoadAsync(start.Task, service, request);

        start.SetResult();
        return await Task.WhenAll(first, second).ConfigureAwait(false);
    }

    private static async Task<ProductAvailabilitySummary> WaitThenLoadAsync(
        Task start,
        ProductCatalogStampedeProtectionService service,
        ProductAvailabilityRequest request)
    {
        await start.ConfigureAwait(false);
        return await service.GetAvailabilityAsync(request).ConfigureAwait(false);
    }
}

public static class ProductCatalogStampedeProtectionServiceCollectionExtensions
{
    public static IServiceCollection AddProductCatalogStampedeProtectionDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedProductCatalogStampedeProtectionPolicy.CreateGenerated());
        services.AddSingleton<ProductCatalogOrigin>();
        services.AddSingleton<ProductCatalogStampedeProtectionService>();
        services.AddSingleton<ProductCatalogStampedeProtectionDemoRunner>();
        return services;
    }
}
