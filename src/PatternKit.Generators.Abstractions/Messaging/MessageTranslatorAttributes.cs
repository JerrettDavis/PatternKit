using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateMessageTranslatorAttribute(Type inputType, Type outputType) : Attribute
{
    public Type InputType { get; } = inputType ?? throw new ArgumentNullException(nameof(inputType));
    public Type OutputType { get; } = outputType ?? throw new ArgumentNullException(nameof(outputType));
    public string FactoryName { get; set; } = "Create";
    public string TranslatorName { get; set; } = "message-translator";
    public bool PreserveHeaders { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class MessageTranslatorHandlerAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MessageTranslatorDropHeaderAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Header name is required.", nameof(name))
        : name;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MessageTranslatorHeaderAttribute(string name, string value) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Header name is required.", nameof(name))
        : name;

    public string Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
}
