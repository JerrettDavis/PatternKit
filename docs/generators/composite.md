# Composite Generator

## Overview

The **Composite Generator** creates GoF-compliant Composite pattern implementations from a component contract (interface or abstract class). It generates:

- **ComponentBase** — an abstract leaf base class with default no-op implementations
- **CompositeBase** — an abstract composite base class with child management (`Add`/`Remove`) and delegation to children
- **Traversal helpers** (optional) — `DepthFirst()` and `BreadthFirst()` enumeration methods

The generator produces **self-contained C# code** with **no runtime PatternKit dependency**.

## Quick Start

### 1. Define the Component Contract

Mark your interface or abstract class with `[CompositeComponent]`:

```csharp
using PatternKit.Generators.Composite;

[CompositeComponent]
public partial interface IGraphic
{
    void Draw();
    void Move(int x, int y);
}
```

### 2. Build Your Project

The generator produces `IGraphic.Composite.g.cs` containing:

```csharp
// Leaf base with default no-op implementations
public abstract partial class GraphicBase : IGraphic
{
    public virtual void Draw() { }
    public virtual void Move(int x, int y) { }
}

// Composite base with child management and delegation
public abstract partial class GraphicComposite : IGraphic
{
    public IReadOnlyList<IGraphic> Children => _children;

    public void Add(IGraphic child) { ... }
    public void Remove(IGraphic child) { ... }

    public virtual void Draw()
    {
        for (int i = 0; i < _children.Count; i++)
            _children[i].Draw();
    }

    public virtual void Move(int x, int y)
    {
        for (int i = 0; i < _children.Count; i++)
            _children[i].Move(x, y);
    }
}
```

### 3. Create Concrete Leaves and Composites

```csharp
public class Circle : GraphicBase
{
    public override void Draw() => Console.WriteLine("Drawing circle");
}

public class Group : GraphicComposite
{
    // Add/Remove/Draw/Move inherited from generated base
}
```

## Attributes

| Attribute | Target | Description |
|---|---|---|
| `[CompositeComponent]` | Interface / Abstract class | Marks the component contract |
| `[CompositeIgnore]` | Method / Property | Excludes a member from generation |

### CompositeComponentAttribute Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `ComponentBaseName` | `string?` | Auto | Name of the leaf base class |
| `CompositeBaseName` | `string?` | Auto | Name of the composite base class |
| `ChildrenPropertyName` | `string` | `"Children"` | Property name for the children collection |
| `Storage` | `CompositeChildrenStorage` | `List` | Backing collection type (`List` or `ImmutableArray`) |
| `GenerateTraversalHelpers` | `bool` | `false` | Generate `DepthFirst()`/`BreadthFirst()` methods |

### CompositeChildrenStorage Enum

| Value | Description |
|---|---|
| `List` | `List<T>` — mutable child management |
| `ImmutableArray` | `ImmutableArray<T>` — immutable child management |

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| PKCPS001 | Error | Component type is not partial |
| PKCPS002 | Error | Component is not an interface or abstract class |
| PKCPS003 | Error | Generated type name conflicts with existing type |
| PKCPS004 | Error | Unsupported member kind (events, indexers) |

## Examples

### Custom Names and Traversal

```csharp
[CompositeComponent(
    ComponentBaseName = "Leaf",
    CompositeBaseName = "Group",
    ChildrenPropertyName = "Items",
    GenerateTraversalHelpers = true)]
public partial interface IGraphic
{
    void Draw();
}
```

### Immutable Storage

```csharp
[CompositeComponent(Storage = CompositeChildrenStorage.ImmutableArray)]
public partial interface IGraphic
{
    void Draw();
}
```

### Excluding Members

```csharp
[CompositeComponent]
public partial interface IGraphic
{
    void Draw();

    [CompositeIgnore]
    void DebugDump(); // Not delegated to children
}
```

## Best Practices

- Use `[CompositeIgnore]` for members that only make sense on individual nodes (not composites)
- Enable `GenerateTraversalHelpers` when you need to walk the tree
- The generated `CompositeBase` uses `for` loops (no LINQ) for zero-allocation iteration
- Methods with return values return the last child's result; override if you need aggregation logic

## See Also

- [Composite Generator Example](../examples/composite-generator-demo.md)
- [Bridge Generator](bridge.md)
- [Decorator Generator](decorator.md)
