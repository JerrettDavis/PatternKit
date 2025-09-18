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
        for (int i = 0; i < count; i++) { await Task.Yield(); yield return start + i; }
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
        .When("sending Ping(5)", async Task<(Pong r, List<string> log)> (t) => { var (m, log) = t; var r = await m.Send<Ping, Pong>(new Ping(5)); return (r, log); })
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
