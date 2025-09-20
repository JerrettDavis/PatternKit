using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Behavioral.Mediator;
using PatternKit.Examples.MediatorDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.MediatorDemo;

[Feature("Mediator demo: DI scanning, send/publish/stream, pipeline behaviors (MediatR parity)")]
public sealed class MediatorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Ping(int Value);

    private sealed record Pong([UsedImplicitly] string Value);

    private sealed record UnseenNotification([UsedImplicitly] string Name) : INotification;

    [UsedImplicitly]
    private sealed record Unknown(int X) : ICommand<int>;
#if NET8_0_OR_GREATER
    [UsedImplicitly]
    private sealed record UnseenStream(int N) : IStreamRequest<int>;

#endif

    private static (ServiceProvider sp, IAppMediator med, IMediatorDemoSink sink) Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMediatorDemoSink, MediatorDemoSink>();
        services.AddPatternKitMediator(typeof(PingHandler).Assembly);
        var sp = services.BuildServiceProvider();
        return (sp, sp.GetRequiredService<IAppMediator>(), sp.GetRequiredService<IMediatorDemoSink>());
    }

    [Scenario("Send executes handlers and logs behavior before/after")]
    [Fact]
    public Task Send_Executes_And_Logs()
        => Given("a built mediator", Build)
            .When("sending Ping and Echo and Sum", async Task<(string r1, string r2, int r3, List<string> Log)> (t) =>
            {
                var (_, m, sink) = t;
                var r1 = await m.Send(new PingCmd(7));
                var r2 = await m.Send(new EchoCmd("hi"));
                var r3 = await m.Send(new SumCmd(2, 3));
                return (r1, r2, r3, sink.Log);
            })
            .Then("results match", t => t is { r1: "pong:7", r2: "hi", r3: 5 })
            .And("log includes before/after entries for each command", t =>
            {
                var log = string.Join('|', t.Log);
                return log.Contains("before:PingCmd") && log.Contains("after:PingCmd:pong:7") &&
                       log.Contains("before:EchoCmd") && log.Contains("after:EchoCmd:hi") &&
                       log.Contains("before:SumCmd") && log.Contains("after:SumCmd:5");
            })
            .AssertPassed();

    [Scenario("Publish fans out to all notification handlers (no behaviors applied)")]
    [Fact]
    public Task Publish_Fanout()
        => Given("a built mediator", Build)
            .When("publishing UserCreated", async Task<List<string>> (t) =>
            {
                var (_, m, sink) = t;
                await m.Publish(new UserCreated("Ada"));
                return sink.Log;
            })
            .Then("log contains email and audit entries", log =>
            {
                var s = string.Join('|', log);
                return s.Contains("email:welcome:Ada") && s.Contains("audit:user-created:Ada");
            })
            .And("no command behaviors ran for notifications", log => !log.Any(l => l.StartsWith("before:") || l.StartsWith("after:")))
            .AssertPassed();

#if NET8_0_OR_GREATER
    [Scenario("Stream yields items from handler")]
    [Fact]
    public Task Stream_Yields_Items()
        => Given("a built mediator", Build)
            .When("streaming CountUp", async Task<List<int>> (t) =>
            {
                var (_, m, _) = t;
                var list = new List<int>();
                await foreach (var i in m.Stream(new CountUpCmd(3, 4))) list.Add(i);
                return list;
            })
            .Then("collected 3,4,5,6", xs => string.Join(',', xs) == "3,4,5,6")
            .AssertPassed();
