namespace PatternKit.Generators.PortsAndAdapters;

/// <summary>Generates a Ports and Adapters pipeline factory from attributed adapter and application port methods.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GeneratePortsAndAdaptersAttribute : Attribute
{
    public GeneratePortsAndAdaptersAttribute(Type inboundType, Type commandType, Type resultType, Type outboundType)
    {
        InboundType = inboundType ?? throw new ArgumentNullException(nameof(inboundType));
        CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
        ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
        OutboundType = outboundType ?? throw new ArgumentNullException(nameof(outboundType));
    }

    public Type InboundType { get; }
    public Type CommandType { get; }
    public Type ResultType { get; }
    public Type OutboundType { get; }
    public string FactoryName { get; set; } = "Create";
    public string PipelineName { get; set; } = "ports-and-adapters";
}

/// <summary>Marks the inbound adapter method for a generated Ports and Adapters pipeline.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class InboundAdapterAttribute : Attribute;

/// <summary>Marks the application port handler for a generated Ports and Adapters pipeline.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ApplicationPortAttribute : Attribute;

/// <summary>Marks the outbound adapter method for a generated Ports and Adapters pipeline.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OutboundAdapterAttribute : Attribute;
