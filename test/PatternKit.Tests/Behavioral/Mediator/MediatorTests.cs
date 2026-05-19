using TinyBDD;
using TinyBDD.Assertions;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Mediator;

[Feature("Behavioral - Mediator (commands, notifications, streaming, behaviors)")]
public sealed class MediatorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Ping(int Value);
    private sealed record Pong(string Value);

    private sealed record Note(string Text);

#if NET8_0_OR_GREATER
    private static async IAsyncEnumerable<int> Range(int start, int count)
    {
        for (var i = 0; i < count; i++) { await Task.Yield(); yield return start + i; }
    }
#endif

    [Scenario("Send: command handler runs through behaviors and returns value")]
    [Fact]
    public Task Send_Command_Works()
        => Given("a mediator with pre/post/whole behaviors and a Ping->Pong handler", () =>
        {
            var log = new List<string>();
            var m = PatternKit.Behavioral.Mediator.Mediator.Create()
                .Pre((in _, _) => { log.Add("pre"); return default; })
                .Whole((in req, ct, next) =>
                {
                    log.Add("whole:before");
                    var vt = next(in req, ct);
                    if (vt.IsCompletedSuccessfully)
                    {
                        var res = vt.Result;
                        log.Add("whole:after");
                        return new ValueTask<object?>(res);
                    }
                    return AwaitAndLog(vt, log);

                    static async ValueTask<object?> AwaitAndLog(ValueTask<object?> pending, List<string> lg)
                    {
                        var res = await pending.ConfigureAwait(false);
                        lg.Add("whole:after");
                        return res;
                    }
                })
                .Post((in _, res, _) => { log.Add($"post:{(res is Pong p ? p.Value : "null")}"); return default; })
                .Command<Ping, Pong>(static (in p, _) => new ValueTask<Pong>(new Pong($"pong:{p.Value}")))
                .Build();
            return (m, log);
        })
        .When("sending Ping(5)", async Task<(Pong r, List<string> log)> (t) => { var (m, log) = t; var r = await m.Send<Ping, Pong>(new Ping(5)); return (r, log)!; })
        .Then("result is pong:5", t => Expect.For(t.r.Value).ToBe("pong:5"))
        .And("behaviors logged pre, whole before/after, and post", t => Expect.For(string.Join('|', t.log)).ToBe("pre|whole:before|whole:after|post:pong:5"))
        .AssertPassed();

    [Scenario("Publish: notification invokes all handlers and pre/post behaviors")]
    [Fact]
    public Task Publish_Notifications_FanOut()
        => Given("a mediator with two notification handlers and pre/post", () =>
        {
            var log = new List<string>();
            var m = PatternKit.Behavioral.Mediator.Mediator.Create()
                .Pre((in _, _) => { log.Add("pre"); return default; })
                .Post((in _, _, _) => { log.Add("post"); return default; })
                .Notification<Note>((in n, _) => { log.Add($"note1:{n.Text}"); return default; })
                .Notification<Note>((in n, _) => { log.Add($"note2:{n.Text}"); return default; })
                .Build();
            return (m, log);
        })
        .When("publishing Note('hi')", async Task<List<string>> (t) => { var (m, log) = t; var note = new Note("hi"); await m.Publish(in note); return log; })
        .Then("pre then two handlers then post", log => string.Join('|', log) == "pre|note1:hi|note2:hi|post")
        .AssertPassed();

#if NET8_0_OR_GREATER
    [Scenario("Stream: streaming command yields items and respects behaviors")]
    [Fact]
    public Task Stream_Command_Yields()
        => Given("a mediator with a streaming handler", () =>
        {
            var log = new List<string>();
            var m = PatternKit.Behavioral.Mediator.Mediator.Create()
                .Pre((in _, _) => { log.Add("pre"); return default; })
                .Whole((in req, ct, next) =>
                {
                    log.Add("whole");
                    return next(in req, ct);
                })
                .Post((in _, _, _) => { log.Add("post"); return default; })
                .Stream<Ping, int>(static (in p, _) => Range(p.Value, 3))
                .Build();
            return (m, log);
        })
        .When("streaming", async Task<(List<int> list, List<string> log)> (t) =>
        {
            var (m, log) = t;
            var list = new List<int>();
            await foreach (var x in m.Stream<Ping, int>(new Ping(2))) list.Add(x);
            return (list, log);
        })
        .Then("collected 2,3,4", t => string.Join(',', t.list) == "2,3,4")
        .And("pre and whole and post ran", t => string.Join('|', t.log) == "pre|whole|post")
        .AssertPassed();
