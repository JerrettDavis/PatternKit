using PatternKit.Examples.AsyncStateDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.AsyncStateDemo;

public sealed class AsyncStateDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Connect flow: connect -> ok => enters Connected with ordered hooks/effects")]
    [Fact]
    public async Task Connect_Flow()
    {
        await Given("async connection demo", () => default(object?))
            .When<(ConnectionStateDemo.Mode Final, List<string> Log)>(
                "run connect, ok",
                (Func<object?, Task<(ConnectionStateDemo.Mode, List<string>)>>)(_ => ConnectionStateDemo.RunAsync("connect", "ok").AsTask())
            )
            .Then("final Connected and logs show exit/eff/enter handover", r =>
                r.Final == ConnectionStateDemo.Mode.Connected &&
                string.Join(',', r.Log.ToArray()) == string.Join(',', new[]{
                    "exit:Disconnected","effect:dial","enter:Connecting",
                    "exit:Connecting","effect:handshake","enter:Connected"
                }))
            .AssertPassed();
    }

    [Scenario("Connected default stay only runs effect:noop")]
    [Fact]
    public async Task Default_Stay_NoOp()
    {
        await Given<(ConnectionStateDemo.Mode Final, List<string> Log)>(
                "connected",
                (Func<Task<(ConnectionStateDemo.Mode, List<string>)>>)(() => ConnectionStateDemo.RunAsync("connect","ok").AsTask())
            )
            .When<(ConnectionStateDemo.Mode Final, List<string> Log)>(
                "unknown event in Connected",
                (Func<(ConnectionStateDemo.Mode, List<string>), Task<(ConnectionStateDemo.Mode, List<string>)>>)(
                    _ => ConnectionStateDemo.RunAsync("connect","ok","ignore").AsTask()
                )
            )
            .Then("still Connected and last step noop", r => r.Final == ConnectionStateDemo.Mode.Connected && r.Log.Last() == "effect:noop")
            .AssertPassed();
    }
}
