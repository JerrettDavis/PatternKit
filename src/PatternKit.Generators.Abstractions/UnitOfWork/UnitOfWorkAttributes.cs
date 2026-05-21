using System;

namespace PatternKit.Generators.UnitOfWork;

/// <summary>Generates a UnitOfWork factory from attributed commit and rollback methods.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateUnitOfWorkAttribute : Attribute
{
    public string FactoryName { get; set; } = "Create";
}

/// <summary>Marks a commit step for a generated unit of work.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class UnitOfWorkStepAttribute(string name, int order) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Unit-of-work step name is required.", nameof(name))
        : name;

    public int Order { get; } = order;
    public string? RollbackMethodName { get; set; }
}
