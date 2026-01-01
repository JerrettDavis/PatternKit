using PatternKit.Structural.Proxy;

namespace PatternKit.Tests.Structural.Proxy;

public sealed class AsyncProxyTests
{
    #region AsyncProxy<TIn, TOut> Tests

    [Fact]
    public async Task AsyncProxy_Direct_Delegates_To_Subject()
    {
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) =>
        {
            await Task.Delay(1, ct);
            return x * 2;
        }).Build();

        var result = await proxy.ExecuteAsync(5);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncProxy_VirtualProxy_Lazy_Initialization()
    {
        var initialized = false;
        var proxy = AsyncProxy<int, int>.Create()
            .VirtualProxy(async ct =>
            {
                initialized = true;
                return (x, ct2) => new ValueTask<int>(x * 2);
            })
            .Build();

        Assert.False(initialized);

        var result = await proxy.ExecuteAsync(5);

        Assert.True(initialized);
        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncProxy_ProtectionProxy_Allows_Authorized()
    {
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .ProtectionProxy(async (x, ct) => x > 0)
            .Build();

        var result = await proxy.ExecuteAsync(5);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncProxy_ProtectionProxy_Denies_Unauthorized()
    {
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .ProtectionProxy(async (x, ct) => x > 0)
            .Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            proxy.ExecuteAsync(-5).AsTask());
    }

    [Fact]
    public async Task AsyncProxy_Before_Intercepts()
    {
        var log = new List<string>();
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) =>
        {
            log.Add("subject");
            return x * 2;
        }).Before(async (x, ct) => log.Add($"before-{x}")).Build();

        await proxy.ExecuteAsync(5);

        Assert.Equal("before-5", log[0]);
        Assert.Equal("subject", log[1]);
    }

    [Fact]
    public async Task AsyncProxy_After_Intercepts()
    {
        var log = new List<string>();
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) =>
        {
            log.Add("subject");
            return x * 2;
        }).After(async (input, output, ct) => log.Add($"after-{input}->{output}")).Build();

        await proxy.ExecuteAsync(5);

        Assert.Equal("subject", log[0]);
        Assert.Contains("after-5->10", log[1]);
    }

    [Fact]
    public async Task AsyncProxy_Intercept_Modifies_Behavior()
    {
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .Intercept(async (x, ct, next) => x < 0 ? 0 : await next(x, ct))
            .Build();

        var result = await proxy.ExecuteAsync(-5);

        Assert.Equal(0, result);
    }

    [Fact]
    public void AsyncProxy_Build_Throws_Without_Subject()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncProxy<int, int>.Create().Build());
    }

    #endregion

    #region AsyncActionProxy<TIn> Tests

    [Fact]
    public async Task AsyncActionProxy_Executes()
    {
        var executed = false;
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) =>
        {
            await Task.Delay(1, ct);
            executed = true;
        }).Build();

        await proxy.ExecuteAsync(5);

        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncActionProxy_VirtualProxy_Lazy()
    {
        var initialized = false;
        var executed = false;
        var proxy = AsyncActionProxy<int>.Create()
            .VirtualProxy(async ct =>
            {
                initialized = true;
                return (x, ct2) => { executed = true; return default; };
            })
            .Build();

        Assert.False(initialized);

        await proxy.ExecuteAsync(5);

        Assert.True(initialized);
        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncActionProxy_ProtectionProxy_Allows()
    {
        var executed = false;
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => { executed = true; })
            .ProtectionProxy(async (x, ct) => x > 0)
            .Build();

        await proxy.ExecuteAsync(5);

        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncActionProxy_ProtectionProxy_Denies()
    {
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => { })
            .ProtectionProxy(async (x, ct) => x > 0)
            .Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            proxy.ExecuteAsync(-5).AsTask());
    }

    [Fact]
    public async Task AsyncActionProxy_Before_Intercepts()
    {
        var log = new List<string>();
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => log.Add("subject"))
            .Before(async (x, ct) => log.Add($"before-{x}"))
            .Build();

        await proxy.ExecuteAsync(5);

        Assert.Equal("before-5", log[0]);
        Assert.Equal("subject", log[1]);
    }

    [Fact]
    public async Task AsyncActionProxy_After_Intercepts()
    {
        var log = new List<string>();
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => log.Add("subject"))
            .After(async (x, ct) => log.Add($"after-{x}"))
            .Build();

        await proxy.ExecuteAsync(5);

        Assert.Equal("subject", log[0]);
        Assert.Contains("after-5", log[1]);
    }

    [Fact]
    public async Task AsyncActionProxy_Intercept_Can_Skip()
    {
        var executed = false;
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => { executed = true; })
            .Intercept(async (x, ct, next) =>
            {
                if (x > 0)
                    await next(x, ct);
            })
            .Build();

        await proxy.ExecuteAsync(-5);

        Assert.False(executed);
    }

    [Fact]
    public void AsyncActionProxy_Build_Throws_Without_Subject()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncActionProxy<int>.Create().Build());
    }

    #endregion

    #region ActionProxy<TIn> Tests

    [Fact]
    public void ActionProxy_Executes()
    {
        var executed = false;
        var proxy = ActionProxy<int>.Create(x => executed = true).Build();

        proxy.Execute(5);

        Assert.True(executed);
    }

    [Fact]
    public void ActionProxy_VirtualProxy_Lazy()
    {
        var initialized = false;
        var executed = false;
        var proxy = ActionProxy<int>.Create()
            .VirtualProxy(() =>
            {
                initialized = true;
                return x => { executed = true; };
            })
            .Build();

        Assert.False(initialized);

        proxy.Execute(5);

        Assert.True(initialized);
        Assert.True(executed);
    }

    [Fact]
    public void ActionProxy_ProtectionProxy_Allows()
    {
        var executed = false;
        var proxy = ActionProxy<int>.Create(x => executed = true)
            .ProtectionProxy(x => x > 0)
            .Build();

        proxy.Execute(5);

        Assert.True(executed);
    }

    [Fact]
    public void ActionProxy_ProtectionProxy_Denies()
    {
        var proxy = ActionProxy<int>.Create(x => { })
            .ProtectionProxy(x => x > 0)
            .Build();

        Assert.Throws<UnauthorizedAccessException>(() => proxy.Execute(-5));
    }

    [Fact]
    public void ActionProxy_Before_Intercepts()
    {
        var log = new List<string>();
        var proxy = ActionProxy<int>.Create(x => log.Add("subject"))
            .Before(x => log.Add($"before-{x}"))
            .Build();

        proxy.Execute(5);

        Assert.Equal("before-5", log[0]);
        Assert.Equal("subject", log[1]);
    }

    [Fact]
    public void ActionProxy_After_Intercepts()
    {
        var log = new List<string>();
        var proxy = ActionProxy<int>.Create(x => log.Add("subject"))
            .After(x => log.Add($"after-{x}"))
            .Build();

        proxy.Execute(5);

        Assert.Equal("subject", log[0]);
        Assert.Contains("after-5", log[1]);
    }

    [Fact]
    public void ActionProxy_Intercept_Can_Skip()
    {
        var executed = false;
        var proxy = ActionProxy<int>.Create(x => executed = true)
            .Intercept((x, next) =>
            {
                if (x > 0)
                    next(x);
            })
            .Build();

        proxy.Execute(-5);

        Assert.False(executed);
    }

    [Fact]
    public void ActionProxy_Build_Throws_Without_Subject()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ActionProxy<int>.Create().Build());
    }

    [Fact]
    public void ActionProxy_VirtualProxy_Thread_Safe()
    {
        var initCount = 0;
        var proxy = ActionProxy<int>.Create()
            .VirtualProxy(() =>
            {
                Interlocked.Increment(ref initCount);
                Thread.Sleep(10);
                return x => { };
            })
            .Build();

        Parallel.For(0, 10, _ => proxy.Execute(5));

        Assert.Equal(1, initCount);
    }

    #endregion

    #region Null Argument Tests

    [Fact]
    public void AsyncProxy_VirtualProxy_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncProxy<int, int>.Create().VirtualProxy((AsyncProxy<int, int>.SubjectFactory)null!));
    }

    [Fact]
    public void AsyncProxy_ProtectionProxy_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncProxy<int, int>.Create(async (x, ct) => x).ProtectionProxy((AsyncProxy<int, int>.AccessValidator)null!));
    }

    [Fact]
    public void AsyncProxy_Before_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncProxy<int, int>.Create(async (x, ct) => x).Before(null!));
    }

    [Fact]
    public void AsyncProxy_After_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncProxy<int, int>.Create(async (x, ct) => x).After(null!));
    }

    [Fact]
    public void AsyncProxy_Intercept_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncProxy<int, int>.Create(async (x, ct) => x).Intercept(null!));
    }

    [Fact]
    public void ActionProxy_VirtualProxy_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionProxy<int>.Create().VirtualProxy(null!));
    }

    [Fact]
    public void ActionProxy_ProtectionProxy_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionProxy<int>.Create(x => { }).ProtectionProxy(null!));
    }

    [Fact]
    public void ActionProxy_Before_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionProxy<int>.Create(x => { }).Before(null!));
    }

    [Fact]
    public void ActionProxy_After_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionProxy<int>.Create(x => { }).After(null!));
    }

    [Fact]
    public void ActionProxy_Intercept_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionProxy<int>.Create(x => { }).Intercept(null!));
    }

    #endregion

    #region Additional AsyncProxy Tests

    [Fact]
    public async Task AsyncProxy_VirtualProxy_Caches_Subject()
    {
        var initCount = 0;
        var proxy = AsyncProxy<int, int>.Create()
            .VirtualProxy(async ct =>
            {
                Interlocked.Increment(ref initCount);
                return (x, ct2) => new ValueTask<int>(x * 2);
            })
            .Build();

        await proxy.ExecuteAsync(1);
        await proxy.ExecuteAsync(2);
        await proxy.ExecuteAsync(3);

        Assert.Equal(1, initCount);
    }

    [Fact]
    public async Task AsyncProxy_VirtualProxy_Sync_Factory()
    {
        var initialized = false;
        var proxy = AsyncProxy<int, int>.Create()
            .VirtualProxy(() =>
            {
                initialized = true;
                return (x, ct) => new ValueTask<int>(x * 3);
            })
            .Build();

        var result = await proxy.ExecuteAsync(10);

        Assert.True(initialized);
        Assert.Equal(30, result);
    }

    [Fact]
    public async Task AsyncProxy_ProtectionProxy_Sync_Validator()
    {
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .ProtectionProxy(x => x >= 0)
            .Build();

        var result = await proxy.ExecuteAsync(5);
        Assert.Equal(10, result);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            proxy.ExecuteAsync(-1).AsTask());
    }

    [Fact]
    public async Task AsyncProxy_Before_Sync_Action()
    {
        var log = new List<string>();
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .Before(x => log.Add($"before:{x}"))
            .Build();

        var result = await proxy.ExecuteAsync(5);

        Assert.Equal(10, result);
        Assert.Contains("before:5", log);
    }

    [Fact]
    public async Task AsyncProxy_After_Sync_Action()
    {
        var log = new List<string>();
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .After((x, r) => log.Add($"after:{x}={r}"))
            .Build();

        var result = await proxy.ExecuteAsync(5);

        Assert.Equal(10, result);
        Assert.Contains("after:5=10", log);
    }

    [Fact]
    public async Task AsyncProxy_VirtualProxy_Thread_Safe()
    {
        var initCount = 0;
        var proxy = AsyncProxy<int, int>.Create()
            .VirtualProxy(async ct =>
            {
                Interlocked.Increment(ref initCount);
                await Task.Delay(10, ct); // simulate slow init
                return (x, ct2) => new ValueTask<int>(x * 2);
            })
            .Build();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => proxy.ExecuteAsync(5).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, initCount);
        Assert.All(tasks, t => Assert.Equal(10, t.Result));
    }

    [Fact]
    public void AsyncProxy_Intercept_NoSubject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncProxy<int, int>.Create()
                .Intercept(async (x, ct, next) => x));
    }

    [Fact]
    public void AsyncProxy_Before_NoSubject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncProxy<int, int>.Create()
                .Before(async (x, ct) => { }));
    }

    [Fact]
    public void AsyncProxy_After_NoSubject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncProxy<int, int>.Create()
                .After(async (x, r, ct) => { }));
    }

    [Fact]
    public void AsyncProxy_ProtectionProxy_NoSubject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncProxy<int, int>.Create()
                .ProtectionProxy(async (x, ct) => true));
    }

    [Fact]
    public async Task AsyncActionProxy_VirtualProxy_Sync_Factory()
    {
        var initialized = false;
        var executed = false;
        var proxy = AsyncActionProxy<int>.Create()
            .VirtualProxy(() =>
            {
                initialized = true;
                return (x, ct) => { executed = true; return default; };
            })
            .Build();

        await proxy.ExecuteAsync(5);

        Assert.True(initialized);
        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncActionProxy_ProtectionProxy_Sync_Validator()
    {
        var executed = false;
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => { executed = true; })
            .ProtectionProxy(x => x >= 0)
            .Build();

        await proxy.ExecuteAsync(5);
        Assert.True(executed);

        executed = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            proxy.ExecuteAsync(-1).AsTask());
        Assert.False(executed);
    }

    [Fact]
    public async Task AsyncActionProxy_Before_Sync()
    {
        var log = new List<string>();
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => log.Add("subject"))
            .Before(x => log.Add($"before:{x}"))
            .Build();

        await proxy.ExecuteAsync(5);

        Assert.Equal(2, log.Count);
        Assert.Equal("before:5", log[0]);
        Assert.Equal("subject", log[1]);
    }

    [Fact]
    public async Task AsyncActionProxy_After_Sync()
    {
        var log = new List<string>();
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => log.Add("subject"))
            .After(x => log.Add($"after:{x}"))
            .Build();

        await proxy.ExecuteAsync(5);

        Assert.Equal(2, log.Count);
        Assert.Equal("subject", log[0]);
        Assert.Equal("after:5", log[1]);
    }

    [Fact]
    public void AsyncActionProxy_Intercept_NoSubject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncActionProxy<int>.Create()
                .Intercept(async (x, ct, next) => { }));
    }

    [Fact]
    public void AsyncActionProxy_Before_NoSubject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncActionProxy<int>.Create()
                .Before(async (x, ct) => { }));
    }

    [Fact]
    public void AsyncActionProxy_After_NoSubject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncActionProxy<int>.Create()
                .After(async (x, ct) => { }));
    }

    [Fact]
    public void AsyncActionProxy_ProtectionProxy_NoSubject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncActionProxy<int>.Create()
                .ProtectionProxy(async (x, ct) => true));
    }

    [Fact]
    public async Task AsyncActionProxy_VirtualProxy_Caches_Subject()
    {
        var initCount = 0;
        var proxy = AsyncActionProxy<int>.Create()
            .VirtualProxy(async ct =>
            {
                Interlocked.Increment(ref initCount);
                return (x, ct2) => default;
            })
            .Build();

        await proxy.ExecuteAsync(1);
        await proxy.ExecuteAsync(2);
        await proxy.ExecuteAsync(3);

        Assert.Equal(1, initCount);
    }

    [Fact]
    public async Task AsyncActionProxy_VirtualProxy_Thread_Safe()
    {
        var initCount = 0;
        var execCount = 0;
        var proxy = AsyncActionProxy<int>.Create()
            .VirtualProxy(async ct =>
            {
                Interlocked.Increment(ref initCount);
                await Task.Delay(10, ct);
                return (x, ct2) => { Interlocked.Increment(ref execCount); return default; };
            })
            .Build();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => proxy.ExecuteAsync(5).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, initCount);
        Assert.Equal(10, execCount);
    }

    [Fact]
    public async Task AsyncActionProxy_Null_VirtualProxy_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncActionProxy<int>.Create()
                .VirtualProxy((AsyncActionProxy<int>.SubjectFactory)null!));
    }

    [Fact]
    public async Task AsyncActionProxy_Null_ProtectionProxy_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncActionProxy<int>.Create(async (x, ct) => { })
                .ProtectionProxy((AsyncActionProxy<int>.AccessValidator)null!));
    }

    [Fact]
    public async Task AsyncActionProxy_Null_Before_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncActionProxy<int>.Create(async (x, ct) => { })
                .Before((AsyncActionProxy<int>.ActionHook)null!));
    }

    [Fact]
    public async Task AsyncActionProxy_Null_After_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncActionProxy<int>.Create(async (x, ct) => { })
                .After((AsyncActionProxy<int>.ActionHook)null!));
    }

    [Fact]
    public async Task AsyncActionProxy_Null_Intercept_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AsyncActionProxy<int>.Create(async (x, ct) => { })
                .Intercept(null!));
    }

    #endregion
}
