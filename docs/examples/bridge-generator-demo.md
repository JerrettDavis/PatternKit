# Bridge Generator Demo: Shape Rendering

Demonstrates the Bridge pattern generator separating **shape abstractions** from **rendering implementations**, so both can vary independently.

* **Goal:** render the same shapes (Circle, Rectangle) through different renderers (SVG, Text) without coupling shape logic to rendering details.
* **Key idea:** `[BridgeAbstraction]` generates a protected constructor, implementor property, and forwarding methods, so concrete shapes just call the forwarded methods.

---

## The demo

```csharp
using PatternKit.Generators.Bridge;

[BridgeImplementor]
public partial interface IShapeRenderer
{
    string RenderCircle(int cx, int cy, int radius);
    string RenderRectangle(int x, int y, int width, int height);
    string Name { get; }
}

[BridgeAbstraction(typeof(IShapeRenderer))]
public partial class Shape
{
    public virtual string Describe() => $"[{GetType().Name}]";
}

public class Circle : Shape
{
    public Circle(IShapeRenderer renderer, int cx, int cy, int radius) : base(renderer) { ... }

    public override string Describe()
        => $"[Circle via {Name}] {RenderCircle(_cx, _cy, _radius)}";
}
```

## Mental model

```
Shape (abstraction) ──has──▶ IShapeRenderer (implementor)
  │                              ▲
  ├── Circle                     ├── SvgRenderer
  └── Rectangle                  └── TextRenderer
```

The generated bridge code provides `RenderCircle(...)`, `RenderRectangle(...)`, and `Name` as protected members on `Shape`, forwarding to the implementor.

## Test references

- `BridgeGeneratorDemoTests.Demo_Runs_Successfully` — validates the full demo
- `BridgeGeneratorDemoTests.Svg_Renders_Circle` — SVG output for circles
- `BridgeGeneratorDemoTests.Text_Renders_Rectangle` — text output for rectangles
- `BridgeGeneratorDemoTests.Different_Renderers_Produce_Different_Output` — same shape, different renderers
