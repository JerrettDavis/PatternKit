using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates typed factory methods for a saga/process manager class.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateSagaAttribute : Attribute
{
    /// <summary>Creates a saga generator attribute.</summary>
    public GenerateSagaAttribute(Type stateType)
    {
        StateType = stateType ?? throw new ArgumentNullException(nameof(stateType));
    }

    /// <summary>Saga state type processed by generated factories.</summary>
    public Type StateType { get; }

    /// <summary>Name of the generated sync factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Name of the generated async factory method.</summary>
    public string AsyncFactoryName { get; set; } = "CreateAsync";
}

/// <summary>
/// Marks a static method as a generated saga step.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SagaStepAttribute : Attribute
{
    /// <summary>Creates a saga step attribute.</summary>
    public SagaStepAttribute(Type messageType, int order)
    {
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
        Order = order;
    }

    /// <summary>Message payload type handled by this step.</summary>
    public Type MessageType { get; }

    /// <summary>Step order in the generated saga builder.</summary>
    public int Order { get; }
}

/// <summary>
/// Marks a static method as the generated saga completion predicate.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SagaCompleteWhenAttribute : Attribute;
