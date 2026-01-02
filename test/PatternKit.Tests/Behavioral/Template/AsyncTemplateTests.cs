using PatternKit.Behavioral.Template;

namespace PatternKit.Tests.Behavioral.Template;

public sealed class AsyncTemplateTests
{
    #region AsyncTemplate<TIn, TOut> Tests

    [Fact]
    public async Task AsyncTemplate_Executes_Steps_In_Order()
    {
        var log = new List<string>();
        var template = AsyncTemplate<int, string>.Create(async (x, ct) =>
            {
                log.Add("step");
                return x.ToString();
            })
            .Before((ctx, ct) =>
            {
                log.Add($"before-{ctx}");
                return default;
            })
            .After((ctx, result, ct) =>
            {
                log.Add($"after-{ctx}->{result}");
                return default;
            })
            .Build();

        var result = await template.ExecuteAsync(5);

        Assert.Equal("5", result);
        Assert.Equal("before-5", log[0]);
        Assert.Equal("step", log[1]);
        Assert.Equal("after-5->5", log[2]);
    }

    [Fact]
    public async Task AsyncTemplate_Multiple_Before_Steps()
    {
        var log = new List<string>();
        var template = AsyncTemplate<int, int>.Create((x, ct) => new ValueTask<int>(x))
            .Before(x => log.Add("before-1"))
            .Before(x => log.Add("before-2"))
            .Build();

        await template.ExecuteAsync(5);

        Assert.Equal("before-1", log[0]);
        Assert.Equal("before-2", log[1]);
    }

    [Fact]
    public async Task AsyncTemplate_Multiple_After_Steps()
    {
        var log = new List<string>();
        var template = AsyncTemplate<int, int>.Create((x, ct) => new ValueTask<int>(x))
            .After((ctx, res) => log.Add("after-1"))
            .After((ctx, res) => log.Add("after-2"))
            .Build();

        await template.ExecuteAsync(5);

        Assert.Equal("after-1", log[0]);
        Assert.Equal("after-2", log[1]);
    }

    [Fact]
    public async Task AsyncTemplate_TryExecute_Returns_Success()
    {
        var template = AsyncTemplate<int, int>.Create((x, ct) => new ValueTask<int>(x * 2))
            .Build();

        var (success, result, error) = await template.TryExecuteAsync(5);

        Assert.True(success);
        Assert.Equal(10, result);
        Assert.Null(error);
    }

    [Fact]
    public async Task AsyncTemplate_TryExecute_Catches_Exception()
    {
        var template = AsyncTemplate<int, int>.Create((x, ct) =>
            {
                throw new InvalidOperationException("test");
            })
            .Build();

        var (success, result, error) = await template.TryExecuteAsync(5);

        Assert.False(success);
        Assert.Equal(default, result);
        Assert.Equal("test", error);
    }

    [Fact]
    public async Task AsyncTemplate_Async_Before()
    {
        var log = new List<string>();
        var template = AsyncTemplate<int, int>.Create((x, ct) => new ValueTask<int>(x))
            .Before(async (ctx, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add("async-before");
            })
            .Build();

        await template.ExecuteAsync(5);

        Assert.Single(log);
        Assert.Equal("async-before", log[0]);
    }

    [Fact]
    public async Task AsyncTemplate_Async_After()
    {
        var log = new List<string>();
        var template = AsyncTemplate<int, int>.Create((x, ct) => new ValueTask<int>(x))
            .After(async (ctx, res, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add("async-after");
            })
            .Build();

        await template.ExecuteAsync(5);

        Assert.Single(log);
        Assert.Equal("async-after", log[0]);
    }

    [Fact]
    public async Task AsyncTemplate_OnError_Handler()
    {
        var log = new List<string>();
        var template = AsyncTemplate<int, int>.Create((x, ct) =>
            {
                throw new InvalidOperationException("error");
            })
            .OnError((ctx, err) => log.Add($"error: {err}"))
            .Build();

        var (success, _, _) = await template.TryExecuteAsync(5);

        Assert.False(success);
        Assert.Single(log);
        Assert.Contains("error", log[0]);
    }

    [Fact]
    public async Task AsyncTemplate_Synchronized()
    {
        var count = 0;
        var template = AsyncTemplate<int, int>.Create(async (x, ct) =>
            {
                var local = count;
                await Task.Delay(10, ct);
                count = local + 1;
                return count;
            })
            .Synchronized()
            .Build();

        var tasks = Enumerable.Range(0, 5).Select(_ => template.ExecuteAsync(0)).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(5, count);
    }

    #endregion

