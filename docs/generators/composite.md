# Composite Generator

The Composite generator creates contract-first base types for tree-like object graphs. Generated code has no runtime dependency on PatternKit.

## Usage

```csharp
using PatternKit.Generators.Composite;

[CompositeComponent(GenerateTraversalHelpers = true)]
public partial interface ICategory
{
    string Name { get; }
}

public sealed class CategoryLeaf : CategoryComponentBase
{
    public override string Name { get; }
}

public sealed class CategoryNode : CategoryCompositeBase
{
    public override string Name { get; }
}
```

The generated component base defaults to leaf behavior: `IsLeaf` is `true`, `Children` is empty, and `Add`, `Remove`, and `Clear` throw `NotSupportedException`.

The generated composite base stores children in insertion order, rejects null children, exposes a read-only child view, and supports deterministic `Add`, `Remove`, and `Clear`.

## Attributes

- `[CompositeComponent]` marks a partial interface or abstract class component contract.
- `[CompositeIgnore]` excludes a property or method from generated base declarations.

## Diagnostics

- `PKCMP001`: component contract must be partial.
- `PKCMP002`: component target must be an interface or abstract class.
- `PKCMP003`: generated base type name conflicts with an existing type.
- `PKCMP004`: unsupported contract member, such as an event.
