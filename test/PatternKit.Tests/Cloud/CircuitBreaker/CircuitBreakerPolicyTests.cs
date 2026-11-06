using PatternKit.Cloud.CircuitBreaker;
using TinyBDD;

namespace PatternKit.Tests.Cloud.CircuitBreaker;

public sealed class CircuitBreakerPolicyTests
{
    [Scenario("Circuit breaker opens after handled result failures and rejects calls")]
    [Fact]
    public void CircuitBreaker_Opens_After_Handled_Result_Failures_And_Rejects_Calls()
    {
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        var calls = 0;
        var policy = CircuitBreakerPolicy<int>.Create("gateway")
            .WithFailureThreshold(2)
            .WithBreakDuration(TimeSpan.FromMinutes(1))
            .WithClock(() => now)
            .HandleResult(static status => status >= 500)
            .Build();

        var first = policy.Execute(() => { calls++; return 503; });
        var second = policy.Execute(() => { calls++; return 503; });
        var third = policy.Execute(() => { calls++; return 200; });

        ScenarioExpect.False(first.Succeeded);
        ScenarioExpect.Equal(CircuitBreakerState.Closed, first.State);
        ScenarioExpect.False(second.Succeeded);
        ScenarioExpect.Equal(CircuitBreakerState.Open, second.State);
        ScenarioExpect.True(third.Rejected);
        ScenarioExpect.Equal(CircuitBreakerState.Open, third.State);
        ScenarioExpect.Equal(2, calls);
    }

    [Scenario("Circuit breaker closes after a successful half-open probe")]
    [Fact]
    public void CircuitBreaker_Closes_After_Successful_HalfOpen_Probe()
    {
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        var policy = CircuitBreakerPolicy<string>.Create("fulfillment")
            .WithFailureThreshold(1)
            .WithBreakDuration(TimeSpan.FromMilliseconds(500))
            .WithClock(() => now)
            .HandleResult(static result => result == "down")
            .Build();

        var opened = policy.Execute(static () => "down");
        now = now.AddMilliseconds(500);
        var recovered = policy.Execute(static () => "ok");
        var snapshot = policy.Snapshot;

        ScenarioExpect.Equal(CircuitBreakerState.Open, opened.State);
        ScenarioExpect.True(recovered.Succeeded);
        ScenarioExpect.Equal("ok", recovered.Value);
        ScenarioExpect.Equal(CircuitBreakerState.Closed, recovered.State);
        ScenarioExpect.Equal(CircuitBreakerState.Closed, snapshot.State);
        ScenarioExpect.Equal(0, snapshot.FailureCount);
    }

    [Scenario("Circuit breaker reopens after a failed half-open probe")]
    [Fact]
    public void CircuitBreaker_Reopens_After_Failed_HalfOpen_Probe()
    {
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        var policy = CircuitBreakerPolicy<string>.Create("fulfillment")
            .WithFailureThreshold(1)
            .WithBreakDuration(TimeSpan.FromMilliseconds(500))
            .WithClock(() => now)
            .HandleResult(static result => result == "down")
            .Build();

        _ = policy.Execute(static () => "down");
        now = now.AddMilliseconds(500);
        var failedProbe = policy.Execute(static () => "down");
        var rejected = policy.Execute(static () => "ok");

        ScenarioExpect.False(failedProbe.Succeeded);
        ScenarioExpect.Equal(CircuitBreakerState.Open, failedProbe.State);
        ScenarioExpect.True(rejected.Rejected);
        ScenarioExpect.Equal(CircuitBreakerState.Open, rejected.State);
    }

    [Scenario("Circuit breaker records handled exceptions and rethrows unhandled exceptions")]
    [Fact]
    public void CircuitBreaker_Records_Handled_Exceptions_And_Rethrows_Unhandled_Exceptions()
    {
        var handledPolicy = CircuitBreakerPolicy<string>.Create("timeouts")
            .WithFailureThreshold(1)
            .HandleException(static exception => exception is TimeoutException)
            .Build();
        var fatalPolicy = CircuitBreakerPolicy<string>.Create("fatal")
            .HandleException(static exception => exception is TimeoutException)
            .Build();

        var result = handledPolicy.Execute(() => throw new TimeoutException("dependency timeout"));

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.Equal(CircuitBreakerState.Open, result.State);
        ScenarioExpect.IsType<TimeoutException>(result.Exception);
        ScenarioExpect.Throws<InvalidOperationException>(() => fatalPolicy.Execute(() => throw new InvalidOperationException("fatal")));
    }

    [Scenario("Async circuit breaker preserves cancellation and returns handled failures")]
    [Fact]
    public async Task AsyncCircuitBreaker_Preserves_Cancellation_And_Returns_Handled_Failures()
    {
        var policy = CircuitBreakerPolicy<int>.Create("async")
            .WithFailureThreshold(1)
            .HandleResult(static status => status == 503)
            .Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await policy.ExecuteAsync(static _ => new ValueTask<int>(503));

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.Equal(503, result.Value);
        ScenarioExpect.Equal(CircuitBreakerState.Open, result.State);
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync(static _ => new ValueTask<int>(200), cts.Token).AsTask());
    }

    [Scenario("Circuit breaker rejects invalid configuration")]
    [Fact]
    public async Task CircuitBreaker_Rejects_Invalid_Configuration()
    {
        var policy = CircuitBreakerPolicy<string>.Create("nulls").Build();

        ScenarioExpect.Throws<ArgumentException>(() => CircuitBreakerPolicy<string>.Create("").Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => CircuitBreakerPolicy<string>.Create().WithFailureThreshold(0).Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => CircuitBreakerPolicy<string>.Create().WithBreakDuration(TimeSpan.FromMilliseconds(-1)).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => CircuitBreakerPolicy<string>.Create().HandleResult(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => CircuitBreakerPolicy<string>.Create().HandleException(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => CircuitBreakerPolicy<string>.Create().WithClock(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => policy.Execute(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => policy.ExecuteAsync(null!).AsTask());
    }
}
