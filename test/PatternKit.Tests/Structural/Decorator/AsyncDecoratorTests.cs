using PatternKit.Structural.Decorator;

namespace PatternKit.Tests.Structural.Decorator;

public sealed class AsyncDecoratorTests
{
    #region AsyncDecorator<TIn, TOut> Tests

    [Fact]
    public async Task AsyncDecorator_Core_Executes()
    {
        var decorator = AsyncDecorator<int, int>.Create(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            })
            .Build();

        var result = await decorator.ExecuteAsync(5);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncDecorator_Sync_Core_Executes()
    {
        var decorator = AsyncDecorator<int, int>.Create((x, _) => new ValueTask<int>(x * 2))
            .Build();

        var result = await decorator.ExecuteAsync(5);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncDecorator_Before_Transforms_Input()
    {
        var decorator = AsyncDecorator<int, int>.Create((x, _) => new ValueTask<int>(x * 2))
            .Before(x => x + 10) // Sync before: transform 5 -> 15
            .Build();

        var result = await decorator.ExecuteAsync(5);

        Assert.Equal(30, result); // (5+10) * 2
    }

    [Fact]
    public async Task AsyncDecorator_Before_Async_Transforms_Input()
    {
        var decorator = AsyncDecorator<int, int>.Create((x, _) => new ValueTask<int>(x * 2))
            .Before(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x + 10;
            })
            .Build();

        var result = await decorator.ExecuteAsync(5);

        Assert.Equal(30, result); // (5+10) * 2
    }

    [Fact]
    public async Task AsyncDecorator_After_Transforms_Output()
    {
        var decorator = AsyncDecorator<int, int>.Create((x, _) => new ValueTask<int>(x * 2))
            .After((input, output) => output + 1) // Sync after: transform output
            .Build();

        var result = await decorator.ExecuteAsync(5);

        Assert.Equal(11, result); // 5*2 + 1
    }

    [Fact]
    public async Task AsyncDecorator_After_Async_Transforms_Output()
    {
        var decorator = AsyncDecorator<int, int>.Create((x, _) => new ValueTask<int>(x * 2))
            .After(async (input, output, ct) =>
            {
                await Task.Delay(1, ct);
                return output + input;
            })
            .Build();

        var result = await decorator.ExecuteAsync(5);

        Assert.Equal(15, result); // 5*2 + 5
    }

    [Fact]
    public async Task AsyncDecorator_Around_Wraps_Execution()
    {
        var log = new List<string>();
        var decorator = AsyncDecorator<int, int>.Create((x, _) =>
            {
                log.Add("core");
                return new ValueTask<int>(x * 2);
            })
            .Around(async (input, ct, next) =>
            {
                log.Add("around-before");
                var result = await next(input, ct);
                log.Add("around-after");
                return result + 1;
            })
            .Build();

        var result = await decorator.ExecuteAsync(5);

        Assert.Equal("around-before", log[0]);
        Assert.Equal("core", log[1]);
        Assert.Equal("around-after", log[2]);
        Assert.Equal(11, result); // 5*2 + 1
    }

    [Fact]
    public async Task AsyncDecorator_Multiple_Layers_Execute_In_Order()
    {
        var log = new List<string>();
        var decorator = AsyncDecorator<int, int>.Create((x, _) =>
            {
                log.Add("core");
                return new ValueTask<int>(x);
            })
            .Before(x => { log.Add("before-1"); return x; })
            .Before(x => { log.Add("before-2"); return x; })
            .After((_, o) => { log.Add("after-1"); return o; })
            .After((_, o) => { log.Add("after-2"); return o; })
            .Build();

        await decorator.ExecuteAsync(5);

        // Before hooks execute in order (FIFO)
        Assert.Equal("before-1", log[0]);
        Assert.Equal("before-2", log[1]);
        Assert.Equal("core", log[2]);
        // After hooks execute in reverse order (LIFO - like onion layers unwinding)
        Assert.Equal("after-2", log[3]);
        Assert.Equal("after-1", log[4]);
    }

