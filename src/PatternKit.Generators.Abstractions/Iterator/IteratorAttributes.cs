namespace PatternKit.Generators.Iterator;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class IteratorAttribute : Attribute
{
    public bool GenerateEnumerator { get; set; } = true;
    public bool GenerateTryMoveNext { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class IteratorStepAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TraversalIteratorAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class DepthFirstAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class BreadthFirstAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class TraversalChildrenAttribute : Attribute;
