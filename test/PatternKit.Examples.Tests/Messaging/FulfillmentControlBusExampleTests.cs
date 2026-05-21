using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.ControlBus;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class FulfillmentControlBusExampleTests
{
    [Scenario("FluentControlBus DispatchesOperationalCommands")]
    [Fact]
    public void FluentControlBus_DispatchesOperationalCommands()
    {
        var pause = FulfillmentControlBusExampleRunner.RunFluent(new("pause", "processor-1"));
        var drain = FulfillmentControlBusExampleRunner.RunFluent(new("drain", "processor-1"));

        ScenarioExpect.True(pause.Succeeded);
        ScenarioExpect.Equal("pause-processor", pause.HandlerName);
        ScenarioExpect.True(pause.Paused);
        ScenarioExpect.True(drain.Succeeded);
        ScenarioExpect.True(drain.Draining);
    }

    [Scenario("GeneratedControlBus MatchesFluentCommandHandling")]
    [Fact]
    public void GeneratedControlBus_MatchesFluentCommandHandling()
    {
        var state = new FulfillmentProcessorControlState();
        FulfillmentProcessorControlRegistry.Current = state;
        var bus = GeneratedFulfillmentControlBus.Create();

        var result = bus.Dispatch(Message<FulfillmentControlCommand>.Create(new("resume", "processor-1"))
            .WithHeader(ControlBusHeaders.CommandName, "resume"));

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("fulfillment-control", result.BusName);
        ScenarioExpect.Equal("resume", result.CommandName);
        ScenarioExpect.Equal("resume-processor", result.HandlerName);
        ScenarioExpect.False(state.Paused);
        ScenarioExpect.False(state.Draining);
    }

    [Scenario("ServiceCollection ImportsControlBusExample")]
    [Fact]
    public void ServiceCollection_ImportsControlBusExample()
    {
        var services = new ServiceCollection();
        services.AddFulfillmentControlBusDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var bus = provider.GetRequiredService<ControlBus<FulfillmentControlCommand>>();
        var runner = provider.GetRequiredService<FulfillmentControlBusExampleRunner>();

        var direct = bus.Dispatch(Message<FulfillmentControlCommand>.Create(new("pause", "processor-1"))
            .WithHeader(ControlBusHeaders.CommandName, "pause"));
        var summary = runner.RunGenerated(new("drain", "processor-1"));

        ScenarioExpect.True(direct.Succeeded);
        ScenarioExpect.True(summary.Succeeded);
        ScenarioExpect.True(summary.Paused);
        ScenarioExpect.True(summary.Draining);
    }

    [Scenario("AggregateServiceCollection ImportsControlBusExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsControlBusExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<FulfillmentControlBusExampleService>();

        var summary = example.Service.Execute(new("pause", "processor-1"));

        ScenarioExpect.True(summary.Succeeded);
        ScenarioExpect.True(summary.Paused);
        ScenarioExpect.Equal("pause-processor", summary.HandlerName);
    }
}
