namespace PatternKit.Generators.Interpreter;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateInterpreterAttribute(Type contextType, Type resultType) : Attribute
{
    public Type ContextType { get; } = contextType ?? throw new ArgumentNullException(nameof(contextType));
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));
    public string FactoryMethodName { get; set; } = "Create";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class InterpreterTerminalAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Terminal name is required.", nameof(name))
        : name;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class InterpreterNonTerminalAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Non-terminal name is required.", nameof(name))
        : name;
}
