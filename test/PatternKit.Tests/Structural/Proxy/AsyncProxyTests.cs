using PatternKit.Structural.Proxy;
using TinyBDD;

namespace PatternKit.Tests.Structural.Proxy;

public sealed class AsyncProxyTests
{
    #region AsyncProxy<TIn, TOut> Tests

    [Scenario("AsyncProxy Direct Delegates To Subject")]
    [Fact]
    public async Task AsyncProxy_Direct_Delegates_To_Subject()
    {
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) =>
        {
            await Task.Delay(1, ct);
            return x * 2;
        }).Build();

        var result = await proxy.ExecuteAsync(5);

        ScenarioExpect.Equal(10, result);
    }

    [Scenario("AsyncProxy VirtualProxy Lazy Initialization")]
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

        ScenarioExpect.False(initialized);

        var result = await proxy.ExecuteAsync(5);

        ScenarioExpect.True(initialized);
        ScenarioExpect.Equal(10, result);
    }

    [Scenario("AsyncProxy ProtectionProxy Allows Authorized")]
    [Fact]
    public async Task AsyncProxy_ProtectionProxy_Allows_Authorized()
    {
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .ProtectionProxy(async (x, ct) => x > 0)
            .Build();

        var result = await proxy.ExecuteAsync(5);

        ScenarioExpect.Equal(10, result);
    }

    [Scenario("AsyncProxy ProtectionProxy Denies Unauthorized")]
    [Fact]
    public async Task AsyncProxy_ProtectionProxy_Denies_Unauthorized()
    {
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .ProtectionProxy(async (x, ct) => x > 0)
            .Build();

        await ScenarioExpect.ThrowsAsync<UnauthorizedAccessException>(() =>
            proxy.ExecuteAsync(-5).AsTask());
    }

    [Scenario("AsyncProxy Before Intercepts")]
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

        ScenarioExpect.Equal("before-5", log[0]);
        ScenarioExpect.Equal("subject", log[1]);
    }

    [Scenario("AsyncProxy After Intercepts")]
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

        ScenarioExpect.Equal("subject", log[0]);
        ScenarioExpect.Contains("after-5->10", log[1]);
    }

    [Scenario("AsyncProxy Intercept Modifies Behavior")]
    [Fact]
    public async Task AsyncProxy_Intercept_Modifies_Behavior()
    {
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .Intercept(async (x, ct, next) => x < 0 ? 0 : await next(x, ct))
            .Build();

        var result = await proxy.ExecuteAsync(-5);

        ScenarioExpect.Equal(0, result);
    }

    [Scenario("AsyncProxy Build Throws Without Subject")]
    [Fact]
    public void AsyncProxy_Build_Throws_Without_Subject()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncProxy<int, int>.Create().Build());
    }

    #endregion

    #region AsyncActionProxy<TIn> Tests

    [Scenario("AsyncActionProxy Executes")]
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

        ScenarioExpect.True(executed);
    }

    [Scenario("AsyncActionProxy VirtualProxy Lazy")]
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

        ScenarioExpect.False(initialized);

        await proxy.ExecuteAsync(5);

        ScenarioExpect.True(initialized);
        ScenarioExpect.True(executed);
    }

    [Scenario("AsyncActionProxy ProtectionProxy Allows")]
    [Fact]
    public async Task AsyncActionProxy_ProtectionProxy_Allows()
    {
        var executed = false;
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => { executed = true; })
            .ProtectionProxy(async (x, ct) => x > 0)
            .Build();

        await proxy.ExecuteAsync(5);

        ScenarioExpect.True(executed);
    }

    [Scenario("AsyncActionProxy ProtectionProxy Denies")]
    [Fact]
    public async Task AsyncActionProxy_ProtectionProxy_Denies()
    {
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => { })
            .ProtectionProxy(async (x, ct) => x > 0)
            .Build();

        await ScenarioExpect.ThrowsAsync<UnauthorizedAccessException>(() =>
            proxy.ExecuteAsync(-5).AsTask());
    }

    [Scenario("AsyncActionProxy Before Intercepts")]
    [Fact]
    public async Task AsyncActionProxy_Before_Intercepts()
    {
        var log = new List<string>();
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => log.Add("subject"))
            .Before(async (x, ct) => log.Add($"before-{x}"))
            .Build();

        await proxy.ExecuteAsync(5);

        ScenarioExpect.Equal("before-5", log[0]);
        ScenarioExpect.Equal("subject", log[1]);
    }

    [Scenario("AsyncActionProxy After Intercepts")]
    [Fact]
    public async Task AsyncActionProxy_After_Intercepts()
    {
        var log = new List<string>();
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => log.Add("subject"))
            .After(async (x, ct) => log.Add($"after-{x}"))
            .Build();

        await proxy.ExecuteAsync(5);

        ScenarioExpect.Equal("subject", log[0]);
        ScenarioExpect.Contains("after-5", log[1]);
    }

    [Scenario("AsyncActionProxy Intercept Can Skip")]
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

        ScenarioExpect.False(executed);
    }

    [Scenario("AsyncActionProxy Build Throws Without Subject")]
    [Fact]
    public void AsyncActionProxy_Build_Throws_Without_Subject()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncActionProxy<int>.Create().Build());
    }

    #endregion

    #region ActionProxy<TIn> Tests

    [Scenario("ActionProxy Executes")]
    [Fact]
    public void ActionProxy_Executes()
    {
        var executed = false;
        var proxy = ActionProxy<int>.Create(x => executed = true).Build();

        proxy.Execute(5);

        ScenarioExpect.True(executed);
    }

    [Scenario("ActionProxy VirtualProxy Lazy")]
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

        ScenarioExpect.False(initialized);

        proxy.Execute(5);

        ScenarioExpect.True(initialized);
        ScenarioExpect.True(executed);
    }

    [Scenario("ActionProxy ProtectionProxy Allows")]
    [Fact]
    public void ActionProxy_ProtectionProxy_Allows()
    {
        var executed = false;
        var proxy = ActionProxy<int>.Create(x => executed = true)
            .ProtectionProxy(x => x > 0)
            .Build();

        proxy.Execute(5);

        ScenarioExpect.True(executed);
    }

    [Scenario("ActionProxy ProtectionProxy Denies")]
    [Fact]
    public void ActionProxy_ProtectionProxy_Denies()
    {
        var proxy = ActionProxy<int>.Create(x => { })
            .ProtectionProxy(x => x > 0)
            .Build();

        ScenarioExpect.Throws<UnauthorizedAccessException>(() => proxy.Execute(-5));
    }

    [Scenario("ActionProxy Before Intercepts")]
    [Fact]
    public void ActionProxy_Before_Intercepts()
    {
        var log = new List<string>();
        var proxy = ActionProxy<int>.Create(x => log.Add("subject"))
            .Before(x => log.Add($"before-{x}"))
            .Build();

        proxy.Execute(5);

        ScenarioExpect.Equal("before-5", log[0]);
        ScenarioExpect.Equal("subject", log[1]);
    }

    [Scenario("ActionProxy After Intercepts")]
    [Fact]
    public void ActionProxy_After_Intercepts()
    {
        var log = new List<string>();
        var proxy = ActionProxy<int>.Create(x => log.Add("subject"))
            .After(x => log.Add($"after-{x}"))
            .Build();

        proxy.Execute(5);

        ScenarioExpect.Equal("subject", log[0]);
        ScenarioExpect.Contains("after-5", log[1]);
    }

    [Scenario("ActionProxy Intercept Can Skip")]
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

        ScenarioExpect.False(executed);
    }

    [Scenario("ActionProxy Build Throws Without Subject")]
    [Fact]
    public void ActionProxy_Build_Throws_Without_Subject()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            ActionProxy<int>.Create().Build());
    }

    [Scenario("ActionProxy VirtualProxy Thread Safe")]
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

        ScenarioExpect.Equal(1, initCount);
    }

    #endregion

    #region Null Argument Tests

    [Scenario("AsyncProxy VirtualProxy Null Throws")]
    [Fact]
    public void AsyncProxy_VirtualProxy_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncProxy<int, int>.Create().VirtualProxy((AsyncProxy<int, int>.SubjectFactory)null!));
    }

    [Scenario("AsyncProxy ProtectionProxy Null Throws")]
    [Fact]
    public void AsyncProxy_ProtectionProxy_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncProxy<int, int>.Create(async (x, ct) => x).ProtectionProxy((AsyncProxy<int, int>.AccessValidator)null!));
    }

    [Scenario("AsyncProxy Before Null Throws")]
    [Fact]
    public void AsyncProxy_Before_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncProxy<int, int>.Create(async (x, ct) => x).Before(null!));
    }

    [Scenario("AsyncProxy After Null Throws")]
    [Fact]
    public void AsyncProxy_After_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncProxy<int, int>.Create(async (x, ct) => x).After(null!));
    }

    [Scenario("AsyncProxy Intercept Null Throws")]
    [Fact]
    public void AsyncProxy_Intercept_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncProxy<int, int>.Create(async (x, ct) => x).Intercept(null!));
    }

    [Scenario("ActionProxy VirtualProxy Null Throws")]
    [Fact]
    public void ActionProxy_VirtualProxy_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            ActionProxy<int>.Create().VirtualProxy(null!));
    }

    [Scenario("ActionProxy ProtectionProxy Null Throws")]
    [Fact]
    public void ActionProxy_ProtectionProxy_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            ActionProxy<int>.Create(x => { }).ProtectionProxy(null!));
    }

    [Scenario("ActionProxy Before Null Throws")]
    [Fact]
    public void ActionProxy_Before_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            ActionProxy<int>.Create(x => { }).Before(null!));
    }

    [Scenario("ActionProxy After Null Throws")]
    [Fact]
    public void ActionProxy_After_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            ActionProxy<int>.Create(x => { }).After(null!));
    }

    [Scenario("ActionProxy Intercept Null Throws")]
    [Fact]
    public void ActionProxy_Intercept_Null_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            ActionProxy<int>.Create(x => { }).Intercept(null!));
    }

    #endregion

    #region Additional AsyncProxy Tests

    [Scenario("AsyncProxy VirtualProxy Caches Subject")]
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

        ScenarioExpect.Equal(1, initCount);
    }

    [Scenario("AsyncProxy VirtualProxy Sync Factory")]
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

        ScenarioExpect.True(initialized);
        ScenarioExpect.Equal(30, result);
    }

    [Scenario("AsyncProxy ProtectionProxy Sync Validator")]
    [Fact]
    public async Task AsyncProxy_ProtectionProxy_Sync_Validator()
    {
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .ProtectionProxy(x => x >= 0)
            .Build();

        var result = await proxy.ExecuteAsync(5);
        ScenarioExpect.Equal(10, result);

        await ScenarioExpect.ThrowsAsync<UnauthorizedAccessException>(() =>
            proxy.ExecuteAsync(-1).AsTask());
    }

    [Scenario("AsyncProxy Before Sync Action")]
    [Fact]
    public async Task AsyncProxy_Before_Sync_Action()
    {
        var log = new List<string>();
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .Before(x => log.Add($"before:{x}"))
            .Build();

        var result = await proxy.ExecuteAsync(5);

        ScenarioExpect.Equal(10, result);
        ScenarioExpect.Contains("before:5", log);
    }

    [Scenario("AsyncProxy After Sync Action")]
    [Fact]
    public async Task AsyncProxy_After_Sync_Action()
    {
        var log = new List<string>();
        var proxy = AsyncProxy<int, int>.Create(async (x, ct) => x * 2)
            .After((x, r) => log.Add($"after:{x}={r}"))
            .Build();

        var result = await proxy.ExecuteAsync(5);

        ScenarioExpect.Equal(10, result);
        ScenarioExpect.Contains("after:5=10", log);
    }

    [Scenario("AsyncProxy VirtualProxy Thread Safe")]
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

        ScenarioExpect.Equal(1, initCount);
        ScenarioExpect.All(tasks, t => ScenarioExpect.Equal(10, t.Result));
    }

    [Scenario("AsyncProxy Intercept NoSubject Throws")]
    [Fact]
    public void AsyncProxy_Intercept_NoSubject_Throws()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncProxy<int, int>.Create()
                .Intercept(async (x, ct, next) => x));
    }

    [Scenario("AsyncProxy Before NoSubject Throws")]
    [Fact]
    public void AsyncProxy_Before_NoSubject_Throws()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncProxy<int, int>.Create()
                .Before(async (x, ct) => { }));
    }

    [Scenario("AsyncProxy After NoSubject Throws")]
    [Fact]
    public void AsyncProxy_After_NoSubject_Throws()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncProxy<int, int>.Create()
                .After(async (x, r, ct) => { }));
    }

    [Scenario("AsyncProxy ProtectionProxy NoSubject Throws")]
    [Fact]
    public void AsyncProxy_ProtectionProxy_NoSubject_Throws()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncProxy<int, int>.Create()
                .ProtectionProxy(async (x, ct) => true));
    }

    [Scenario("AsyncActionProxy VirtualProxy Sync Factory")]
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

        ScenarioExpect.True(initialized);
        ScenarioExpect.True(executed);
    }

    [Scenario("AsyncActionProxy ProtectionProxy Sync Validator")]
    [Fact]
    public async Task AsyncActionProxy_ProtectionProxy_Sync_Validator()
    {
        var executed = false;
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => { executed = true; })
            .ProtectionProxy(x => x >= 0)
            .Build();

        await proxy.ExecuteAsync(5);
        ScenarioExpect.True(executed);

        executed = false;
        await ScenarioExpect.ThrowsAsync<UnauthorizedAccessException>(() =>
            proxy.ExecuteAsync(-1).AsTask());
        ScenarioExpect.False(executed);
    }

    [Scenario("AsyncActionProxy Before Sync")]
    [Fact]
    public async Task AsyncActionProxy_Before_Sync()
    {
        var log = new List<string>();
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => log.Add("subject"))
            .Before(x => log.Add($"before:{x}"))
            .Build();

        await proxy.ExecuteAsync(5);

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("before:5", log[0]);
        ScenarioExpect.Equal("subject", log[1]);
    }

    [Scenario("AsyncActionProxy After Sync")]
    [Fact]
    public async Task AsyncActionProxy_After_Sync()
    {
        var log = new List<string>();
        var proxy = AsyncActionProxy<int>.Create(async (x, ct) => log.Add("subject"))
            .After(x => log.Add($"after:{x}"))
            .Build();

        await proxy.ExecuteAsync(5);

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("subject", log[0]);
        ScenarioExpect.Equal("after:5", log[1]);
    }

    [Scenario("AsyncActionProxy Intercept NoSubject Throws")]
    [Fact]
    public void AsyncActionProxy_Intercept_NoSubject_Throws()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncActionProxy<int>.Create()
                .Intercept(async (x, ct, next) => { }));
    }

    [Scenario("AsyncActionProxy Before NoSubject Throws")]
    [Fact]
    public void AsyncActionProxy_Before_NoSubject_Throws()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncActionProxy<int>.Create()
                .Before(async (x, ct) => { }));
    }

    [Scenario("AsyncActionProxy After NoSubject Throws")]
    [Fact]
    public void AsyncActionProxy_After_NoSubject_Throws()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncActionProxy<int>.Create()
                .After(async (x, ct) => { }));
    }

    [Scenario("AsyncActionProxy ProtectionProxy NoSubject Throws")]
    [Fact]
    public void AsyncActionProxy_ProtectionProxy_NoSubject_Throws()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncActionProxy<int>.Create()
                .ProtectionProxy(async (x, ct) => true));
    }

    [Scenario("AsyncActionProxy VirtualProxy Caches Subject")]
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

        ScenarioExpect.Equal(1, initCount);
    }

    [Scenario("AsyncActionProxy VirtualProxy Thread Safe")]
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

        ScenarioExpect.Equal(1, initCount);
        ScenarioExpect.Equal(10, execCount);
    }

    [Scenario("AsyncActionProxy Null VirtualProxy Throws")]
    [Fact]
    public async Task AsyncActionProxy_Null_VirtualProxy_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncActionProxy<int>.Create()
                .VirtualProxy((AsyncActionProxy<int>.SubjectFactory)null!));
    }

    [Scenario("AsyncActionProxy Null ProtectionProxy Throws")]
    [Fact]
    public async Task AsyncActionProxy_Null_ProtectionProxy_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncActionProxy<int>.Create(async (x, ct) => { })
                .ProtectionProxy((AsyncActionProxy<int>.AccessValidator)null!));
    }

    [Scenario("AsyncActionProxy Null Before Throws")]
    [Fact]
    public async Task AsyncActionProxy_Null_Before_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncActionProxy<int>.Create(async (x, ct) => { })
                .Before((AsyncActionProxy<int>.ActionHook)null!));
    }

    [Scenario("AsyncActionProxy Null After Throws")]
    [Fact]
    public async Task AsyncActionProxy_Null_After_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncActionProxy<int>.Create(async (x, ct) => { })
                .After((AsyncActionProxy<int>.ActionHook)null!));
    }

    [Scenario("AsyncActionProxy Null Intercept Throws")]
    [Fact]
    public async Task AsyncActionProxy_Null_Intercept_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncActionProxy<int>.Create(async (x, ct) => { })
                .Intercept(null!));
    }

    #endregion
}