#endif
}

#region Additional Mediator Tests

public sealed class MediatorBuilderTests
{
    private sealed record Ping(int Value);
    private sealed record Pong(string Value);
    private sealed record Alert(string Message);

    [Scenario("Send NoHandler Throws")]
    [Fact]
    public async Task Send_NoHandler_Throws()
    {
        var m = PatternKit.Behavioral.Mediator.Mediator.Create().Build();

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(
            () => m.Send<Ping, Pong>(new Ping(1)).AsTask());
    }

    [Scenario("Send NullResult ReturnsDefault")]
    [Fact]
    public async Task Send_NullResult_ReturnsDefault()
    {
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Command<Ping, Pong>((in p, _) => new ValueTask<Pong>((Pong)null!))
            .Build();

        var result = await m.Send<Ping, Pong>(new Ping(1));
        ScenarioExpect.Null(result);
    }

    [Scenario("Publish NoHandlers NoOp")]
    [Fact]
    public async Task Publish_NoHandlers_NoOp()
    {
        var m = PatternKit.Behavioral.Mediator.Mediator.Create().Build();

        // Should not throw
        await m.Publish(new Alert("test"));
    }

    [Scenario("Publish SingleHandler Executes")]
    [Fact]
    public async Task Publish_SingleHandler_Executes()
    {
        var received = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Notification<Alert>((in a, _) => { received.Add(a.Message); return default; })
            .Build();

        await m.Publish(new Alert("hello"));

        ScenarioExpect.Single(received);
        ScenarioExpect.Equal("hello", received[0]);
    }

    [Scenario("Publish MultipleHandlers AllExecute")]
    [Fact]
    public async Task Publish_MultipleHandlers_AllExecute()
    {
        var log = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Notification<Alert>((in a, _) => { log.Add($"h1:{a.Message}"); return default; })
            .Notification<Alert>((in a, _) => { log.Add($"h2:{a.Message}"); return default; })
            .Notification<Alert>((in a, _) => { log.Add($"h3:{a.Message}"); return default; })
            .Build();

        await m.Publish(new Alert("test"));

        ScenarioExpect.Equal(3, log.Count);
        ScenarioExpect.Equal("h1:test", log[0]);
        ScenarioExpect.Equal("h2:test", log[1]);
        ScenarioExpect.Equal("h3:test", log[2]);
    }

    [Scenario("Pre Behavior Runs Before Handler")]
    [Fact]
    public async Task Pre_Behavior_Runs_Before_Handler()
    {
        var log = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Pre((in _, _) => { log.Add("pre"); return default; })
            .Command<Ping, Pong>((in p, _) => { log.Add("handler"); return new ValueTask<Pong>(new Pong("done")); })
            .Build();

        await m.Send<Ping, Pong>(new Ping(1));

        ScenarioExpect.Equal(new[] { "pre", "handler" }, log);
    }

    [Scenario("Post Behavior Runs After Handler")]
    [Fact]
    public async Task Post_Behavior_Runs_After_Handler()
    {
        var log = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Post((in _, _, _) => { log.Add("post"); return default; })
            .Command<Ping, Pong>((in p, _) => { log.Add("handler"); return new ValueTask<Pong>(new Pong("done")); })
            .Build();

        await m.Send<Ping, Pong>(new Ping(1));

        ScenarioExpect.Equal(new[] { "handler", "post" }, log);
    }

    [Scenario("Multiple Pre And Post Behaviors")]
    [Fact]
    public async Task Multiple_Pre_And_Post_Behaviors()
    {
        var log = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Pre((in _, _) => { log.Add("pre1"); return default; })
            .Pre((in _, _) => { log.Add("pre2"); return default; })
            .Post((in _, _, _) => { log.Add("post1"); return default; })
            .Post((in _, _, _) => { log.Add("post2"); return default; })
            .Command<Ping, Pong>((in p, _) => { log.Add("handler"); return new ValueTask<Pong>(new Pong("done")); })
            .Build();

        await m.Send<Ping, Pong>(new Ping(1));

        ScenarioExpect.Equal(new[] { "pre1", "pre2", "handler", "post1", "post2" }, log);
    }

    [Scenario("Whole Behavior Wraps Handler")]
    [Fact]
    public async Task Whole_Behavior_Wraps_Handler()
    {
        var log = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Whole((in req, ct, next) =>
            {
                log.Add("whole:before");
                var result = next(in req, ct);
                log.Add("whole:after");
                return result;
            })
            .Command<Ping, Pong>((in p, _) => { log.Add("handler"); return new ValueTask<Pong>(new Pong("done")); })
            .Build();

        await m.Send<Ping, Pong>(new Ping(1));

        ScenarioExpect.Equal(new[] { "whole:before", "handler", "whole:after" }, log);
    }

