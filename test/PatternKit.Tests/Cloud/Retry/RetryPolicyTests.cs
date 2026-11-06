using PatternKit.Cloud.Retry;
using TinyBDD;

namespace PatternKit.Tests.Cloud.Retry;

public sealed class RetryPolicyTests
{
    [Scenario("Retry policy succeeds after transient exceptions")]
    [Fact]
    public void RetryPolicy_Succeeds_After_Transient_Exceptions()
    {
        var attempts = 0;
        var policy = RetryPolicy<string>.Create("inventory")
            .WithMaxAttempts(3)
            .HandleException(static ex => ex is TimeoutException)
            .Build();

        var result = policy.Execute(() =>
        {
            attempts++;
            if (attempts < 3)
                throw new TimeoutException();
            return "available";
        });

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("available", result.Value);
        ScenarioExpect.Equal(3, result.Attempts);
    }

    [Scenario("Retry policy exhausts attempts when results remain transient")]
    [Fact]
    public void RetryPolicy_Exhausts_Attempts_When_Results_Remain_Transient()
    {
        var attempts = 0;
        var policy = RetryPolicy<int>.Create("status")
            .WithMaxAttempts(4)
            .HandleResult(static status => status == 503)
            .Build();

        var result = policy.Execute(() =>
        {
            attempts++;
            return 503;
        });

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.Equal(503, result.Value);
        ScenarioExpect.Equal(4, result.Attempts);
        ScenarioExpect.Equal(4, attempts);
    }

    [Scenario("Async retry policy applies delay provider and cancellation")]
    [Fact]
    public async Task AsyncRetryPolicy_Applies_DelayProvider_And_Cancellation()
    {
        var delays = new List<TimeSpan>();
        var attempts = 0;
        var policy = RetryPolicy<string>.Create("async")
            .WithMaxAttempts(3)
            .WithInitialDelay(TimeSpan.FromMilliseconds(5))
            .WithExponentialBackoff(2)
            .WithDelayProvider((delay, _) =>
            {
                delays.Add(delay);
                return default;
            })
            .HandleResult(static result => result == "retry")
            .Build();

        var result = await policy.ExecuteAsync(_ =>
        {
            attempts++;
            return new ValueTask<string>(attempts < 3 ? "retry" : "ok");
        });

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("ok", result.Value);
        ScenarioExpect.Equal(3, result.Attempts);
        ScenarioExpect.Equal([TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(10)], delays);
    }

    [Scenario("Async retry policy preserves cancellation")]
    [Fact]
    public async Task AsyncRetryPolicy_Preserves_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var policy = RetryPolicy<string>.Create("cancel").Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync(_ => new ValueTask<string>("never"), cts.Token).AsTask());
    }

    [Scenario("Retry policy returns the last handled exception when exhausted")]
    [Fact]
    public void RetryPolicy_Returns_Last_Handled_Exception_When_Exhausted()
    {
        var policy = RetryPolicy<string>.Create("timeouts")
            .WithMaxAttempts(2)
            .HandleException(static exception => exception is TimeoutException)
            .Build();

        var result = policy.Execute(() => throw new TimeoutException("inventory timeout"));

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.Equal(2, result.Attempts);
        ScenarioExpect.IsType<TimeoutException>(result.Exception);
        ScenarioExpect.Null(result.Value);
    }

    [Scenario("Retry policy rethrows unhandled exceptions")]
    [Fact]
    public void RetryPolicy_Rethrows_Unhandled_Exceptions()
    {
        var policy = RetryPolicy<string>.Create("fatal")
            .HandleException(static exception => exception is TimeoutException)
            .Build();

        ScenarioExpect.Throws<InvalidOperationException>(() => policy.Execute(() => throw new InvalidOperationException("fatal")));
    }

    [Scenario("Retry policy rejects null operations")]
    [Fact]
    public async Task RetryPolicy_Rejects_Null_Operations()
    {
        var policy = RetryPolicy<string>.Create("nulls").Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => policy.Execute(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => policy.ExecuteAsync(null!).AsTask());
    }

    [Scenario("Retry policy validates configuration")]
    [Fact]
    public void RetryPolicy_Validates_Configuration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => RetryPolicy<string>.Create("").Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => RetryPolicy<string>.Create().WithMaxAttempts(0).Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => RetryPolicy<string>.Create().WithInitialDelay(TimeSpan.FromMilliseconds(-1)).Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => RetryPolicy<string>.Create().WithExponentialBackoff(0.5));
        ScenarioExpect.Throws<ArgumentNullException>(() => RetryPolicy<string>.Create().HandleResult(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => RetryPolicy<string>.Create().HandleException(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => RetryPolicy<string>.Create().WithDelayProvider(null!));
    }
}
