using PatternKit.Structural.Bridge;
using TinyBDD;

namespace PatternKit.Tests.Structural.Bridge;

public sealed class AsyncBridgeTests
{
    #region AsyncBridge<TIn, TOut, TImpl> Tests

    [Scenario("AsyncBridge Executes With Provider")]
    [Fact]
    public async Task AsyncBridge_Executes_With_Provider()
    {
        var bridge = AsyncBridge<string, string, int>.Create(() => 42)
            .Operation((abs, impl) => $"{abs}-{impl}")
            .Build();

        var result = await bridge.ExecuteAsync("hello");

        ScenarioExpect.Equal("hello-42", result);
    }

    [Scenario("AsyncBridge Async Provider Works")]
    [Fact]
    public async Task AsyncBridge_Async_Provider_Works()
    {
        var bridge = AsyncBridge<string, string, int>.Create(async ct =>
            {
                await Task.Delay(1, ct);
                return 100;
            })
            .Operation((abs, impl) => $"{abs}-{impl}")
            .Build();

        var result = await bridge.ExecuteAsync("test");

        ScenarioExpect.Equal("test-100", result);
    }

    [Scenario("AsyncBridge Async Operation Works")]
    [Fact]
    public async Task AsyncBridge_Async_Operation_Works()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 2)
            .Operation(async (input, impl, ct) =>
            {
                await Task.Delay(1, ct);
                return input * impl;
            })
            .Build();

        var result = await bridge.ExecuteAsync(5);

        ScenarioExpect.Equal(10, result);
    }

    [Scenario("AsyncBridge TryExecute Returns Success")]
    [Fact]
    public async Task AsyncBridge_TryExecute_Returns_Success()
    {
        var bridge = AsyncBridge<string, string, int>.Create(() => 42)
            .Operation((abs, impl) => $"{abs}-{impl}")
            .Build();

        var (success, result, error) = await bridge.TryExecuteAsync("hello");

        ScenarioExpect.True(success);
        ScenarioExpect.Equal("hello-42", result);
        ScenarioExpect.Null(error);
    }

    [Scenario("AsyncBridge TryExecute Catches Exception")]
    [Fact]
    public async Task AsyncBridge_TryExecute_Catches_Exception()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 1)
            .Operation((_, _) => throw new InvalidOperationException("test error"))
            .Build();

        var (success, result, error) = await bridge.TryExecuteAsync(5);

        ScenarioExpect.False(success);
        ScenarioExpect.Equal(default, result);
        ScenarioExpect.Equal("test error", error);
    }

    [Scenario("AsyncBridge Before Hook Executes")]
    [Fact]
    public async Task AsyncBridge_Before_Hook_Executes()
    {
        var log = new List<string>();
        var bridge = AsyncBridge<string, string, int>.Create(() => 42)
            .Operation((input, impl) =>
            {
                log.Add("operation");
                return $"{input}-{impl}";
            })
            .Before((input, impl) => log.Add($"before-{input}-{impl}"))
            .Build();

        await bridge.ExecuteAsync("test");

        ScenarioExpect.Equal("before-test-42", log[0]);
        ScenarioExpect.Equal("operation", log[1]);
    }

    [Scenario("AsyncBridge After Hook Executes")]
    [Fact]
    public async Task AsyncBridge_After_Hook_Executes()
    {
        var log = new List<string>();
        var bridge = AsyncBridge<string, string, int>.Create(() => 42)
            .Operation((input, impl) =>
            {
                log.Add("operation");
                return $"{input}-{impl}";
            })
            .After((input, impl, result) =>
            {
                log.Add($"after-{result}");
                return result + "-modified";
            })
            .Build();

        var result = await bridge.ExecuteAsync("test");

        ScenarioExpect.Equal("operation", log[0]);
        ScenarioExpect.Equal("after-test-42", log[1]);
        ScenarioExpect.Equal("test-42-modified", result);
    }

    [Scenario("AsyncBridge Require Validation Passes")]
    [Fact]
    public async Task AsyncBridge_Require_Validation_Passes()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 2)
            .Operation((input, impl) => input * impl)
            .Require((input, impl) => input > 0 ? null : "Input must be positive")
            .Build();

        var result = await bridge.ExecuteAsync(5);

        ScenarioExpect.Equal(10, result);
    }

    [Scenario("AsyncBridge Require Validation Fails")]
    [Fact]
    public async Task AsyncBridge_Require_Validation_Fails()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 2)
            .Operation((input, impl) => input * impl)
            .Require((input, impl) => input > 0 ? null : "Input must be positive")
            .Build();

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(
            () => bridge.ExecuteAsync(-5).AsTask());

        ScenarioExpect.Equal("Input must be positive", ex.Message);
    }

    [Scenario("AsyncBridge RequireResult Validation Passes")]
    [Fact]
    public async Task AsyncBridge_RequireResult_Validation_Passes()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 2)
            .Operation((input, impl) => input * impl)
            .RequireResult((input, impl, result) => result > 0 ? null : "Result must be positive")
            .Build();

        var result = await bridge.ExecuteAsync(5);

        ScenarioExpect.Equal(10, result);
    }

    [Scenario("AsyncBridge RequireResult Validation Fails")]
    [Fact]
    public async Task AsyncBridge_RequireResult_Validation_Fails()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 2)
            .Operation((input, impl) => input * impl)
            .RequireResult((input, impl, result) => result > 0 ? null : "Result must be positive")
            .Build();

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(
            () => bridge.ExecuteAsync(-5).AsTask());

        ScenarioExpect.Equal("Result must be positive", ex.Message);
    }

    [Scenario("AsyncBridge ProviderFrom Depends On Input")]
    [Fact]
    public async Task AsyncBridge_ProviderFrom_Depends_On_Input()
    {
        var bridge = AsyncBridge<int, int, int>.Create(
                async (input, ct) => input * 10)
            .Operation((input, impl) => input + impl)
            .Build();

        var result = await bridge.ExecuteAsync(5);

        ScenarioExpect.Equal(55, result); // 5 + (5*10)
    }

    [Scenario("AsyncBridge Build Throws Without Operation")]
    [Fact]
    public void AsyncBridge_Build_Throws_Without_Operation()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncBridge<int, int, int>.Create(() => 1).Build());
    }

    #endregion

    #region ActionBridge<TIn, TImpl> Tests

    [Scenario("ActionBridge Executes")]
    [Fact]
    public void ActionBridge_Executes()
    {
        var log = new List<string>();
        var bridge = ActionBridge<string, int>.Create(() => 42)
            .Operation((in input, impl) => log.Add($"{input}-{impl}"))
            .Build();

        bridge.Execute("hello");

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("hello-42", log[0]);
    }

    [Scenario("ActionBridge Before Hook Executes")]
    [Fact]
    public void ActionBridge_Before_Hook_Executes()
    {
        var log = new List<string>();
        var bridge = ActionBridge<string, int>.Create(() => 42)
            .Operation((in input, impl) => log.Add("operation"))
            .Before((in input, impl) => log.Add($"before-{input}"))
            .Build();

        bridge.Execute("test");

        ScenarioExpect.Equal("before-test", log[0]);
        ScenarioExpect.Equal("operation", log[1]);
    }

    [Scenario("ActionBridge After Hook Executes")]
    [Fact]
    public void ActionBridge_After_Hook_Executes()
    {
        var log = new List<string>();
        var bridge = ActionBridge<string, int>.Create(() => 42)
            .Operation((in input, impl) => log.Add("operation"))
            .After((in input, impl) => log.Add($"after-{input}"))
            .Build();

        bridge.Execute("test");

        ScenarioExpect.Equal("operation", log[0]);
        ScenarioExpect.Equal("after-test", log[1]);
    }

    [Scenario("ActionBridge TryExecute Returns Success")]
    [Fact]
    public void ActionBridge_TryExecute_Returns_Success()
    {
        var executed = false;
        var bridge = ActionBridge<string, int>.Create(() => 42)
            .Operation((in input, impl) => executed = true)
            .Build();

        var success = bridge.TryExecute("hello", out var error);

        ScenarioExpect.True(success);
        ScenarioExpect.True(executed);
        ScenarioExpect.Null(error);
    }

    [Scenario("ActionBridge TryExecute Catches Exception")]
    [Fact]
    public void ActionBridge_TryExecute_Catches_Exception()
    {
        var bridge = ActionBridge<string, int>.Create(() => 42)
            .Operation((in input, impl) => throw new InvalidOperationException("test error"))
            .Build();

        var success = bridge.TryExecute("hello", out var error);

        ScenarioExpect.False(success);
        ScenarioExpect.Equal("test error", error);
    }

    [Scenario("ActionBridge Require Validation Passes")]
    [Fact]
    public void ActionBridge_Require_Validation_Passes()
    {
        var executed = false;
        var bridge = ActionBridge<int, int>.Create(() => 1)
            .Operation((in input, impl) => executed = true)
            .Require((in input, impl) => input > 0 ? null : "Input must be positive")
            .Build();

        bridge.Execute(5);

        ScenarioExpect.True(executed);
    }

    [Scenario("ActionBridge Require Validation Fails")]
    [Fact]
    public void ActionBridge_Require_Validation_Fails()
    {
        var bridge = ActionBridge<int, int>.Create(() => 1)
            .Operation((in input, impl) => { })
            .Require((in input, impl) => input > 0 ? null : "Input must be positive")
            .Build();

        var ex = ScenarioExpect.Throws<InvalidOperationException>(() => bridge.Execute(-5));

        ScenarioExpect.Equal("Input must be positive", ex.Message);
    }

    [Scenario("ActionBridge Build Throws Without Operation")]
    [Fact]
    public void ActionBridge_Build_Throws_Without_Operation()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            ActionBridge<int, int>.Create(() => 1).Build());
    }

    [Scenario("ActionBridge ProviderFrom Depends On Input")]
    [Fact]
    public void ActionBridge_ProviderFrom_Depends_On_Input()
    {
        var capturedImpl = 0;
        var bridge = ActionBridge<int, int>.Create((in input) => input * 10)
            .Operation((in input, impl) => capturedImpl = impl)
            .Build();

        bridge.Execute(5);

        ScenarioExpect.Equal(50, capturedImpl);
    }

    #endregion

    #region AsyncActionBridge<TIn, TImpl> Tests

    [Scenario("AsyncActionBridge Executes")]
    [Fact]
    public async Task AsyncActionBridge_Executes()
    {
        var log = new List<string>();
        var bridge = AsyncActionBridge<string, int>.Create(() => 42)
            .Operation(async (input, impl, ct) => log.Add($"{input}-{impl}"))
            .Build();

        await bridge.ExecuteAsync("hello");

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("hello-42", log[0]);
    }

    [Scenario("AsyncActionBridge Async Provider Works")]
    [Fact]
    public async Task AsyncActionBridge_Async_Provider_Works()
    {
        var log = new List<string>();
        var bridge = AsyncActionBridge<string, int>.Create(async ct =>
            {
                await Task.Delay(1, ct);
                return 100;
            })
            .Operation(async (input, impl, ct) => log.Add($"{input}-{impl}"))
            .Build();

        await bridge.ExecuteAsync("test");

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("test-100", log[0]);
    }

    [Scenario("AsyncActionBridge Sync Operation")]
    [Fact]
    public async Task AsyncActionBridge_Sync_Operation()
    {
        var log = new List<string>();
        var bridge = AsyncActionBridge<string, int>.Create(() => 42)
            .Operation((input, impl) => log.Add($"sync-{input}-{impl}"))
            .Build();

        await bridge.ExecuteAsync("test");

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("sync-test-42", log[0]);
    }

    [Scenario("AsyncActionBridge Before Hook Executes")]
    [Fact]
    public async Task AsyncActionBridge_Before_Hook_Executes()
    {
        var log = new List<string>();
        var bridge = AsyncActionBridge<string, int>.Create(() => 42)
            .Operation(async (input, impl, ct) => log.Add("operation"))
            .Before(async (input, impl, ct) => log.Add($"before-{input}"))
            .Build();

        await bridge.ExecuteAsync("test");

        ScenarioExpect.Equal("before-test", log[0]);
        ScenarioExpect.Equal("operation", log[1]);
    }

    [Scenario("AsyncActionBridge Before Sync Hook")]
    [Fact]
    public async Task AsyncActionBridge_Before_Sync_Hook()
    {
        var log = new List<string>();
        var bridge = AsyncActionBridge<string, int>.Create(() => 42)
            .Operation(async (input, impl, ct) => log.Add("operation"))
            .Before((input, impl) => log.Add("sync-before"))
            .Build();

        await bridge.ExecuteAsync("test");

        ScenarioExpect.Equal("sync-before", log[0]);
        ScenarioExpect.Equal("operation", log[1]);
    }

    [Scenario("AsyncActionBridge After Hook Executes")]
    [Fact]
    public async Task AsyncActionBridge_After_Hook_Executes()
    {
        var log = new List<string>();
        var bridge = AsyncActionBridge<string, int>.Create(() => 42)
            .Operation(async (input, impl, ct) => log.Add("operation"))
            .After(async (input, impl, ct) => log.Add($"after-{input}"))
            .Build();

        await bridge.ExecuteAsync("test");

        ScenarioExpect.Equal("operation", log[0]);
        ScenarioExpect.Equal("after-test", log[1]);
    }

    [Scenario("AsyncActionBridge After Sync Hook")]
    [Fact]
    public async Task AsyncActionBridge_After_Sync_Hook()
    {
        var log = new List<string>();
        var bridge = AsyncActionBridge<string, int>.Create(() => 42)
            .Operation(async (input, impl, ct) => log.Add("operation"))
            .After((input, impl) => log.Add("sync-after"))
            .Build();

        await bridge.ExecuteAsync("test");

        ScenarioExpect.Equal("operation", log[0]);
        ScenarioExpect.Equal("sync-after", log[1]);
    }

    [Scenario("AsyncActionBridge TryExecute Returns Success")]
    [Fact]
    public async Task AsyncActionBridge_TryExecute_Returns_Success()
    {
        var executed = false;
        var bridge = AsyncActionBridge<string, int>.Create(() => 42)
            .Operation(async (input, impl, ct) => executed = true)
            .Build();

        var (success, error) = await bridge.TryExecuteAsync("hello");

        ScenarioExpect.True(success);
        ScenarioExpect.True(executed);
        ScenarioExpect.Null(error);
    }

    [Scenario("AsyncActionBridge TryExecute Catches Exception")]
    [Fact]
    public async Task AsyncActionBridge_TryExecute_Catches_Exception()
    {
        var bridge = AsyncActionBridge<string, int>.Create(() => 42)
            .Operation(async (input, impl, ct) => throw new InvalidOperationException("test error"))
            .Build();

        var (success, error) = await bridge.TryExecuteAsync("hello");

        ScenarioExpect.False(success);
        ScenarioExpect.Equal("test error", error);
    }

    [Scenario("AsyncActionBridge Require Validation Passes")]
    [Fact]
    public async Task AsyncActionBridge_Require_Validation_Passes()
    {
        var executed = false;
        var bridge = AsyncActionBridge<int, int>.Create(() => 1)
            .Operation(async (input, impl, ct) => executed = true)
            .Require(async (input, impl, ct) => input > 0 ? null : "Input must be positive")
            .Build();

        await bridge.ExecuteAsync(5);

        ScenarioExpect.True(executed);
    }

    [Scenario("AsyncActionBridge Require Validation Fails")]
    [Fact]
    public async Task AsyncActionBridge_Require_Validation_Fails()
    {
        var bridge = AsyncActionBridge<int, int>.Create(() => 1)
            .Operation(async (input, impl, ct) => { })
            .Require(async (input, impl, ct) => input > 0 ? null : "Input must be positive")
            .Build();

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(
            () => bridge.ExecuteAsync(-5).AsTask());

        ScenarioExpect.Equal("Input must be positive", ex.Message);
    }

    [Scenario("AsyncActionBridge Require Sync Validation")]
    [Fact]
    public async Task AsyncActionBridge_Require_Sync_Validation()
    {
        var bridge = AsyncActionBridge<int, int>.Create(() => 1)
            .Operation(async (input, impl, ct) => { })
            .Require((input, impl) => input > 0 ? null : "Sync validation failed")
            .Build();

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(
            () => bridge.ExecuteAsync(-5).AsTask());

        ScenarioExpect.Equal("Sync validation failed", ex.Message);
    }

    [Scenario("AsyncActionBridge TryExecute Returns Validation Error")]
    [Fact]
    public async Task AsyncActionBridge_TryExecute_Returns_Validation_Error()
    {
        var bridge = AsyncActionBridge<int, int>.Create(() => 1)
            .Operation(async (input, impl, ct) => { })
            .Require((input, impl) => input > 0 ? null : "Input must be positive")
            .Build();

        var (success, error) = await bridge.TryExecuteAsync(-5);

        ScenarioExpect.False(success);
        ScenarioExpect.Equal("Input must be positive", error);
    }

    [Scenario("AsyncActionBridge ProviderFrom Depends On Input")]
    [Fact]
    public async Task AsyncActionBridge_ProviderFrom_Depends_On_Input()
    {
        var capturedImpl = 0;
        var bridge = AsyncActionBridge<int, int>.Create(
                async (input, ct) => input * 10)
            .Operation(async (input, impl, ct) => capturedImpl = impl)
            .Build();

        await bridge.ExecuteAsync(5);

        ScenarioExpect.Equal(50, capturedImpl);
    }

    [Scenario("AsyncActionBridge Build Throws Without Operation")]
    [Fact]
    public void AsyncActionBridge_Build_Throws_Without_Operation()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncActionBridge<int, int>.Create(() => 1).Build());
    }

    #endregion

    #region Null Argument Tests

    [Scenario("AsyncBridge Provider Null Throws")]
    [Fact]
    public void AsyncBridge_Provider_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncBridge<int, int, int>.Create((AsyncBridge<int, int, int>.Provider)null!));
    }

    [Scenario("ActionBridge Provider Null Throws")]
    [Fact]
    public void ActionBridge_Provider_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            ActionBridge<int, int>.Create((ActionBridge<int, int>.Provider)null!));
    }

    #endregion
}