    [Fact]
    public async Task AsyncDecorator_No_Layers_Executes_Core()
    {
        var decorator = AsyncDecorator<int, int>.Create((x, _) => new ValueTask<int>(x * 2))
            .Build();

        var result = await decorator.ExecuteAsync(5);

        Assert.Equal(10, result);
    }

    #endregion

    #region AsyncActionDecorator<TIn> Tests

    [Fact]
    public async Task AsyncActionDecorator_Core_Executes()
    {
        var executed = false;
        var decorator = AsyncActionDecorator<int>.Create(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                executed = true;
            })
            .Build();

        await decorator.ExecuteAsync(5);

        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncActionDecorator_Before_Transforms_Input()
    {
        var capturedInput = 0;
        var decorator = AsyncActionDecorator<int>.Create((x, _) =>
            {
                capturedInput = x;
                return default;
            })
            .Before(x => x + 10) // Transform 5 -> 15
            .Build();

        await decorator.ExecuteAsync(5);

        Assert.Equal(15, capturedInput);
    }

    [Fact]
    public async Task AsyncActionDecorator_Before_Async_Transforms_Input()
    {
        var capturedInput = 0;
        var decorator = AsyncActionDecorator<int>.Create((x, _) =>
            {
                capturedInput = x;
                return default;
            })
            .Before(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x + 10;
            })
            .Build();

        await decorator.ExecuteAsync(5);

