using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMessagingGatewayAttribute : Attribute
{
    public GenerateMessagingGatewayAttribute(Type requestType, Type responseType)
    {
        RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
        ResponseType = responseType ?? throw new ArgumentNullException(nameof(responseType));
    }

    public Type RequestType { get; }

    public Type ResponseType { get; }

    public string FactoryName { get; set; } = "Create";

    public string GatewayName { get; set; } = "messaging-gateway";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MessagingGatewayHandlerAttribute : Attribute
{
}
