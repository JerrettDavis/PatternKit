using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Marks an assembly for source-generated Mediator pattern implementation.
/// 
/// The Mediator pattern reduces coupling between components by centralizing communication
/// through a mediator object. This source generator produces a standalone mediator with
/// zero PatternKit runtime dependencies.
/// 
/// The generated mediator supports:
/// - Commands (request → response)
/// - Notifications (fan-out to multiple handlers)
/// - Streams (async enumerable results)
/// - Pipelines (pre/post hooks for cross-cutting concerns)
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GenerateDispatcherAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the namespace for the generated dispatcher.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the name of the generated dispatcher class.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether to include object-based overloads (Send(object), Stream(object), etc.).
    /// Default is false for type safety.
    /// </summary>
    public bool IncludeObjectOverloads { get; set; }

    /// <summary>
    /// Gets or sets whether to include streaming support.
    /// Default is true.
    /// </summary>
    public bool IncludeStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets the visibility of the generated dispatcher.
    /// Default is public.
    /// </summary>
    public GeneratedVisibility Visibility { get; set; } = GeneratedVisibility.Public;
}

/// <summary>
/// Specifies the visibility of generated types.
/// </summary>
public enum GeneratedVisibility
{
    /// <summary>
    /// Generated types are public.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Generated types are internal.
    /// </summary>
    Internal = 1
}