    [Scenario("Multiple Whole Behaviors Wrap InOrder")]
    [Fact]
    public async Task Multiple_Whole_Behaviors_Wrap_InOrder()
    {
        var log = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Whole((in req, ct, next) =>
            {
                log.Add("outer:before");
                var result = next(in req, ct);
                log.Add("outer:after");
                return result;
            })
            .Whole((in req, ct, next) =>
            {
                log.Add("inner:before");
                var result = next(in req, ct);
                log.Add("inner:after");
                return result;
            })
            .Command<Ping, Pong>((in p, _) => { log.Add("handler"); return new ValueTask<Pong>(new Pong("done")); })
            .Build();

        await m.Send<Ping, Pong>(new Ping(1));

        // Last registered is innermost
        ScenarioExpect.Equal(new[] { "outer:before", "inner:before", "handler", "inner:after", "outer:after" }, log);
    }

    [Scenario("Sync Command Handler")]
    [Fact]
    public async Task Sync_Command_Handler()
    {
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Command<Ping, Pong>(static (in p) => new Pong($"sync:{p.Value}"))
            .Build();

        var result = await m.Send<Ping, Pong>(new Ping(42));

        ScenarioExpect.NotNull(result);
        ScenarioExpect.Equal("sync:42", result!.Value);
    }

    [Scenario("Async Whole Behavior")]
    [Fact]
    public async Task Async_Whole_Behavior()
    {
        var log = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Whole((in req, ct, next) =>
            {
                log.Add("whole:before");
                return WholeAsync(req, ct, next, log);
                static async ValueTask<object?> WholeAsync(object? r, CancellationToken c, PatternKit.Behavioral.Mediator.Mediator.MediatorNext n, List<string> l)
                {
                    await Task.Yield();
                    var result = await n(in r, c);
                    l.Add("whole:after");
                    return result;
                }
            })
            .Command<Ping, Pong>((in p, _) =>
            {
                var val = p.Value;
                return HandlerAsync(val, log);
                static async ValueTask<Pong> HandlerAsync(int v, List<string> l)
                {
                    await Task.Yield();
                    l.Add("handler");
                    return new Pong("done");
                }
            })
            .Build();

        await m.Send<Ping, Pong>(new Ping(1));

        ScenarioExpect.Equal(new[] { "whole:before", "handler", "whole:after" }, log);
    }

    [Scenario("Publish With Behaviors")]
    [Fact]
    public async Task Publish_With_Behaviors()
    {
        var log = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Pre((in _, _) => { log.Add("pre"); return default; })
            .Post((in _, _, _) => { log.Add("post"); return default; })
            .Notification<Alert>((in a, _) => { log.Add($"handler:{a.Message}"); return default; })
            .Build();

        await m.Publish(new Alert("test"));

        ScenarioExpect.Equal(new[] { "pre", "handler:test", "post" }, log);
    }

#if NET8_0_OR_GREATER
    [Scenario("Stream NoHandler Throws")]
    [Fact]
    public async Task Stream_NoHandler_Throws()
    {
        var m = PatternKit.Behavioral.Mediator.Mediator.Create().Build();

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in m.Stream<Ping, int>(new Ping(1))) { }
        });
    }

    [Scenario("Stream Yields All Items")]
    [Fact]
    public async Task Stream_Yields_All_Items()
    {
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Stream<Ping, int>(static (in p, _) => RangeAsync(p.Value, 5))
            .Build();

        var items = new List<int>();
        await foreach (var x in m.Stream<Ping, int>(new Ping(10)))
            items.Add(x);

        ScenarioExpect.Equal(new[] { 10, 11, 12, 13, 14 }, items);
    }

    private static async IAsyncEnumerable<int> RangeAsync(int start, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return start + i;
        }
    }

    [Scenario("Stream PrePost Behaviors")]
    [Fact]
    public async Task Stream_PrePost_Behaviors()
    {
        var log = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Pre((in _, _) => { log.Add("pre"); return default; })
            .Post((in _, _, _) => { log.Add("post"); return default; })
            .Stream<Ping, int>(static (in p, _) => RangeAsync(p.Value, 2))
            .Build();

        await foreach (var _ in m.Stream<Ping, int>(new Ping(1))) { }

        ScenarioExpect.Contains("pre", log);
        ScenarioExpect.Contains("post", log);
    }

    [Scenario("Stream With Whole Behavior")]
    [Fact]
    public async Task Stream_With_Whole_Behavior()
    {
        var log = new List<string>();
        var m = PatternKit.Behavioral.Mediator.Mediator.Create()
            .Whole((in req, ct, next) =>
            {
                log.Add("whole");
                return next(in req, ct);
            })
            .Stream<Ping, int>(static (in p, _) => RangeAsync(p.Value, 3))
            .Build();

        await foreach (var _ in m.Stream<Ping, int>(new Ping(1))) { }

        ScenarioExpect.Contains("whole", log);
    }
