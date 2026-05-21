namespace PatternKit.Generators.TransactionScript;

/// <summary>Generates a Transaction Script factory from attributed handler and validator methods.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateTransactionScriptAttribute : Attribute
{
    public GenerateTransactionScriptAttribute(Type requestType, Type responseType)
    {
        RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
        ResponseType = responseType ?? throw new ArgumentNullException(nameof(responseType));
    }

    public Type RequestType { get; }

    public Type ResponseType { get; }

    public string FactoryName { get; set; } = "Create";

    public string ScriptName { get; set; } = "";
}

/// <summary>Marks the handler method for a generated Transaction Script.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class TransactionScriptHandlerAttribute : Attribute;

/// <summary>Marks the optional validator method for a generated Transaction Script.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class TransactionScriptValidatorAttribute : Attribute;