        Assert.Equal(15, capturedInput);
    }

    [Fact]
    public async Task AsyncActionDecorator_After_Executes_After_Core()
    {
        var log = new List<string>();
        var decorator = AsyncActionDecorator<int>.Create((x, _) =>
            {
                log.Add("core");
                return default;
            })
            .After(x => log.Add("after")) // Sync after
            .Build();

        await decorator.ExecuteAsync(5);

        Assert.Equal("core", log[0]);
        Assert.Equal("after", log[1]);
    }

    [Fact]
    public async Task AsyncActionDecorator_After_Async_Executes_After_Core()
    {
        var log = new List<string>();
        var decorator = AsyncActionDecorator<int>.Create((x, _) =>
            {
                log.Add("core");
                return default;
            })
            .After(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add("after");
            })
            .Build();

        await decorator.ExecuteAsync(5);

        Assert.Equal("core", log[0]);
        Assert.Equal("after", log[1]);
    }

    [Fact]
    public async Task AsyncActionDecorator_Around_Wraps_Execution()
    {
        var log = new List<string>();
        var decorator = AsyncActionDecorator<int>.Create((x, _) =>
            {
                log.Add("core");
                return default;
            })
            .Around(async (input, ct, next) =>
            {
                log.Add("around-before");
                await next(input, ct);
                log.Add("around-after");
            })
            .Build();

        await decorator.ExecuteAsync(5);

        Assert.Equal("around-before", log[0]);
        Assert.Equal("core", log[1]);
        Assert.Equal("around-after", log[2]);
    }

    [Fact]
    public async Task AsyncActionDecorator_Multiple_Layers()
    {
        var log = new List<string>();
        var decorator = AsyncActionDecorator<int>.Create((x, _) =>
            {
                log.Add("core");
                return default;
            })
            .Before(x => { log.Add("before"); return x; })
            .After(x => log.Add("after"))
            .Build();

        await decorator.ExecuteAsync(5);

        Assert.Equal("before", log[0]);
        Assert.Equal("core", log[1]);
        Assert.Equal("after", log[2]);
    }

    #endregion

    #region ActionDecorator<TIn> Tests

    [Fact]
    public void ActionDecorator_Core_Executes()
    {
        var executed = false;
        var decorator = ActionDecorator<int>.Create(x => executed = true).Build();

        decorator.Execute(5);

        Assert.True(executed);
    }

    [Fact]
    public void ActionDecorator_Before_Transforms_Input()
    {
        var capturedInput = 0;
        var decorator = ActionDecorator<int>.Create(x => capturedInput = x)
            .Before(x => x + 10)
            .Build();

        decorator.Execute(5);

        Assert.Equal(15, capturedInput);
    }

    [Fact]
    public void ActionDecorator_After_Executes_After_Core()
    {
        var log = new List<string>();
        var decorator = ActionDecorator<int>.Create(x => log.Add("core"))
            .After(x => log.Add("after"))
            .Build();

        decorator.Execute(5);

        Assert.Equal("core", log[0]);
        Assert.Equal("after", log[1]);
    }

    [Fact]
    public void ActionDecorator_Around_Wraps_Execution()
    {
        var log = new List<string>();
        var decorator = ActionDecorator<int>.Create(x => log.Add("core"))
            .Around((input, next) =>
            {
                log.Add("around-before");
                next(input);
                log.Add("around-after");
            })
            .Build();

        decorator.Execute(5);

        Assert.Equal("around-before", log[0]);
        Assert.Equal("core", log[1]);
        Assert.Equal("around-after", log[2]);
    }

    [Fact]
    public void ActionDecorator_Multiple_Layers()
    {
        var log = new List<string>();
        var decorator = ActionDecorator<int>.Create(x => log.Add("core"))
            .Before(x => { log.Add("before-1"); return x; })
            .Before(x => { log.Add("before-2"); return x; })
            .After(x => log.Add("after-1"))
            .After(x => log.Add("after-2"))
            .Build();

        decorator.Execute(5);

        // Before hooks execute in order (FIFO)
        Assert.Equal("before-1", log[0]);
        Assert.Equal("before-2", log[1]);
        Assert.Equal("core", log[2]);
        // After hooks execute in reverse order (LIFO - like onion layers unwinding)
        Assert.Equal("after-2", log[3]);
        Assert.Equal("after-1", log[4]);
    }

    [Fact]
    public void ActionDecorator_No_Layers_Executes_Core()
    {
        var executed = false;
        var decorator = ActionDecorator<int>.Create(x => executed = true).Build();

        decorator.Execute(5);

        Assert.True(executed);
    }

    [Fact]
    public void ActionDecorator_Around_Can_Skip_Core()
    {
        var coreExecuted = false;
        var decorator = ActionDecorator<int>.Create(x => coreExecuted = true)
            .Around((input, next) =>
            {
                // Don't call next - skip core
            })
            .Build();

        decorator.Execute(5);

        Assert.False(coreExecuted);
    }

    #endregion

    #region Null Argument Tests

    [Fact]
    public void AsyncDecorator_Component_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncDecorator<int, int>.Create(null!));
    }

    [Fact]
    public void AsyncDecorator_Before_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncDecorator<int, int>.Create((x, _) => new ValueTask<int>(x))
                .Before((AsyncDecorator<int, int>.BeforeTransform)null!));
    }

    [Fact]
    public void AsyncDecorator_After_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncDecorator<int, int>.Create((x, _) => new ValueTask<int>(x))
                .After((AsyncDecorator<int, int>.AfterTransform)null!));
    }

    [Fact]
    public void AsyncDecorator_Around_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncDecorator<int, int>.Create((x, _) => new ValueTask<int>(x))
                .Around(null!));
    }

    [Fact]
    public void AsyncActionDecorator_Component_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncActionDecorator<int>.Create(null!));
    }

    [Fact]
    public void AsyncActionDecorator_Before_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncActionDecorator<int>.Create((x, _) => default)
                .Before((AsyncActionDecorator<int>.BeforeTransform)null!));
    }

    [Fact]
    public void AsyncActionDecorator_After_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncActionDecorator<int>.Create((x, _) => default)
                .After((AsyncActionDecorator<int>.AfterAction)null!));
    }

    [Fact]
    public void AsyncActionDecorator_Around_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncActionDecorator<int>.Create((x, _) => default)
                .Around(null!));
    }

    [Fact]
    public void ActionDecorator_Component_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionDecorator<int>.Create(null!));
    }

    [Fact]
    public void ActionDecorator_Before_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionDecorator<int>.Create(x => { }).Before(null!));
    }

    [Fact]
    public void ActionDecorator_After_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionDecorator<int>.Create(x => { }).After(null!));
    }

    [Fact]
    public void ActionDecorator_Around_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionDecorator<int>.Create(x => { }).Around(null!));
    }

    #endregion
}
