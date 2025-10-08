using System;
using System.IO;
using System.Linq;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ProxyDemo;

[Feature("Examples - Proxy Pattern Demonstrations (Parameterless Methods)")]
[Collection("ConsoleOutput")] // prevent parallel Console.Out redirection conflicts
public sealed class ProxyDemoParameterlessTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private (bool success, string output) CaptureConsole(Action action)
    {
        var original = Console.Out;
        using var sw = new StringWriter();
        try
        {
            Console.SetOut(sw);
            action();
            Console.Out.Flush();
            return (true, sw.ToString());
        }
        catch
        {
            return (false, sw.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Scenario("Parameterless DemonstrateVirtualProxy executes and writes header")]
    [Fact]
    public Task DemonstrateVirtualProxy_NoWriter_Executes()
        => Given("parameterless virtual proxy demo", () => true)
            .When("executing demo", _ => CaptureConsole(() => PatternKit.Examples.ProxyDemo.ProxyDemo.DemonstrateVirtualProxy()))
            .Then("executes successfully", r => r.success)
            .And("writes virtual proxy header", r => r.output.Contains("Virtual Proxy - Lazy Initialization"))
            .AssertPassed();

    [Scenario("Parameterless DemonstrateProtectionProxy executes and writes header")]
    [Fact]
    public Task DemonstrateProtectionProxy_NoWriter_Executes()
        => Given("parameterless protection proxy demo", () => true)
            .When("executing demo", _ => CaptureConsole(() => PatternKit.Examples.ProxyDemo.ProxyDemo.DemonstrateProtectionProxy()))
            .Then("executes successfully", r => r.success)
            .And("writes protection proxy header", r => r.output.Contains("Protection Proxy - Access Control"))
            .AssertPassed();

    [Scenario("Parameterless DemonstrateCachingProxy executes and writes header")]
    [Fact]
    public Task DemonstrateCachingProxy_NoWriter_Executes()
        => Given("parameterless caching proxy demo", () => true)
            .When("executing demo", _ => CaptureConsole(() => PatternKit.Examples.ProxyDemo.ProxyDemo.DemonstrateCachingProxy()))
            .Then("executes successfully", r => r.success)
            .And("writes caching proxy header", r => r.output.Contains("Caching Proxy - Result Memoization"))
            .AssertPassed();

    [Scenario("Parameterless DemonstrateLoggingProxy executes and writes header")]
    [Fact]
    public Task DemonstrateLoggingProxy_NoWriter_Executes()
        => Given("parameterless logging proxy demo", () => true)
            .When("executing demo", _ => CaptureConsole(() => PatternKit.Examples.ProxyDemo.ProxyDemo.DemonstrateLoggingProxy()))
            .Then("executes successfully", r => r.success)
            .And("writes logging proxy header", r => r.output.Contains("Logging Proxy - Invocation Tracking"))
            .AssertPassed();

    [Scenario("Parameterless DemonstrateCustomInterception executes and writes header")]
    [Fact]
    public Task DemonstrateCustomInterception_NoWriter_Executes()
        => Given("parameterless custom interception demo", () => true)
            .When("executing demo", _ => CaptureConsole(() => PatternKit.Examples.ProxyDemo.ProxyDemo.DemonstrateCustomInterception()))
            .Then("executes successfully", r => r.success)
            .And("writes custom interception header", r => r.output.Contains("Custom Interception - Retry Logic"))
            .AssertPassed();

    [Scenario("Parameterless DemonstrateMockFramework executes and writes header")]
    [Fact]
    public Task DemonstrateMockFramework_NoWriter_Executes()
        => Given("parameterless mock framework demo", () => true)
            .When("executing demo", _ => CaptureConsole(() => PatternKit.Examples.ProxyDemo.ProxyDemo.DemonstrateMockFramework()))
            .Then("executes successfully", r => r.success)
            .And("writes mock framework header", r => r.output.Contains("Mock Framework - Test Doubles"))
            .AssertPassed();

    [Scenario("Parameterless DemonstrateRemoteProxy executes and writes header")]
    [Fact]
    public Task DemonstrateRemoteProxy_NoWriter_Executes()
        => Given("parameterless remote proxy demo", () => true)
            .When("executing demo", _ => CaptureConsole(() => PatternKit.Examples.ProxyDemo.ProxyDemo.DemonstrateRemoteProxy()))
            .Then("executes successfully", r => r.success)
            .And("writes remote proxy header", r => r.output.Contains("Remote Proxy - Network Call Optimization"))
            .AssertPassed();

    [Scenario("Parameterless RunAllDemos executes all demos and writes all headers")]
    [Fact]
    public Task RunAllDemos_NoWriter_Executes_All()
        => Given("parameterless run all demos", () => true)
            .When("executing all demos", _ => CaptureConsole(() => PatternKit.Examples.ProxyDemo.ProxyDemo.RunAllDemos()))
            .Then("executes successfully", r => r.success)
            .And("includes all demo headers", r =>
                new []
                {
                    "Virtual Proxy - Lazy Initialization",
                    "Protection Proxy - Access Control",
                    "Caching Proxy - Result Memoization",
                    "Logging Proxy - Invocation Tracking",
                    "Custom Interception - Retry Logic",
                    "Mock Framework - Test Doubles",
                    "Remote Proxy - Network Call Optimization"
                }.All(h => r.output.Contains(h)))
            .AssertPassed();
}

