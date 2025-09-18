using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.MediatorDemo;
using TinyBDD;
using TinyBDD.Assertions;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.MediatorDemo;

[Feature("Mediator demo: DI scanning, send/publish/stream, pipeline behaviors (MediatR parity)")]
public sealed class MediatorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Helper types for negative-path tests
    private sealed record UnseenNotification(string Name) : INotification;
    
    private sealed record Unknown(int X) : ICommand<int>;
#if NET8_0_OR_GREATER
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
            .Then("results match", t => t.r1 == "pong:7" && t.r2 == "hi" && t.r3 == 5)
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
                return Record.Exception(() => { var _ = m.Stream(new UnseenStream(1)); });
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
}
