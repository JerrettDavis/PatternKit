namespace PatternKit.Generators.Template;

/// <summary>
/// Marks a partial type as a template method workflow host.
/// The generator will produce Execute/ExecuteAsync methods that invoke
/// steps and hooks in deterministic order.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class TemplateAttribute : Attribute
{
    /// <summary>
    /// Name of the generated synchronous execute method. Default: "Execute".
    /// </summary>
    public string ExecuteMethodName { get; set; } = "Execute";

    /// <summary>
    /// Name of the generated asynchronous execute method. Default: "ExecuteAsync".
    /// </summary>
    public string ExecuteAsyncMethodName { get; set; } = "ExecuteAsync";

    /// <summary>
    /// Whether to generate the asynchronous Execute method.
    /// If not specified, inferred from presence of ValueTask/CancellationToken in steps/hooks.
    /// </summary>
    public bool GenerateAsync { get; set; }

    /// <summary>
    /// Forces generation of async method even if no async steps exist.
    /// </summary>
    public bool ForceAsync { get; set; }

    /// <summary>
    /// Determines how errors are handled during template execution.
    /// </summary>
    public TemplateErrorPolicy ErrorPolicy { get; set; } = TemplateErrorPolicy.Rethrow;
}
