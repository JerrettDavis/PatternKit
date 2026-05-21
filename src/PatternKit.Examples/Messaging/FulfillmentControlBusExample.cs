using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.ControlBus;

namespace PatternKit.Examples.Messaging;

/// <summary>Operational command sent to the fulfillment control bus.</summary>
public sealed record FulfillmentControlCommand(string CommandName, string ProcessorId);

/// <summary>Summary returned after dispatching a fulfillment control command.</summary>
public sealed record FulfillmentControlSummary(
    bool Succeeded,
    string CommandName,
    string? HandlerName,
    string? RejectionReason,
    bool Paused,
    bool Draining);

/// <summary>Container-owned fulfillment processor state used by the control bus handlers.</summary>
public sealed class FulfillmentProcessorControlState
{
    private readonly object _gate = new();
    private bool _paused;
    private bool _draining;

    public bool Paused
    {
        get
        {
            lock (_gate)
                return _paused;
        }
    }

    public bool Draining
    {
        get
        {
            lock (_gate)
                return _draining;
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            _paused = true;
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            _paused = false;
            _draining = false;
        }
    }

    public void Drain()
    {
        lock (_gate)
        {
            _draining = true;
        }
    }
}

/// <summary>Service that dispatches operational commands through an injected control bus.</summary>
public sealed class FulfillmentControlBusService(
    ControlBus<FulfillmentControlCommand> bus,
    FulfillmentProcessorControlState state)
{
    public FulfillmentControlSummary Execute(FulfillmentControlCommand command)
    {
        var message = Message<FulfillmentControlCommand>.Create(command)
            .WithHeader(ControlBusHeaders.CommandName, command.CommandName)
            .WithCorrelationId(command.ProcessorId);
        var result = bus.Dispatch(message);
        return new(
            result.Succeeded,
            result.CommandName,
            result.HandlerName,
            result.RejectionReason,
            state.Paused,
            state.Draining);
    }
}

/// <summary>Fluent control-bus builder used by applications that do not enable generators.</summary>
public static class FulfillmentControlBuses
{
    public static ControlBus<FulfillmentControlCommand> Create(FulfillmentProcessorControlState state)
    {
        if (state is null)
            throw new ArgumentNullException(nameof(state));

        return ControlBus<FulfillmentControlCommand>.Create("fulfillment-control")
            .Handle("pause", "pause-processor", (_, _) =>
            {
                state.Pause();
                return ControlBusResult<FulfillmentControlCommand>.Success();
            })
            .Handle("resume", "resume-processor", (_, _) =>
            {
                state.Resume();
                return ControlBusResult<FulfillmentControlCommand>.Success();
            })
            .Handle("drain", "drain-processor", (_, _) =>
            {
                state.Drain();
                return ControlBusResult<FulfillmentControlCommand>.Success();
            })
            .Build();
    }
}

/// <summary>Bridge from generated static handlers to container-owned fulfillment state.</summary>
public static class FulfillmentProcessorControlRegistry
{
    public static FulfillmentProcessorControlState Current { get; set; } = new();
}

/// <summary>Source-generated control bus for fulfillment operations.</summary>
[GenerateControlBus(typeof(FulfillmentControlCommand), FactoryName = "Create", BusName = "fulfillment-control")]
public static partial class GeneratedFulfillmentControlBus
{
    [ControlBusCommand("pause", "pause-processor", 10)]
    private static ControlBusResult<FulfillmentControlCommand> Pause(Message<FulfillmentControlCommand> message, MessageContext context)
    {
        FulfillmentProcessorControlRegistry.Current.Pause();
        return ControlBusResult<FulfillmentControlCommand>.Success();
    }

    [ControlBusCommand("resume", "resume-processor", 20)]
    private static ControlBusResult<FulfillmentControlCommand> Resume(Message<FulfillmentControlCommand> message, MessageContext context)
    {
        FulfillmentProcessorControlRegistry.Current.Resume();
        return ControlBusResult<FulfillmentControlCommand>.Success();
    }

    [ControlBusCommand("drain", "drain-processor", 30)]
    private static ControlBusResult<FulfillmentControlCommand> Drain(Message<FulfillmentControlCommand> message, MessageContext context)
    {
        FulfillmentProcessorControlRegistry.Current.Drain();
        return ControlBusResult<FulfillmentControlCommand>.Success();
    }
}

/// <summary>Runner that demonstrates both fluent and generated control-bus paths.</summary>
public sealed class FulfillmentControlBusExampleRunner(FulfillmentControlBusService service)
{
    public FulfillmentControlSummary RunGenerated(FulfillmentControlCommand command)
        => service.Execute(command);

    public static FulfillmentControlSummary RunFluent(FulfillmentControlCommand command)
    {
        var state = new FulfillmentProcessorControlState();
        var bus = FulfillmentControlBuses.Create(state);
        var result = bus.Dispatch(Message<FulfillmentControlCommand>.Create(command)
            .WithHeader(ControlBusHeaders.CommandName, command.CommandName)
            .WithCorrelationId(command.ProcessorId));
        return new(result.Succeeded, result.CommandName, result.HandlerName, result.RejectionReason, state.Paused, state.Draining);
    }
}

/// <summary>DI helpers for importing the fulfillment control-bus example into standard .NET containers.</summary>
public static class FulfillmentControlBusExampleServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentControlBusDemo(this IServiceCollection services)
    {
        services.AddSingleton<FulfillmentProcessorControlState>();
        services.AddSingleton(sp =>
        {
            var state = sp.GetRequiredService<FulfillmentProcessorControlState>();
            FulfillmentProcessorControlRegistry.Current = state;
            return GeneratedFulfillmentControlBus.Create();
        });
        services.AddSingleton<FulfillmentControlBusService>();
        services.AddSingleton<FulfillmentControlBusExampleRunner>();
        return services;
    }
}