#endif

    [Scenario("Publish with no handlers is a no-op (no throw)")]
    [Fact]
    public Task Publish_NoHandlers_NoThrow()
        => Given("a built mediator", Build)
            .When("publishing a notification with no handlers", async Task<List<string>> (t) =>
            {
                var (_, m, sink) = t;
                await m.Publish(new UnseenNotification("nobody"));
                return sink.Log; // unchanged
            })
            .Then("log remains unchanged (no entries)", log => log.Count == 0)
            .AssertPassed();

    [Scenario("Send on unregistered command throws InvalidOperationException")]
    [Fact]
    public Task Send_Missing_Handler_Throws()
        => Given("a built mediator", Build)
            .When("sending unregistered command", t =>
            {
                var (_, m, _) = t;
                return Record.ExceptionAsync(() => m.Send(new Unknown(1)).AsTask());
            })
            .Then("InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

#if NET8_0_OR_GREATER
    [Scenario("Stream on unregistered request throws InvalidOperationException")]
    [Fact]
    public Task Stream_Missing_Handler_Throws()
        => Given("a built mediator", Build)
            .When("streaming unregistered request", t =>
            {
                var (_, m, _) = t;
                return Record.Exception(() =>
                {
                    var _ = m.Stream(new UnseenStream(1));
                });
            })
            .Then("InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();
#endif

    [Scenario("Closed-generic behavior for SumCmd runs in addition to open-generic behavior")]
    [Fact]
    public Task Closed_Generic_Behavior_Applies()
        => Given("a built mediator", Build)
            .When("sending SumCmd", async Task<List<string>> (t) =>
            {
                var (_, m, sink) = t;
                _ = await m.Send(new SumCmd(2, 3));
                return sink.Log;
            })
            .Then("both open-generic and closed-generic behavior messages exist", log =>
            {
                var s = string.Join('|', log);
                return s.Contains("before:SumCmd") && s.Contains("after:SumCmd:5") &&
                       s.Contains("sum:before") && s.Contains("sum:after:5");
            })
            .AssertPassed();

    [Fact]
    public Task LoggingBehavior_Runs_Before_And_After()
        => Given("a mediator with logging whole behavior", () =>
            {
                var log = new List<string>();
                var m = Mediator.Create()
                    .Whole((in req, ct, next) =>
                    {
                        log.Add("before");
                        var vt = next(in req, ct);
                        // This is GROSS
                        return new ValueTask<object?>(vt.AsTask().ContinueWith(t =>
                        {
                            log.Add("after");
                            return t.Result;
                        }, ct));
                    })
                    .Command<Ping, Pong>((in p, _) => new ValueTask<Pong>(new Pong($"pong:{p.Value}")))
                    .Build();
                return (m, log);
            })
            .When("sending Ping", async Task<(Pong, List<string>)> (t) =>
            {
                var (m, log) = t;
                var r = await m.Send<Ping, Pong>(new Ping(1));
                return (r, log);
            })
            .Then("log contains before and after", t => t.Item2.Contains("before") && t.Item2.Contains("after"))
            .AssertPassed();

    [Fact]
    public Task ExceptionBehavior_Catches_And_Logs()
        => Given("a mediator with exception-catching whole behavior", () =>
            {
                var log = new List<string>();
                var m = Mediator.Create()
                    .Whole((in req, ct, next) =>
                    {
                        try
                        {
                            return next(in req, ct);
                        }
                        catch (Exception ex)
                        {
                            log.Add($"caught:{ex.Message}");
                            throw;
                        }
                    })
                    .Command<Ping, Pong>((in _, _) => throw new InvalidOperationException("fail"))
                    .Build();
                return (m, log);
            })
            .When("sending Ping", async Task<List<string>> (t) =>
            {
                var (m, log) = t;
                try
                {
                    await m.Send<Ping, Pong>(new Ping(1));
                }
                catch
                {
                    // ignored
                }

                return log;
            })
            .Then("log contains caught", log => log.Exists(l => l.StartsWith("caught:")))
            .AssertPassed();

    [Fact]
    public Task ValidationBehavior_ShortCircuits_On_Invalid()
        => Given("a mediator with validation pre behavior", () =>
            {
                var m = Mediator.Create()
                    .Pre((in req, _) =>
                    {
                        if (req is Ping { Value: < 0 })
                            throw new ArgumentException("Negative not allowed");
                        return default;
                    })
                    .Command<Ping, Pong>((in p, _) => new ValueTask<Pong>(new Pong($"pong:{p.Value}")))
                    .Build();
                return m;
            })
            .When("sending Ping(-1)", async Task<Exception> (m) =>
            {
                try
                {
                    await m.Send<Ping, Pong>(new Ping(-1));
                }
                catch (Exception ex)
                {
                    return ex;
                }

                return null!;
            })
            .Then("throws ArgumentException", ex => ex is ArgumentException)
            .AssertPassed();

    [Fact]
    public Task MultipleBehaviors_Compose_In_Order()
        => Given("a mediator with multiple behaviors", () =>
            {
                var log = new List<string>();
                var m = Mediator.Create()
                    .Pre((in _, _) =>
                    {
                        log.Add("pre");
                        return default;
                    })
                    .Whole((in req, ct, next) =>
                    {
                        log.Add("whole1:before");
                        var vt = next(in req, ct);
                        log.Add("whole1:after");
                        return vt;
                    })
                    .Whole((in req, ct, next) =>
                    {
                        log.Add("whole2:before");
                        var vt = next(in req, ct);
                        log.Add("whole2:after");
                        return vt;
                    })
                    .Post((in _, _, _) =>
                    {
                        log.Add("post");
                        return default;
                    })
                    .Command<Ping, Pong>((in p, _) => new ValueTask<Pong>(new Pong($"pong:{p.Value}")))
                    .Build();
                return (m, log);
            })
            .When("sending Ping", async Task<List<string>> (t) =>
            {
                var (m, log) = t;
                await m.Send<Ping, Pong>(new Ping(2));
                return log;
            })
            .Then("log shows correct order", log =>
                string.Join("|", log) == "pre|whole1:before|whole2:before|whole2:after|whole1:after|post")
            .AssertPassed();
}