    #region AsyncActionTemplate<TIn> Tests

    [Fact]
    public async Task AsyncActionTemplate_Executes()
    {
        var executed = false;
        var template = AsyncActionTemplate<int>.Create(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                executed = true;
            })
            .Build();

        await template.ExecuteAsync(5);

        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncActionTemplate_Steps_In_Order()
    {
        var log = new List<string>();
        var template = AsyncActionTemplate<int>.Create((x, ct) =>
            {
                log.Add("core");
                return default;
            })
            .Before(x => log.Add("before"))
            .After(x => log.Add("after"))
            .Build();

        await template.ExecuteAsync(5);

        Assert.Equal("before", log[0]);
        Assert.Equal("core", log[1]);
        Assert.Equal("after", log[2]);
    }

    [Fact]
    public async Task AsyncActionTemplate_TryExecute_Returns_Success()
    {
        var executed = false;
        var template = AsyncActionTemplate<int>.Create((x, ct) =>
            {
                executed = true;
                return default;
            })
            .Build();

        var (success, error) = await template.TryExecuteAsync(5);

        Assert.True(success);
        Assert.True(executed);
        Assert.Null(error);
    }

    [Fact]
    public async Task AsyncActionTemplate_TryExecute_Catches_Exception()
    {
        var template = AsyncActionTemplate<int>.Create((x, ct) =>
            {
                throw new InvalidOperationException("test");
            })
            .Build();

        var (success, error) = await template.TryExecuteAsync(5);

        Assert.False(success);
        Assert.Equal("test", error);
    }

    [Fact]
    public async Task AsyncActionTemplate_Multiple_Before()
    {
        var log = new List<string>();
        var template = AsyncActionTemplate<int>.Create((x, ct) => default)
            .Before(x => log.Add("before-1"))
            .Before(x => log.Add("before-2"))
            .Build();

        await template.ExecuteAsync(5);

        Assert.Equal("before-1", log[0]);
        Assert.Equal("before-2", log[1]);
    }

    [Fact]
    public async Task AsyncActionTemplate_Multiple_After()
    {
        var log = new List<string>();
        var template = AsyncActionTemplate<int>.Create((x, ct) => default)
            .After(x => log.Add("after-1"))
            .After(x => log.Add("after-2"))
            .Build();

        await template.ExecuteAsync(5);

        Assert.Equal("after-1", log[0]);
        Assert.Equal("after-2", log[1]);
    }

    [Fact]
    public async Task AsyncActionTemplate_Synchronized()
    {
        var count = 0;
        var template = AsyncActionTemplate<int>.Create(async (x, ct) =>
            {
                var local = count;
                await Task.Delay(10, ct);
                count = local + 1;
            })
            .Synchronized()
            .Build();

        var tasks = Enumerable.Range(0, 5).Select(_ => template.ExecuteAsync(0)).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task AsyncActionTemplate_Async_Before()
    {
        var log = new List<string>();
        var template = AsyncActionTemplate<int>.Create((x, ct) => default)
            .Before(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add("async-before");
            })
            .Build();

        await template.ExecuteAsync(5);

        Assert.Single(log);
        Assert.Equal("async-before", log[0]);
    }

    [Fact]
    public async Task AsyncActionTemplate_Async_After()
    {
        var log = new List<string>();
        var template = AsyncActionTemplate<int>.Create((x, ct) => default)
            .After(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add("async-after");
            })
            .Build();

        await template.ExecuteAsync(5);

