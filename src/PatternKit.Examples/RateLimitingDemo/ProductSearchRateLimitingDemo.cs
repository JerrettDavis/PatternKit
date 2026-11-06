using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.RateLimiting;
using PatternKit.Generators.RateLimiting;

namespace PatternKit.Examples.RateLimitingDemo;

public sealed record SearchResponse(string TenantId, string Query, int TotalResults);

public interface IProductSearchService
{
    ValueTask<SearchResponse> SearchAsync(string tenantId, string query, CancellationToken cancellationToken = default);
}

public sealed class ScriptedProductSearchService : IProductSearchService
{
    public int Calls { get; private set; }

    public ValueTask<SearchResponse> SearchAsync(string tenantId, string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        cancellationToken.ThrowIfCancellationRequested();

        Calls++;
        return new(new SearchResponse(tenantId, query, TotalResults: query.Length * 3));
    }
}

public sealed class ProductSearchRateLimitService(
    IProductSearchService search,
    RateLimitPolicy<SearchResponse> policy)
{
    public async ValueTask<ProductSearchRateLimitResult> SearchAsync(
        string tenantId,
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var result = await policy.ExecuteAsync(
            tenantId,
            ct => search.SearchAsync(tenantId, query, ct),
            cancellationToken);

        return new ProductSearchRateLimitResult(
            tenantId,
            query,
            result.Allowed,
            result.Rejected,
            result.RemainingPermits,
            result.RetryAfter,
            result.Value?.TotalResults ?? 0);
    }
}

public sealed record ProductSearchRateLimitResult(
    string TenantId,
    string Query,
    bool Allowed,
    bool Rejected,
    int RemainingPermits,
    DateTimeOffset? RetryAfter,
    int TotalResults);

public static partial class ProductSearchRateLimitPolicies
{
    public static RateLimitPolicy<SearchResponse> CreateFluentPolicy()
        => RateLimitPolicy<SearchResponse>
            .Create("product-search")
            .WithPermitLimit(2)
            .WithWindow(TimeSpan.FromMinutes(1))
            .Build();
}

[GenerateRateLimitPolicy(
    typeof(SearchResponse),
    FactoryMethodName = "CreateGeneratedPolicy",
    PolicyName = "product-search",
    PermitLimit = 2,
    WindowMilliseconds = 60000)]
public static partial class GeneratedProductSearchRateLimitPolicy;

public static class ProductSearchRateLimitingDemoServiceCollectionExtensions
{
    public static IServiceCollection AddProductSearchRateLimitingDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedProductSearchRateLimitPolicy.CreateGeneratedPolicy());
        services.AddSingleton<ScriptedProductSearchService>();
        services.AddSingleton<IProductSearchService>(static sp => sp.GetRequiredService<ScriptedProductSearchService>());
        services.AddSingleton<ProductSearchRateLimitService>();
        return services;
    }
}
