using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a typed backplane topology method for request/reply routes and publish/subscribe endpoints.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateBackplaneTopologyAttribute : Attribute
{
    /// <summary>Creates a generated backplane topology attribute.</summary>
    public GenerateBackplaneTopologyAttribute(Type servicesType)
    {
        ServicesType = servicesType ?? throw new ArgumentNullException(nameof(servicesType));
    }

    /// <summary>Type that owns the handler methods referenced by route and subscription declarations.</summary>
    public Type ServicesType { get; }

    /// <summary>Host builder type that exposes the backplane topology fluent methods.</summary>
    public Type? HostBuilderType { get; set; }

    /// <summary>Name of the generated method that applies topology to a host builder.</summary>
    public string ConfigureMethodName { get; set; } = "Configure";
}

/// <summary>
/// Declares a generated request/reply command endpoint and route.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class BackplaneRequestReplyAttribute : Attribute
{
    /// <summary>Creates a request/reply route declaration.</summary>
    public BackplaneRequestReplyAttribute(
        Type requestType,
        Type responseType,
        string endpointName,
        string handlerMethodName)
    {
        RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
        ResponseType = responseType ?? throw new ArgumentNullException(nameof(responseType));

        if (string.IsNullOrWhiteSpace(endpointName))
            throw new ArgumentException("Endpoint name cannot be null, empty, or whitespace.", nameof(endpointName));

        if (string.IsNullOrWhiteSpace(handlerMethodName))
            throw new ArgumentException("Handler method name cannot be null, empty, or whitespace.", nameof(handlerMethodName));

        EndpointName = endpointName;
        HandlerMethodName = handlerMethodName;
    }

    /// <summary>Request payload type routed by the generated topology.</summary>
    public Type RequestType { get; }

    /// <summary>Response payload type returned by the request handler.</summary>
    public Type ResponseType { get; }

    /// <summary>Backplane endpoint that receives the request.</summary>
    public string EndpointName { get; }

    /// <summary>Method on the services type used as the request handler.</summary>
    public string HandlerMethodName { get; }

    /// <summary>Optional static predicate method on the topology type. When omitted, this is the default route for the request type.</summary>
    public string? PredicateMethodName { get; set; }
}

/// <summary>
/// Declares a generated publish/subscribe topic subscription.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class BackplaneSubscriptionAttribute : Attribute
{
    /// <summary>Creates a subscription declaration.</summary>
    public BackplaneSubscriptionAttribute(
        Type eventType,
        string topic,
        string endpointName,
        string handlerMethodName)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));

        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be null, empty, or whitespace.", nameof(topic));

        if (string.IsNullOrWhiteSpace(endpointName))
            throw new ArgumentException("Endpoint name cannot be null, empty, or whitespace.", nameof(endpointName));

        if (string.IsNullOrWhiteSpace(handlerMethodName))
            throw new ArgumentException("Handler method name cannot be null, empty, or whitespace.", nameof(handlerMethodName));

        Topic = topic;
        EndpointName = endpointName;
        HandlerMethodName = handlerMethodName;
    }

    /// <summary>Event payload type consumed by the subscription.</summary>
    public Type EventType { get; }

    /// <summary>Topic address consumed by the subscription.</summary>
    public string Topic { get; }

    /// <summary>Backplane endpoint that receives the subscription.</summary>
    public string EndpointName { get; }

    /// <summary>Method on the services type used as the event handler.</summary>
    public string HandlerMethodName { get; }
}
