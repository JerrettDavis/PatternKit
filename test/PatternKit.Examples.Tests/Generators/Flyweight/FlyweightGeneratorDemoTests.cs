using PatternKit.Examples.Generators.Flyweight;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Generators.Flyweight;

[Feature("Flyweight Generator Example")]
public sealed class FlyweightGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Run returns glyph sharing results")]
    [Fact]
    public Task Run_Returns_Glyph_Sharing_Results()
        => Given("the flyweight generator demo", () => (Func<List<string>>)FlyweightGeneratorDemo.Run)
            .When("running the demo", run => run())
            .Then("log contains cache results", log => log.Count > 0)
            .And("same instance for duplicate lookups", log => log.Any(l => l.Contains("Same instance: True")))
            .And("cache reports correct count", log => log.Any(l => l.Contains("Cache count:")))
            .AssertPassed();

    [Scenario("Flyweight cache returns same instance for same key")]
    [Fact]
    public Task Cache_Returns_Same_Instance()
        => Given("a cleared glyph cache", () =>
            {
                Glyph.GlyphCache.Clear();
                return true;
            })
            .When("getting the same character twice", _ =>
            {
                var g1 = Glyph.GlyphCache.Get('X');
                var g2 = Glyph.GlyphCache.Get('X');
                return (g1, g2);
            })
            .Then("both references are the same object", result => ReferenceEquals(result.g1, result.g2))
            .AssertPassed();

    [Scenario("TryGet returns false for uncached keys")]
    [Fact]
    public Task TryGet_Returns_False_For_Uncached()
        => Given("a cleared glyph cache", () =>
            {
                Glyph.GlyphCache.Clear();
                return true;
            })
            .When("trying to get an uncached character", state =>
                Glyph.GlyphCache.TryGet('Z', out var _unused))
            .Then("TryGet returns false", found => !found)
            .AssertPassed();

    [Scenario("Clear empties the cache")]
    [Fact]
    public Task Clear_Empties_Cache()
        => Given("a cache with some entries", () =>
            {
                Glyph.GlyphCache.Clear();
                _ = Glyph.GlyphCache.Get('A');
                _ = Glyph.GlyphCache.Get('B');
                return Glyph.GlyphCache.Count;
            })
            .When("clearing the cache", _ =>
            {
                Glyph.GlyphCache.Clear();
                return Glyph.GlyphCache.Count;
            })
            .Then("cache count is zero", count => count == 0)
            .AssertPassed();
}
