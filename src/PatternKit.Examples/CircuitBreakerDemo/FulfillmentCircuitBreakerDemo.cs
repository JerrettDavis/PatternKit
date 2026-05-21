using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.CircuitBreaker;
using PatternKit.Generators.CircuitBreaker;

namespace PatternKit.Examples.CircuitBreakerDemo;

public sealed record FulfillmentResponse(string OrderId, int StatusCode, string Message)
{
    public bool Accepted => StatusCode == 202;
}

public interface IFulfillmentGateway
{
    ValueTask<FulfillmentResponse> SubmitAsync(string orderId, CancellationToken cancellationToken = default);
}

public sealed class ScriptedFulfillmentGateway(params FulfillmentResponse[] responses) : IFulfillmentGateway
{
    private readonly Queue<FulfillmentResponse> _responses = new(responses);

    public int Calls { get; private set; }

    public ValueTask<FulfillmentResponse> SubmitAsync(string orderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;

        if (_responses.Count == 0)
            return new(new FulfillmentResponse(orderId, 202, "accepted"));

        return new(_responses.Dequeue());
    }
}

public sealed class FulfillmentCircuitBreakerService(
    IFulfillmentGateway gateway,
    CircuitBreakerPolicy<FulfillmentResponse> policy)
{
    public async ValueTask<FulfillmentSubmissionResult> SubmitAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        var result = await policy.ExecuteAsync(
            ct => gateway.SubmitAsync(orderId, ct),
            cancellationToken);

        return new FulfillmentSubmissionResult(
            orderId,
            result.Value?.Accepted ?? false,
            result.Value?.StatusCode ?? 0,
            result.State,
            result.FailureCount,
            result.Rejected,
            result.Exception?.GetType().Name);
    }
}

public sealed record FulfillmentSubmissionResult(
    string OrderId,
    bool Accepted,
    int StatusCode,
    CircuitBreakerState State,
    int FailureCount,
    bool Rejected,
    string? ExceptionType);

public static partial class FulfillmentCircuitBreakerPolicies
{
    public static CircuitBreakerPolicy<FulfillmentResponse> CreateFluentPolicy()
        => CircuitBreakerPolicy<FulfillmentResponse>
            .Create("fulfillment-gateway")
            .WithFailureThreshold(2)
            .WithBreakDuration(TimeSpan.FromSeconds(30))
            .HandleResult(static response => response.StatusCode == 408 || response.StatusCode == 429 || response.StatusCode >= 500)
            .HandleException(static exception => exception is TimeoutException)
            .Build();
}

[GenerateCircuitBreakerPolicy(
    typeof(FulfillmentResponse),
    FactoryMethodName = "CreateGeneratedPolicy",
    PolicyName = "fulfillment-gateway",
    FailureThreshold = 2,
    BreakDurationMilliseconds = 30000)]
public static partial class GeneratedFulfillmentCircuitBreakerPolicy
{
    [CircuitBreakerResultPredicate]
    private static bool ShouldOpen(FulfillmentResponse response)
        => response.StatusCode == 408 || response.StatusCode == 429 || response.StatusCode >= 500;

    [CircuitBreakerExceptionPredicate]
    private static bool ShouldOpen(Exception exception)
        => exception is TimeoutException;
}

public static class FulfillmentCircuitBreakerDemoServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentCircuitBreakerDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedFulfillmentCircuitBreakerPolicy.CreateGeneratedPolicy());
        services.AddSingleton<ScriptedFulfillmentGateway>(static _ => new(
            new FulfillmentResponse("ORDER-42", 503, "fulfillment gateway unavailable"),
            new FulfillmentResponse("ORDER-42", 503, "fulfillment gateway unavailable")));
        services.AddSingleton<IFulfillmentGateway>(static sp => sp.GetRequiredService<ScriptedFulfillmentGateway>());
        services.AddSingleton<FulfillmentCircuitBreakerService>();
        return services;
    }
}
