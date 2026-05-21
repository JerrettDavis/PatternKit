using PatternKit.Messaging;
using ControlBusUnderTest = global::PatternKit.Messaging.ControlBus.ControlBus<PatternKit.Tests.Messaging.ControlBus.ControlBusTests.Command>;
using ControlBusHeaders = global::PatternKit.Messaging.ControlBus.ControlBusHeaders;
using ControlBusResult = global::PatternKit.Messaging.ControlBus.ControlBusResult<PatternKit.Tests.Messaging.ControlBus.ControlBusTests.Command>;
using TinyBDD;

namespace PatternKit.Tests.Messaging.ControlBus;

public sealed class ControlBusTests
{
    [Scenario("Dispatch RoutesNamedControlCommand")]
    [Fact]
    public void Dispatch_RoutesNamedControlCommand()
    {
        var bus = ControlBusUnderTest.Create("fulfillment-control")
            .Handle("pause", "pause-processor", static (_, _) => ControlBusResult.Success())
            .Handle("resume", "resume-processor", static (_, _) => ControlBusResult.Success())
            .Build();

        var result = bus.Dispatch(Message<Command>.Create(new("pause")).WithHeader(ControlBusHeaders.CommandName, "pause"));

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("fulfillment-control", result.BusName);
        ScenarioExpect.Equal("pause", result.CommandName);
        ScenarioExpect.Equal("pause-processor", result.HandlerName);
    }

    [Scenario("Dispatch UsesCustomCommandSelector")]
    [Fact]
    public void Dispatch_UsesCustomCommandSelector()
    {
        var bus = ControlBusUnderTest.Create()
            .SelectCommand(static (message, _) => message.Payload.Name)
            .Handle("drain", "drain-processor", static (_, _) => ControlBusResult.Success())
            .Build();

        var result = bus.Dispatch(Message<Command>.Create(new("drain")));

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("drain", result.CommandName);
    }

    [Scenario("Dispatch RejectsUnknownOrBlankCommands")]
    [Fact]
    public void Dispatch_RejectsUnknownOrBlankCommands()
    {
        var bus = ControlBusUnderTest.Create()
            .Handle("pause", "pause-processor", static (_, _) => ControlBusResult.Success())
            .Build();

        var blank = bus.Dispatch(Message<Command>.Create(new("pause")));
        var unknown = bus.Dispatch(Message<Command>.Create(new("resume")).WithHeader(ControlBusHeaders.CommandName, "resume"));

        ScenarioExpect.False(blank.Succeeded);
        ScenarioExpect.Equal("Control command name was not supplied.", blank.RejectionReason);
        ScenarioExpect.False(unknown.Succeeded);
        ScenarioExpect.Equal("No control bus handler is registered for the command.", unknown.RejectionReason);
    }

    [Scenario("DispatchPreservesHandlerFailure")]
    [Fact]
    public void Dispatch_PreservesHandlerFailure()
    {
        var bus = ControlBusUnderTest.Create()
            .SelectCommand(static (message, _) => message.Payload.Name)
            .Handle("pause", "pause-processor", static (_, _) => ControlBusResult.Failure("Processor is already paused."))
            .Build();

        var result = bus.Dispatch(Message<Command>.Create(new("pause")));

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.Equal("pause-processor", result.HandlerName);
        ScenarioExpect.Equal("Processor is already paused.", result.RejectionReason);
    }

    [Scenario("BuilderRejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => ControlBusUnderTest.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => ControlBusUnderTest.Create().SelectCommand(null!));
        ScenarioExpect.Throws<ArgumentException>(() => ControlBusUnderTest.Create().Handle("", "handler", static (_, _) => ControlBusResult.Success()));
        ScenarioExpect.Throws<ArgumentException>(() => ControlBusUnderTest.Create().Handle("pause", "", static (_, _) => ControlBusResult.Success()));
        ScenarioExpect.Throws<ArgumentNullException>(() => ControlBusUnderTest.Create().Handle("pause", "handler", null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => ControlBusUnderTest.Create().Build());
        ScenarioExpect.Throws<InvalidOperationException>(() => ControlBusUnderTest.Create()
            .Handle("pause", "one", static (_, _) => ControlBusResult.Success())
            .Handle("pause", "two", static (_, _) => ControlBusResult.Success()));
        ScenarioExpect.Throws<ArgumentException>(() => ControlBusResult.Failure(""));
    }

    [Scenario("DispatchRejectsNullMessage")]
    [Fact]
    public void Dispatch_RejectsNullMessage()
    {
        var bus = ControlBusUnderTest.Create()
            .Handle("pause", "pause-processor", static (_, _) => ControlBusResult.Success())
            .Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => bus.Dispatch(null!));
    }

    public sealed record Command(string Name);
}
