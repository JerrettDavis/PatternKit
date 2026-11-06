using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.Retry;
using PatternKit.Generators.Retry;

namespace PatternKit.Examples.RetryDemo;

public sealed record InventoryResponse(string Sku, int Available, int StatusCode)
{
    public bool IsAvailable => StatusCode == 200 && Available > 0;
}

public interface IInventoryClient
{
    ValueTask<InventoryResponse> GetAvailabilityAsync(string sku, CancellationToken cancellationToken = default);
}

public sealed class ScriptedInventoryClient(params InventoryResponse[] responses) : IInventoryClient
{
    private readonly Queue<InventoryResponse> _responses = new(responses);

    public int Calls { get; private set; }

    public ValueTask<InventoryResponse> GetAvailabilityAsync(string sku, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;

        if (_responses.Count == 0)
            return new(new InventoryResponse(sku, 0, 503));

        return new(_responses.Dequeue());
    }
}

public sealed class InventoryLookupService(
    IInventoryClient client,
    RetryPolicy<InventoryResponse> policy)
{
    public async ValueTask<InventoryLookupResult> CheckAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var result = await policy.ExecuteAsync(
            ct => client.GetAvailabilityAsync(sku, ct),
            cancellationToken);

        return new InventoryLookupResult(
            sku,
            result.Succeeded && result.Value is { IsAvailable: true },
            result.Attempts,
            result.Value?.Available ?? 0,
            result.Value?.StatusCode ?? 0);
    }
}

public sealed record InventoryLookupResult(
    string Sku,
    bool Available,
    int Attempts,
    int AvailableQuantity,
    int StatusCode);

public static partial class InventoryRetryPolicies
{
    public static RetryPolicy<InventoryResponse> CreateFluentPolicy()
        => RetryPolicy<InventoryResponse>
            .Create("inventory-availability")
            .WithMaxAttempts(3)
            .WithInitialDelay(TimeSpan.Zero)
            .WithExponentialBackoff(2)
            .HandleResult(static response => response.StatusCode == 408 || response.StatusCode == 429 || response.StatusCode >= 500)
            .HandleException(static exception => exception is TimeoutException)
            .Build();
}

[GenerateRetryPolicy(
    typeof(InventoryResponse),
    FactoryMethodName = "CreateGeneratedPolicy",
    PolicyName = "inventory-availability",
    MaxAttempts = 3,
    InitialDelayMilliseconds = 0,
    BackoffFactor = 2)]
public static partial class GeneratedInventoryRetryPolicy
{
    [RetryResultPredicate]
    private static bool ShouldRetry(InventoryResponse response)
        => response.StatusCode == 408 || response.StatusCode == 429 || response.StatusCode >= 500;

    [RetryExceptionPredicate]
    private static bool ShouldRetry(Exception exception)
        => exception is TimeoutException;
}

public static class InventoryRetryDemoServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryRetryDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedInventoryRetryPolicy.CreateGeneratedPolicy());
        services.AddSingleton<ScriptedInventoryClient>(static _ => new(
            new InventoryResponse("SKU-42", 0, 503),
            new InventoryResponse("SKU-42", 12, 200)));
        services.AddSingleton<IInventoryClient>(static sp => sp.GetRequiredService<ScriptedInventoryClient>());
        services.AddSingleton<InventoryLookupService>();
        return services;
    }
}
