namespace PatternKit.Generators.ServiceLayer;

/// <summary>Generates a Service Layer operation factory from attributed handler and rule methods.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateServiceLayerOperationAttribute : Attribute
{
    public GenerateServiceLayerOperationAttribute(Type requestType, Type responseType)
    {
        RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
        ResponseType = responseType ?? throw new ArgumentNullException(nameof(responseType));
    }

    public Type RequestType { get; }

    public Type ResponseType { get; }

    public string FactoryName { get; set; } = "Create";

    public string OperationName { get; set; } = "";
}

/// <summary>Marks the handler method for a generated Service Layer operation.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ServiceLayerHandlerAttribute : Attribute;

/// <summary>Marks a precondition rule for a generated Service Layer operation.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ServiceLayerRuleAttribute : Attribute
{
    public ServiceLayerRuleAttribute(string code, string message, int order)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Service Layer rule code is required.", nameof(code))
            : code;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Service Layer rule message is required.", nameof(message))
            : message;
        Order = order;
    }

    public string Code { get; }

    public string Message { get; }

    public int Order { get; }
}
