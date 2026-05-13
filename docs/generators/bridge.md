# Bridge Generator

The Bridge generator emits the protected forwarding members that connect an abstraction to an implementor contract. Generated code has no runtime dependency on PatternKit.

## Usage

```csharp
using PatternKit.Generators.Bridge;

[BridgeImplementor]
public partial interface IRenderer
{
    void DrawLine(int x1, int y1, int x2, int y2);
    ValueTask FlushAsync(CancellationToken ct = default);
}

[BridgeAbstraction(typeof(IRenderer), GenerateDefault = true)]
public partial class Shape
{
    public void Draw() => DrawLine(0, 0, 10, 10);
}
```

The generator adds a protected constructor, an `Implementor` property, and protected forwarding methods that preserve the implementor signatures.

## Attributes

- `[BridgeImplementor]` marks an interface or abstract class implementor contract.
- `[BridgeAbstraction(typeof(TImplementor))]` marks the partial abstraction class.
- `[BridgeIgnore]` excludes a method or property from forwarding.

## Diagnostics

- `PKBRG001`: abstraction must be partial.
- `PKBRG002`: implementor must be an interface or abstract class.
- `PKBRG003`: unsupported implementor member, such as an event.
- `PKBRG004`: generated default abstraction type name conflicts with an existing type.
