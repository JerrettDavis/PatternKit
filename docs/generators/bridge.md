# Bridge Generator

## Overview

The **Bridge Generator** separates an abstraction from its implementation so that both can vary independently. It generates a protected constructor, an implementor property, and protected forwarding methods on an abstraction class that delegates to an implementor contract (interface or abstract class).

The generator produces **self-contained C# code** with **no runtime PatternKit dependency**, making it suitable for AOT and trimming scenarios.

## Quick Start

### 1. Define the Implementor Contract

Mark your interface or abstract class with `[BridgeImplementor]`:

```csharp
using PatternKit.Generators.Bridge;

[BridgeImplementor]
public interface IRenderer
{
    void DrawLine(int x1, int y1, int x2, int y2);
    void DrawCircle(int cx, int cy, int radius);
    string Name { get; }
}
```

### 2. Define the Abstraction

Mark your partial class with `[BridgeAbstraction]`, passing the implementor type:

```csharp
[BridgeAbstraction(typeof(IRenderer))]
public partial class Shape { }
```

### 3. Build Your Project

The generator produces `Shape.Bridge.g.cs`:

```csharp
partial class Shape
{
    protected Shape(IRenderer implementor)
    {
        Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
    }

    protected IRenderer Implementor { get; }

    protected void DrawLine(int x1, int y1, int x2, int y2)
        => Implementor.DrawLine(x1, y1, x2, y2);

    protected void DrawCircle(int cx, int cy, int radius)
        => Implementor.DrawCircle(cx, cy, radius);

    protected string Name
        => Implementor.Name;
}
```

### 4. Extend with Concrete Abstractions

```csharp
public class Circle : Shape
{
    public Circle(IRenderer renderer, int cx, int cy, int r) : base(renderer) { ... }
}
```

## Attributes

| Attribute | Target | Description |
|---|---|---|
| `[BridgeImplementor]` | Interface / Abstract class | Marks the implementor contract |
| `[BridgeAbstraction(Type)]` | Partial class | Marks the abstraction host; takes the implementor type |
| `[BridgeIgnore]` | Method / Property | Excludes a member from forwarding |

### BridgeAbstractionAttribute Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `ImplementorPropertyName` | `string` | `"Implementor"` | Name of the generated protected property |
| `GenerateDefault` | `bool` | `false` | Generate a default (no-op) implementor class |
| `DefaultTypeName` | `string?` | Auto | Name of the default implementor class |

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| PKBRG001 | Error | Abstraction type is not partial |
| PKBRG002 | Error | Implementor is not an interface or abstract class |
| PKBRG003 | Error | Unsupported member kind (events, indexers) |
| PKBRG004 | Error | Generated member name conflicts with existing member |
| PKBRG005 | Warning | Member is not accessible for forwarding |

## Examples

### Custom Property Name

```csharp
[BridgeAbstraction(typeof(IRenderer), ImplementorPropertyName = "Renderer")]
public partial class Shape { }
```

### Generating a Default Implementor

```csharp
[BridgeAbstraction(typeof(IRenderer), GenerateDefault = true, DefaultTypeName = "NullRenderer")]
public partial class Shape { }
```

This generates an additional `Shape.Bridge.Default.g.cs` with a `NullRenderer` class that provides no-op implementations for all members.

### Excluding Members

```csharp
[BridgeImplementor]
public interface IRenderer
{
    void Draw();

    [BridgeIgnore]
    void DebugDump(); // Not forwarded
}
```

## Best Practices

- Keep the implementor contract focused on a single responsibility
- Use `[BridgeIgnore]` for debug/diagnostic methods that should not be part of the bridge
- Prefer interfaces over abstract classes for implementor contracts
- Use `GenerateDefault = true` for testing scenarios (null-object pattern)

## See Also

- [Bridge Generator Example](../examples/bridge-generator-demo.md)
- [Decorator Generator](decorator.md)
- [Proxy Generator](proxy.md)
