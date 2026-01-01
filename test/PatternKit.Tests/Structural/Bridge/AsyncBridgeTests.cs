using PatternKit.Structural.Bridge;

namespace PatternKit.Tests.Structural.Bridge;

public sealed class AsyncBridgeTests
{
    #region AsyncBridge<TIn, TOut, TImpl> Tests

    [Fact]
    public async Task AsyncBridge_Executes_With_Provider()
    {
        var bridge = AsyncBridge<string, string, int>.Create(() => 42)
            .Operation((abs, impl) => $"{abs}-{impl}")
            .Build();

        var result = await bridge.ExecuteAsync("hello");

        Assert.Equal("hello-42", result);
    }

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

        Assert.Equal("test-100", result);
    }

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

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncBridge_TryExecute_Returns_Success()
    {
        var bridge = AsyncBridge<string, string, int>.Create(() => 42)
            .Operation((abs, impl) => $"{abs}-{impl}")
            .Build();

        var (success, result, error) = await bridge.TryExecuteAsync("hello");

        Assert.True(success);
        Assert.Equal("hello-42", result);
        Assert.Null(error);
    }

    [Fact]
    public async Task AsyncBridge_TryExecute_Catches_Exception()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 1)
            .Operation((_, _) => throw new InvalidOperationException("test error"))
            .Build();

        var (success, result, error) = await bridge.TryExecuteAsync(5);

        Assert.False(success);
        Assert.Equal(default, result);
        Assert.Equal("test error", error);
    }

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

        Assert.Equal("before-test-42", log[0]);
        Assert.Equal("operation", log[1]);
    }

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

        Assert.Equal("operation", log[0]);
        Assert.Equal("after-test-42", log[1]);
        Assert.Equal("test-42-modified", result);
    }

    [Fact]
    public async Task AsyncBridge_Require_Validation_Passes()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 2)
            .Operation((input, impl) => input * impl)
            .Require((input, impl) => input > 0 ? null : "Input must be positive")
            .Build();

        var result = await bridge.ExecuteAsync(5);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncBridge_Require_Validation_Fails()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 2)
            .Operation((input, impl) => input * impl)
            .Require((input, impl) => input > 0 ? null : "Input must be positive")
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => bridge.ExecuteAsync(-5).AsTask());

        Assert.Equal("Input must be positive", ex.Message);
    }

    [Fact]
    public async Task AsyncBridge_RequireResult_Validation_Passes()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 2)
            .Operation((input, impl) => input * impl)
            .RequireResult((input, impl, result) => result > 0 ? null : "Result must be positive")
            .Build();

        var result = await bridge.ExecuteAsync(5);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncBridge_RequireResult_Validation_Fails()
    {
        var bridge = AsyncBridge<int, int, int>.Create(() => 2)
            .Operation((input, impl) => input * impl)
            .RequireResult((input, impl, result) => result > 0 ? null : "Result must be positive")
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => bridge.ExecuteAsync(-5).AsTask());

        Assert.Equal("Result must be positive", ex.Message);
    }

    [Fact]
    public async Task AsyncBridge_ProviderFrom_Depends_On_Input()
    {
        var bridge = AsyncBridge<int, int, int>.Create(
                async (input, ct) => input * 10)
            .Operation((input, impl) => input + impl)
            .Build();

        var result = await bridge.ExecuteAsync(5);

        Assert.Equal(55, result); // 5 + (5*10)
    }

    [Fact]
    public void AsyncBridge_Build_Throws_Without_Operation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncBridge<int, int, int>.Create(() => 1).Build());
    }

    #endregion

    #region ActionBridge<TIn, TImpl> Tests

    [Fact]
    public void ActionBridge_Executes()
    {
        var log = new List<string>();
        var bridge = ActionBridge<string, int>.Create(() => 42)
            .Operation((in input, impl) => log.Add($"{input}-{impl}"))
            .Build();

        bridge.Execute("hello");

        Assert.Single(log);
        Assert.Equal("hello-42", log[0]);
    }

    [Fact]
    public void ActionBridge_Before_Hook_Executes()
    {
        var log = new List<string>();
        var bridge = ActionBridge<string, int>.Create(() => 42)
            .Operation((in input, impl) => log.Add("operation"))
            .Before((in input, impl) => log.Add($"before-{input}"))
            .Build();

        bridge.Execute("test");

        Assert.Equal("before-test", log[0]);
        Assert.Equal("operation", log[1]);
    }

    [Fact]
    public void ActionBridge_After_Hook_Executes()
    {
        var log = new List<string>();
        var bridge = ActionBridge<string, int>.Create(() => 42)
            .Operation((in input, impl) => log.Add("operation"))
            .After((in input, impl) => log.Add($"after-{input}"))
            .Build();

        bridge.Execute("test");

        Assert.Equal("operation", log[0]);
        Assert.Equal("after-test", log[1]);
    }

    [Fact]
    public void ActionBridge_TryExecute_Returns_Success()
    {
        var executed = false;
        var bridge = ActionBridge<string, int>.Create(() => 42)
            .Operation((in input, impl) => executed = true)
            .Build();

        var success = bridge.TryExecute("hello", out var error);

        Assert.True(success);
        Assert.True(executed);
        Assert.Null(error);
    }

    [Fact]
    public void ActionBridge_TryExecute_Catches_Exception()
    {
        var bridge = ActionBridge<string, int>.Create(() => 42)
            .Operation((in input, impl) => throw new InvalidOperationException("test error"))
            .Build();

        var success = bridge.TryExecute("hello", out var error);

        Assert.False(success);
        Assert.Equal("test error", error);
    }

    [Fact]
    public void ActionBridge_Require_Validation_Passes()
    {
        var executed = false;
        var bridge = ActionBridge<int, int>.Create(() => 1)
            .Operation((in input, impl) => executed = true)
            .Require((in input, impl) => input > 0 ? null : "Input must be positive")
            .Build();

        bridge.Execute(5);

        Assert.True(executed);
    }

    [Fact]
    public void ActionBridge_Require_Validation_Fails()
    {
        var bridge = ActionBridge<int, int>.Create(() => 1)
            .Operation((in input, impl) => { })
            .Require((in input, impl) => input > 0 ? null : "Input must be positive")
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => bridge.Execute(-5));

        Assert.Equal("Input must be positive", ex.Message);
    }

    [Fact]
    public void ActionBridge_Build_Throws_Without_Operation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ActionBridge<int, int>.Create(() => 1).Build());
    }

    [Fact]
    public void ActionBridge_ProviderFrom_Depends_On_Input()
    {
        var capturedImpl = 0;
        var bridge = ActionBridge<int, int>.Create((in input) => input * 10)
            .Operation((in input, impl) => capturedImpl = impl)
            .Build();

        bridge.Execute(5);

        Assert.Equal(50, capturedImpl);
    }

    #endregion

    #region Null Argument Tests

    [Fact]
    public void AsyncBridge_Provider_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncBridge<int, int, int>.Create((AsyncBridge<int, int, int>.Provider)null!));
    }

    [Fact]
    public void ActionBridge_Provider_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionBridge<int, int>.Create((ActionBridge<int, int>.Provider)null!));
    }

    #endregion
}
