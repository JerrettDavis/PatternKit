using System;

namespace PatternKit.Generators.Command;

/// <summary>
/// Marks a partial type as a command for the Command pattern code generation.
/// The generator produces Execute/ExecuteAsync static methods that dispatch
/// to the annotated handler method.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>
    /// Custom name for the generated command type extension.
    /// Default uses the annotated type name.
    /// </summary>
    public string? CommandTypeName { get; set; }

    /// <summary>
    /// Whether to generate an async execution method.
    /// When not explicitly set, inferred from handler method signature.
    /// </summary>
    public bool GenerateAsync { get; set; }

    /// <summary>
    /// Forces generation of the async method even if the handler is synchronous.
    /// Default is false.
    /// </summary>
    public bool ForceAsync { get; set; }

    /// <summary>
    /// Whether to generate an Undo method.
    /// Requires a method marked with [CommandHandler] that has an undo counterpart.
    /// Default is false.
    /// </summary>
    public bool GenerateUndo { get; set; }
}

/// <summary>
/// Marks a method as the handler for a command.
/// The method implements the command's execution logic.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class CommandHandlerAttribute : Attribute
{
    /// <summary>
    /// Optional explicit command type this handler is for.
    /// When null, the handler is associated with the containing type's [Command].
    /// </summary>
    public Type? CommandType { get; set; }
}

/// <summary>
/// Marks a static partial class as a command host that groups multiple commands.
/// The generator produces Execute methods for each [CommandCase] method.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CommandHostAttribute : Attribute
{
}

/// <summary>
/// Marks a method within a [CommandHost] as a command case.
/// Each case gets a generated Execute/ExecuteAsync dispatch method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class CommandCaseAttribute : Attribute
{
}