        Assert.Single(log);
        Assert.Equal("async-after", log[0]);
    }

    [Fact]
    public async Task AsyncActionTemplate_OnError_Sync_Handler()
    {
        var log = new List<string>();
        var template = AsyncActionTemplate<int>.Create((x, ct) =>
            {
                throw new InvalidOperationException("error");
            })
            .OnError((ctx, err) => log.Add($"error: {err}"))
            .Build();

        var (success, error) = await template.TryExecuteAsync(5);

        Assert.False(success);
        Assert.Single(log);
        Assert.Contains("error", log[0]);
    }

    [Fact]
    public async Task AsyncActionTemplate_OnError_Async_Handler()
    {
        var log = new List<string>();
        var template = AsyncActionTemplate<int>.Create((x, ct) =>
            {
                throw new InvalidOperationException("error");
            })
            .OnError(async (ctx, err, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add($"async-error: {err}");
            })
            .Build();

        var (success, error) = await template.TryExecuteAsync(5);

        Assert.False(success);
        Assert.Single(log);
        Assert.Contains("async-error", log[0]);
    }

    [Fact]
    public async Task AsyncActionTemplate_OnError_Handler_Throws_IsSwallowed()
    {
        var log = new List<string>();
        var template = AsyncActionTemplate<int>.Create((x, ct) =>
            {
                throw new InvalidOperationException("original");
            })
            .OnError((ctx, err) => throw new Exception("handler error"))
            .OnError((ctx, err) => log.Add("second handler"))
            .Build();

        var (success, error) = await template.TryExecuteAsync(5);

        Assert.False(success);
        Assert.Equal("original", error);
        Assert.Single(log);
        Assert.Equal("second handler", log[0]);
    }

    #endregion

    #region ActionTemplate<TIn> Tests

    [Fact]
    public void ActionTemplate_Executes()
    {
        var executed = false;
        var template = ActionTemplate<int>.Create(x => executed = true)
            .Build();

        template.Execute(5);

        Assert.True(executed);
    }

    [Fact]
    public void ActionTemplate_Steps_In_Order()
    {
        var log = new List<string>();
        var template = ActionTemplate<int>.Create(x => log.Add("core"))
            .Before(x => log.Add("before"))
            .After(x => log.Add("after"))
            .Build();

        template.Execute(5);

        Assert.Equal("before", log[0]);
        Assert.Equal("core", log[1]);
        Assert.Equal("after", log[2]);
    }

    [Fact]
    public void ActionTemplate_TryExecute_Returns_Success()
    {
        var executed = false;
        var template = ActionTemplate<int>.Create(x => executed = true)
            .Build();

        var success = template.TryExecute(5, out var error);

        Assert.True(success);
        Assert.True(executed);
        Assert.Null(error);
    }

    [Fact]
    public void ActionTemplate_TryExecute_Catches_Exception()
    {
        var template = ActionTemplate<int>.Create(x =>
            {
                throw new InvalidOperationException("test");
            })
            .Build();

        var success = template.TryExecute(5, out var error);

        Assert.False(success);
        Assert.Equal("test", error);
    }

    [Fact]
    public void ActionTemplate_Multiple_Before()
    {
        var log = new List<string>();
        var template = ActionTemplate<int>.Create(x => { })
            .Before(x => log.Add("before-1"))
            .Before(x => log.Add("before-2"))
            .Build();

        template.Execute(5);

        Assert.Contains("before-1", log);
        Assert.Contains("before-2", log);
    }

    [Fact]
    public void ActionTemplate_Multiple_After()
    {
        var log = new List<string>();
        var template = ActionTemplate<int>.Create(x => { })
            .After(x => log.Add("after-1"))
            .After(x => log.Add("after-2"))
            .Build();

        template.Execute(5);

        Assert.Contains("after-1", log);
        Assert.Contains("after-2", log);
    }

    [Fact]
    public void ActionTemplate_Synchronized()
    {
        var count = 0;
        var template = ActionTemplate<int>.Create(x =>
            {
                var local = count;
                Thread.Sleep(10);
                count = local + 1;
            })
            .Synchronized()
            .Build();

        Parallel.For(0, 5, _ => template.Execute(0));

        Assert.Equal(5, count);
    }

    #endregion

    #region TemplateMethod Tests

    private sealed class DoubleTemplate : TemplateMethod<int, int>
    {
        protected override int Step(int context) => context * 2;
    }

    [Fact]
    public void TemplateMethod_Executes()
    {
        var template = new DoubleTemplate();

        var result = template.Execute(5);

        Assert.Equal(10, result);
    }

    private sealed class TemplateWithHooks : TemplateMethod<int, int>
    {
        public List<string> Log { get; } = new();

        protected override void OnBefore(int context) => Log.Add("before");
        protected override int Step(int context)
        {
            Log.Add("step");
            return context * 2;
        }
        protected override void OnAfter(int context, int result) => Log.Add("after");
    }

    [Fact]
    public void TemplateMethod_Hooks_Execute_In_Order()
    {
        var template = new TemplateWithHooks();

        var result = template.Execute(5);

        Assert.Equal(10, result);
        Assert.Equal("before", template.Log[0]);
        Assert.Equal("step", template.Log[1]);
        Assert.Equal("after", template.Log[2]);
    }

    #endregion

    #region Null Argument Tests

    [Fact]
    public void AsyncTemplate_Core_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncTemplate<int, int>.Create(null!));
    }

    [Fact]
    public void AsyncActionTemplate_Core_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncActionTemplate<int>.Create(null!));
    }

    [Fact]
    public void ActionTemplate_Core_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionTemplate<int>.Create(null!));
    }

    #endregion
}
