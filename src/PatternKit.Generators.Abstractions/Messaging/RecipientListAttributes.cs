using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates typed factory methods for a recipient-list class.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateRecipientListAttribute : Attribute
{
    /// <summary>Creates a recipient-list generator attribute.</summary>
    public GenerateRecipientListAttribute(Type payloadType)
    {
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
    }

    /// <summary>Message payload type dispatched by generated recipient lists.</summary>
    public Type PayloadType { get; }

    /// <summary>Name of the generated sync factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Name of the generated async factory method.</summary>
    public string AsyncFactoryName { get; set; } = "CreateAsync";
}

/// <summary>
/// Marks a static method as a generated recipient-list handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RecipientListRecipientAttribute : Attribute
{
    /// <summary>Creates a recipient-list recipient attribute.</summary>
    public RecipientListRecipientAttribute(string name, int order, string predicateMethodName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Recipient name cannot be null, empty, or whitespace.", nameof(name));

        if (string.IsNullOrWhiteSpace(predicateMethodName))
            throw new ArgumentException("Recipient predicate method name cannot be null, empty, or whitespace.", nameof(predicateMethodName));

        Name = name;
        Order = order;
        PredicateMethodName = predicateMethodName;
    }

    /// <summary>Recipient name returned in delivered recipient results.</summary>
    public string Name { get; }

    /// <summary>Recipient order in the generated recipient list.</summary>
    public int Order { get; }

    /// <summary>Name of the static predicate method used by this recipient.</summary>
    public string PredicateMethodName { get; }
}
