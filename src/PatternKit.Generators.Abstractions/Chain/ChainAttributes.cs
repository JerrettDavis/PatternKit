using System;

namespace PatternKit.Generators.Chain;

/// <summary>
/// Defines the chain execution model.
/// </summary>
public enum ChainModel
{
    /// <summary>
    /// Responsibility model: handlers are tried in order; the first that handles the request wins.
    /// Each handler returns a bool indicating whether it handled the request.
    /// </summary>
    Responsibility = 0,

    /// <summary>
    /// Pipeline model: handlers wrap each other like middleware.
    /// Each handler calls <c>next</c> to continue the chain and may modify input/output.
    /// </summary>
    Pipeline = 1
}

/// <summary>
/// Marks a partial type as a chain-of-responsibility or pipeline host.
/// The generator produces Handle/TryHandle/HandleAsync methods that orchestrate
/// handler methods in deterministic order.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ChainAttribute : Attribute
{
    /// <summary>
    /// The chain execution model. Default is <see cref="ChainModel.Responsibility"/>.
    /// </summary>
    public ChainModel Model { get; set; } = ChainModel.Responsibility;

    /// <summary>
    /// Name of the generated synchronous handle method. Default: "Handle".
    /// </summary>
    public string HandleMethodName { get; set; } = "Handle";

    /// <summary>
    /// Name of the generated try-handle method. Default: "TryHandle".
    /// Only generated for <see cref="ChainModel.Responsibility"/>.
    /// </summary>
    public string TryHandleMethodName { get; set; } = "TryHandle";

    /// <summary>
    /// Name of the generated async handle method. Default: "HandleAsync".
    /// </summary>
    public string HandleAsyncMethodName { get; set; } = "HandleAsync";

    /// <summary>
    /// Whether to generate async methods. When not set, inferred from handler signatures.
    /// </summary>
    public bool GenerateAsync { get; set; }

    /// <summary>
    /// Forces async generation even if no handlers are async.
    /// </summary>
    public bool ForceAsync { get; set; }
}

/// <summary>
/// Marks a method as a handler in the chain.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ChainHandlerAttribute : Attribute
{
    /// <summary>
    /// Execution order. Handlers execute in ascending order.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Optional name for diagnostics.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Initializes a new chain handler with the specified execution order.
    /// </summary>
    /// <param name="order">The execution order (lower values execute first).</param>
    public ChainHandlerAttribute(int order)
    {
        Order = order;
    }
}

/// <summary>
/// Marks a method as the default/fallback handler for the <see cref="ChainModel.Responsibility"/> model.
/// Called when no other handler handles the request.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ChainDefaultAttribute : Attribute
{
}

/// <summary>
/// Marks a method as the terminal handler for the <see cref="ChainModel.Pipeline"/> model.
/// The terminal is the innermost handler that does not call <c>next</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ChainTerminalAttribute : Attribute
{
}
