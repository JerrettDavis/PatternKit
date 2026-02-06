using PatternKit.Examples.Generators.Bridge;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Generators.Bridge;

[Feature("Bridge Generator Example")]
public sealed class BridgeGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Demo runs and produces output for all shape-renderer combinations")]
    [Fact]
    public Task Demo_Runs_Successfully()
        => Given("the bridge demo", () => new { })
            .When("Run() is called", _ => BridgeGeneratorDemo.Run())
            .Then("four rendered descriptions are returned", result =>
            {
                Assert.Equal(4, result.Count);
            })
            .AssertPassed();

    [Scenario("SVG renderer produces SVG markup for circle")]
    [Fact]
    public Task Svg_Renders_Circle()
        => Given("an SVG renderer and a circle", () => new
        {
            Renderer = new SvgRenderer(),
        })
            .When("a Circle is described", state =>
            {
                var circle = new Circle(state.Renderer, 50, 50, 25);
                return circle.Describe();
            })
            .Then("the description contains SVG circle markup", result =>
            {
                Assert.Contains("<circle", result);
                Assert.Contains("cx=\"50\"", result);
                Assert.Contains("r=\"25\"", result);
            })
            .AssertPassed();

    [Scenario("Text renderer produces plain text for rectangle")]
    [Fact]
    public Task Text_Renders_Rectangle()
        => Given("a text renderer and a rectangle", () => new
        {
            Renderer = new TextRenderer(),
        })
            .When("a Rectangle is described", state =>
            {
                var rect = new Rectangle(state.Renderer, 10, 10, 100, 50);
                return rect.Describe();
            })
            .Then("the description contains text dimensions", result =>
            {
                Assert.Contains("Rect at (10,10) 100x50", result);
            })
            .AssertPassed();

    [Scenario("Different renderers produce different output for the same shape")]
    [Fact]
    public Task Different_Renderers_Produce_Different_Output()
        => Given("two renderers", () => new
        {
            Svg = new SvgRenderer(),
            Text = new TextRenderer()
        })
            .When("circles are created with each", state => new
            {
                SvgOutput = new Circle(state.Svg, 0, 0, 10).Describe(),
                TextOutput = new Circle(state.Text, 0, 0, 10).Describe()
            })
            .Then("outputs differ", result =>
            {
                Assert.NotEqual(result.SvgOutput, result.TextOutput);
                Assert.Contains("SVG", result.SvgOutput);
                Assert.Contains("Text", result.TextOutput);
            })
            .AssertPassed();
}
