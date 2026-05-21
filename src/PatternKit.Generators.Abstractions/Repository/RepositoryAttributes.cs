using System;

namespace PatternKit.Generators.Repository;

/// <summary>
/// Generates an in-memory repository factory for an entity and key type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateRepositoryAttribute(Type entityType, Type keyType) : Attribute
{
    public Type EntityType { get; } = entityType ?? throw new ArgumentNullException(nameof(entityType));
    public Type KeyType { get; } = keyType ?? throw new ArgumentNullException(nameof(keyType));
    public string FactoryName { get; set; } = "Create";
}

/// <summary>Marks the static method used to select repository keys from entities.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class RepositoryKeySelectorAttribute : Attribute;
