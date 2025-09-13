using PatternKit.Examples.ApiGateway;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ApiGateway;

[Feature("ApiGateway Demo.Run prints expected responses and middleware output")]
public sealed class DemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Demo.Run end-to-end console output")]
    [Fact]
    public async Task Demo_Run_Prints_Expected_Lines()
    {
        await Given("a function that runs Demo.Run and captures console",
                () => (Func<string>)(() => CaptureConsole(Demo.Run)))
            .When("executing the demo",
                run => run())
            .Then("prints 200 text/plain OK for /health",
                text => text.Contains("200 text/plain") && text.Contains("OK"))
            .And("prints 200 application/json with user payload for /users/42",
                text => text.Contains("200 application/json") &&
                        text.Contains("\"id\":42") &&
                        text.Contains("\"name\":\"user42\""))
            .And("prints 404 text/plain 'User not found' for /users/abc",
                text => text.Contains("404 text/plain") &&
                        text.Contains("User not found"))
            .And("logs 'Denied: missing Authorization' before the 401 for /admin/metrics",
                text =>
                {
                    var denied = text.IndexOf("Denied: missing Authorization", StringComparison.Ordinal);
                    var unauthorized = text.IndexOf("401 text/plain", StringComparison.Ordinal);
                    return denied >= 0 && unauthorized >= 0 && denied < unauthorized;
                })
            .And("prints 201 application/json for POST /users",
                text => text.Contains("201 application/json") &&
                        text.Contains("\"ok\":true"))
            .And("prints 404 text/plain Not Found for /nope",
                text => text.Contains("404 text/plain") &&
                        text.Contains("Not Found"))
            .And("does not print a reqid line since X-Request-Id header wasn't set",
                text => !text.Contains("reqid="))
            .AssertPassed();
    }

    // --- helper to capture console output for the duration of an action ---
    private static string CaptureConsole(Action act)
    {
        var sw = new StringWriter();
        var prev = Console.Out;
        try
        {
            Console.SetOut(sw);
            act();
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(prev);
        }
    }
}