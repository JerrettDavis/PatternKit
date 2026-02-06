using System;
using System.Collections.Generic;
using PatternKit.Generators.Bridge;

namespace PatternKit.Examples.Generators.Bridge;

// ============================================================================
// EXAMPLE: Bridge Pattern - Rendering Shapes with Different Renderers
// ============================================================================
// Demonstrates the Bridge pattern separating abstraction (shapes) from
// implementation (renderers), so both can vary independently.
// ============================================================================

#region Implementor Contract

/// <summary>
/// Defines the rendering operations that different renderers must support.
/// This is the "Implementor" side of the Bridge pattern.
/// </summary>
[BridgeImplementor]
public partial interface IShapeRenderer
{
    /// <summary>Renders a circle at the given position with the given radius.</summary>
    string RenderCircle(int cx, int cy, int radius);

    /// <summary>Renders a rectangle with the given dimensions.</summary>
    string RenderRectangle(int x, int y, int width, int height);

    /// <summary>Gets the renderer name.</summary>
    string Name { get; }
}

#endregion

#region Concrete Implementors

/// <summary>Renders shapes as SVG markup.</summary>
public class SvgRenderer : IShapeRenderer
{
    public string Name => "SVG";

    public string RenderCircle(int cx, int cy, int radius)
        => $"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{radius}\" />";

    public string RenderRectangle(int x, int y, int width, int height)
        => $"<rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" />";
}

/// <summary>Renders shapes as plain text descriptions.</summary>
public class TextRenderer : IShapeRenderer
{
    public string Name => "Text";

    public string RenderCircle(int cx, int cy, int radius)
        => $"Circle at ({cx},{cy}) radius={radius}";

    public string RenderRectangle(int x, int y, int width, int height)
        => $"Rect at ({x},{y}) {width}x{height}";
}

#endregion

#region Abstraction

/// <summary>
/// Shape abstraction that delegates rendering to a bridge implementor.
/// The [BridgeAbstraction] attribute generates a protected constructor,
/// an Implementor property, and forwarding methods.
/// </summary>
[BridgeAbstraction(typeof(IShapeRenderer))]
public partial class Shape
{
    /// <summary>Gets a description of this shape rendered by the implementor.</summary>
    public virtual string Describe() => $"[{GetType().Name}]";
}

/// <summary>A circle shape.</summary>
public class Circle : Shape
{
    private readonly int _cx, _cy, _radius;

    public Circle(IShapeRenderer renderer, int cx, int cy, int radius) : base(renderer)
    {
        _cx = cx;
        _cy = cy;
        _radius = radius;
    }

    public override string Describe()
        => $"[Circle via {Name}] {RenderCircle(_cx, _cy, _radius)}";
}

/// <summary>A rectangle shape.</summary>
public class Rectangle : Shape
{
    private readonly int _x, _y, _width, _height;

    public Rectangle(IShapeRenderer renderer, int x, int y, int width, int height) : base(renderer)
    {
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    public override string Describe()
        => $"[Rect via {Name}] {RenderRectangle(_x, _y, _width, _height)}";
}

#endregion

#region Demo

/// <summary>
/// Demonstrates the Bridge pattern generator combining shapes with renderers.
/// </summary>
public static class BridgeGeneratorDemo
{
    /// <summary>
    /// Runs the bridge demo, rendering shapes with different implementors.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();

        var svgRenderer = new SvgRenderer();
        var textRenderer = new TextRenderer();

        // Same shapes, different renderers
        var svgCircle = new Circle(svgRenderer, 50, 50, 25);
        var textCircle = new Circle(textRenderer, 50, 50, 25);

        var svgRect = new Rectangle(svgRenderer, 10, 10, 100, 50);
        var textRect = new Rectangle(textRenderer, 10, 10, 100, 50);

        log.Add(svgCircle.Describe());
        log.Add(textCircle.Describe());
        log.Add(svgRect.Describe());
        log.Add(textRect.Describe());

        return log;
    }
}

#endregion
