using System;

namespace PatternKit.Generators.Messaging;

/// <summary>Generates a typed control bus factory for a partial class or struct.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateControlBusAttribute : Attribute
{
    /// <summary>Creates a control bus generator attribute.</summary>
    public GenerateControlBusAttribute(Type commandType)
        => CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));

    /// <summary>Command type dispatched by the generated control bus.</summary>
    public Type CommandType { get; }

    /// <summary>Name of the generated factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Name assigned to the generated control bus.</summary>
    public string BusName { get; set; } = "control-bus";
}

/// <summary>Marks a static method as a generated control bus command handler.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ControlBusCommandAttribute : Attribute
{
    /// <summary>Creates a control bus command attribute.</summary>
    public ControlBusCommandAttribute(string commandName, string handlerName, int order = 0)
    {
        if (string.IsNullOrWhiteSpace(commandName))
            throw new ArgumentException("Control command name cannot be null, empty, or whitespace.", nameof(commandName));
        if (string.IsNullOrWhiteSpace(handlerName))
            throw new ArgumentException("Control handler name cannot be null, empty, or whitespace.", nameof(handlerName));

        CommandName = commandName;
        HandlerName = handlerName;
        Order = order;
    }

    /// <summary>Operational command name.</summary>
    public string CommandName { get; }

    /// <summary>Handler name used in result metadata.</summary>
    public string HandlerName { get; }

    /// <summary>Registration order in generated source.</summary>
    public int Order { get; }
}