#endif
}

#endregion

#region TaskExtensions and MediatorHelpers Tests

public sealed class TaskExtensionsTests
{
    [Scenario("AsValueTask CompletedTask ReturnsValueTask")]
    [Fact]
    public async Task AsValueTask_CompletedTask_ReturnsValueTask()
    {
        var task = Task.FromResult(42);

        var valueTask = PatternKit.Behavioral.Mediator.TaskExtensions.AsValueTask(task);

        ScenarioExpect.True(valueTask.IsCompletedSuccessfully);
        ScenarioExpect.Equal(42, await valueTask);
    }

    [Scenario("AsValueTask PendingTask ReturnsValueTask")]
    [Fact]
    public async Task AsValueTask_PendingTask_ReturnsValueTask()
    {
        var task = Task.Run(async () =>
        {
            await Task.Delay(1);
            return "result";
        });

        var valueTask = PatternKit.Behavioral.Mediator.TaskExtensions.AsValueTask(task);

        ScenarioExpect.Equal("result", await valueTask);
    }

    [Scenario("AsValueTask FaultedTask PropagatesException")]
    [Fact]
    public async Task AsValueTask_FaultedTask_PropagatesException()
    {
        var task = Task.FromException<int>(new InvalidOperationException("test error"));

        var valueTask = PatternKit.Behavioral.Mediator.TaskExtensions.AsValueTask(task);

        var ex = await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() => valueTask.AsTask());
        ScenarioExpect.Equal("test error", ex.Message);
    }
}

public sealed class MediatorHelpersTests
{
    [Scenario("Box CompletedSuccessfully ReturnsBoxedValue")]
    [Fact]
    public async Task Box_CompletedSuccessfully_ReturnsBoxedValue()
    {
        var valueTask = new ValueTask<int>(42);

        var boxed = PatternKit.Behavioral.Mediator.MediatorHelpers.Box(valueTask);

        ScenarioExpect.True(boxed.IsCompletedSuccessfully);
        var result = await boxed;
        ScenarioExpect.Equal(42, result);
    }

    [Scenario("Box PendingTask AwaitsAndReturns")]
    [Fact]
    public async Task Box_PendingTask_AwaitsAndReturns()
    {
        var source = new TaskCompletionSource<string>();
        var valueTask = new ValueTask<string>(source.Task);

        var boxed = PatternKit.Behavioral.Mediator.MediatorHelpers.Box(valueTask);

        ScenarioExpect.False(boxed.IsCompleted);
        source.SetResult("async result");

        var result = await boxed;
        ScenarioExpect.Equal("async result", result);
    }

    [Scenario("Box NullResult ReturnsNull")]
    [Fact]
    public async Task Box_NullResult_ReturnsNull()
    {
        var valueTask = new ValueTask<string?>((string?)null);

        var boxed = PatternKit.Behavioral.Mediator.MediatorHelpers.Box(valueTask);

        var result = await boxed;
        ScenarioExpect.Null(result);
    }

    [Scenario("Box ReferenceType BoxesCorrectly")]
    [Fact]
    public async Task Box_ReferenceType_BoxesCorrectly()
    {
        var obj = new object();
        var valueTask = new ValueTask<object>(obj);

        var boxed = PatternKit.Behavioral.Mediator.MediatorHelpers.Box(valueTask);

        var result = await boxed;
        ScenarioExpect.Same(obj, result);
    }

    [Scenario("Box AsyncPath BoxesCorrectly")]
    [Fact]
    public async Task Box_AsyncPath_BoxesCorrectly()
    {
        var valueTask = new ValueTask<int>(Task.Run(async () =>
        {
            await Task.Delay(1);
            return 123;
        }));

        var boxed = PatternKit.Behavioral.Mediator.MediatorHelpers.Box(valueTask);

        var result = await boxed;
        ScenarioExpect.Equal(123, result);
    }
}

#endregion
