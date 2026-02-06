using System;

namespace PatternKit.Generators.Iterator;

/// <summary>
/// Marks a partial type as an iterator host.
/// The generator produces a struct Enumerator, TryMoveNext, Current, and GetEnumerator.
/// The user provides a step method that advances state and yields items.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class IteratorAttribute : Attribute
{
    /// <summary>
    /// Whether to generate a struct Enumerator type. Default: true.
    /// </summary>
    public bool GenerateEnumerator { get; set; } = true;

    /// <summary>
    /// Whether to generate a TryMoveNext method. Default: true.
    /// </summary>
    public bool GenerateTryMoveNext { get; set; } = true;
}

/// <summary>
/// Marks a method as the step function for the iterator.
/// The method must have the signature: <c>bool TryStep(ref TState state, out T item)</c>.
/// Returning true means an item was yielded; false means iteration is complete.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class IteratorStepAttribute : Attribute
{
}

/// <summary>
/// Marks a static partial class as a tree-traversal iterator host.
/// The generator produces DFS and/or BFS traversal methods.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TraversalIteratorAttribute : Attribute
{
}

/// <summary>
/// Marks a partial method as a depth-first traversal to be generated.
/// The method must be declared in a class marked with <see cref="TraversalIteratorAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DepthFirstAttribute : Attribute
{
}

/// <summary>
/// Marks a partial method as a breadth-first traversal to be generated.
/// The method must be declared in a class marked with <see cref="TraversalIteratorAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class BreadthFirstAttribute : Attribute
{
}

/// <summary>
/// Marks a method that provides children for tree traversal.
/// The method must return an enumerable of child nodes.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TraversalChildrenAttribute : Attribute
{
}
