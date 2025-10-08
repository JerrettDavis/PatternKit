using PatternKit.Structural.Flyweight;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;
using static PatternKit.Examples.FlyweightDemo.FlyweightDemo;

namespace PatternKit.Examples.Tests.FlyweightDemos;

[Feature("Examples - Flyweight Glyph & Style Demo")]
public sealed class FlyweightDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Glyph rendering reuses instances for repeated characters")]
    [Fact]
    public Task Glyph_Rendering_Reuses()
        => Given("a rendered sentence", () => RenderSentence("HELLO HELLO"))
            .When("group by reference", layout => layout.GroupBy(e => e.glyph).Select(g => (glyph:g.Key,count:g.Count())).ToList())
            .Then("some glyph appears more than once", groups => groups.Any(g => g.count > 1))
            .AssertPassed();

    [Scenario("Positions advance by glyph width (extrinsic computed)")]
    [Fact]
    public Task Positions_Advance_By_Width()
        => Given("layout for 'HI'", () => RenderSentence("HI"))
            .When("extract positions", layout => layout.Select(t => (t.glyph.Char, t.x, t.glyph.Width)).ToList())
            .Then("second x equals first width", data => data.Count == 2 && data[1].x == data[0].Width)
            .AssertPassed();

    [Scenario("Style names case-insensitive reuse")]
    [Fact]
    public Task Styles_Case_Insensitive()
        => Given("case-insensitive style pair", () => DemonstrateCaseInsensitiveStyles("header", "HEADER"))
            .When("inspect tuple", t => t)
            .Then("references identical", t => t.same)
            .AssertPassed();

    [Scenario("Reuse ratio less than 1 when duplicates present")]
    [Fact]
    public Task Reuse_Ratio()
        => Given("reuse analysis", () => AnalyzeReuse("ABABAB"))
            .When("inspect", r => r)
            .Then("unique less than total", r => r.unique < r.total)
            .And("ratio < 1", r => r.reuseRatio < 1.0)
            .AssertPassed();

    [Scenario("Flyweight TryGetExisting remains false before Get")]
    [Fact]
    public Task TryGetExisting_Before_Get()
        => Given("new flyweight", () => Flyweight<char, string>.Create().WithFactory(c => c.ToString()).Build())
            .When("TryGetExisting on 'X'", fw => fw.TryGetExisting('X', out _))
            .Then("returns false", ok => ok == false)
            .AssertPassed();
}

