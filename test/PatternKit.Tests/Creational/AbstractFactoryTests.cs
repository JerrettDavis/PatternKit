using PatternKit.Creational.AbstractFactory;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Creational;

[Feature("Creational - AbstractFactory (Family-based Object Creation)")]
public sealed class AbstractFactoryTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Product interfaces
    private interface IButton { string Render(); }
    private interface ITextBox { string Render(); }
    private interface IMenu { string Render(); }

    // Light theme products
    private sealed class LightButton : IButton { public string Render() => "[Light Button]"; }
    private sealed class LightTextBox : ITextBox { public string Render() => "[Light TextBox]"; }
    private sealed class LightMenu : IMenu { public string Render() => "[Light Menu]"; }

    // Dark theme products
    private sealed class DarkButton : IButton { public string Render() => "[Dark Button]"; }
    private sealed class DarkTextBox : ITextBox { public string Render() => "[Dark TextBox]"; }
    private sealed class DarkMenu : IMenu { public string Render() => "[Dark Menu]"; }

    [Scenario("AbstractFactory creates family of related products")]
    [Fact]
    public Task AbstractFactory_CreatesFamilyProducts()
        => Given("a UI theme factory with light and dark families", () =>
            AbstractFactory<string>.Create()
                .Family("light")
                    .Product<IButton>(() => new LightButton())
                    .Product<ITextBox>(() => new LightTextBox())
                    .Product<IMenu>(() => new LightMenu())
                .Family("dark")
                    .Product<IButton>(() => new DarkButton())
                    .Product<ITextBox>(() => new DarkTextBox())
                    .Product<IMenu>(() => new DarkMenu())
                .Build())
           .When("getting light family products", f =>
            {
                var family = f.GetFamily("light");
                return (
                    button: family.Create<IButton>().Render(),
                    textBox: family.Create<ITextBox>().Render(),
                    menu: family.Create<IMenu>().Render()
                );
            })
           .Then("button is light", r => r.button == "[Light Button]")
           .And("textBox is light", r => r.textBox == "[Light TextBox]")
           .And("menu is light", r => r.menu == "[Light Menu]")
           .AssertPassed();

    [Scenario("Different families produce different products")]
    [Fact]
    public Task AbstractFactory_DifferentFamilies()
        => Given("a UI theme factory", () =>
            AbstractFactory<string>.Create()
                .Family("light")
                    .Product<IButton>(() => new LightButton())
                .Family("dark")
                    .Product<IButton>(() => new DarkButton())
                .Build())
           .When("creating buttons from different families", f => (
               light: f.GetFamily("light").Create<IButton>().Render(),
               dark: f.GetFamily("dark").Create<IButton>().Render()
           ))
           .Then("light family creates light button", r => r.light == "[Light Button]")
           .And("dark family creates dark button", r => r.dark == "[Dark Button]")
           .AssertPassed();

    [Scenario("TryGetFamily returns false for unknown family")]
    [Fact]
    public Task AbstractFactory_TryGetFamily_Unknown()
        => Given("a factory with only light family", () =>
            AbstractFactory<string>.Create()
                .Family("light")
                    .Product<IButton>(() => new LightButton())
                .Build())
           .When("trying to get unknown family", f => f.TryGetFamily("cosmic", out _))
           .Then("returns false", ok => !ok)
           .AssertPassed();

    [Scenario("GetFamily throws for unknown family without default")]
    [Fact]
    public Task AbstractFactory_GetFamily_ThrowsUnknown()
        => Given("a factory without default", () =>
            AbstractFactory<string>.Create()
                .Family("light")
                    .Product<IButton>(() => new LightButton())
                .Build())
           .When("getting unknown family", f =>
            {
                try
                {
                    f.GetFamily("cosmic");
                    return false;
                }
                catch (KeyNotFoundException)
                {
                    return true;
                }
            })
           .Then("throws KeyNotFoundException", threw => threw)
           .AssertPassed();

    [Scenario("Default family is used when key not found")]
    [Fact]
    public Task AbstractFactory_DefaultFamily()
        => Given("a factory with default family", () =>
            AbstractFactory<string>.Create()
                .Family("light")
                    .Product<IButton>(() => new LightButton())
                .DefaultFamily()
                    .DefaultProduct<IButton>(() => new DarkButton())
                .Build())
           .When("getting unknown family", f => f.GetFamily("unknown").Create<IButton>().Render())
           .Then("uses default family", r => r == "[Dark Button]")
           .AssertPassed();

    [Scenario("TryCreate returns false for unregistered product type")]
    [Fact]
    public Task AbstractFactory_TryCreate_UnregisteredType()
        => Given("a family with only button registered", () =>
            AbstractFactory<string>.Create()
                .Family("light")
                    .Product<IButton>(() => new LightButton())
                .Build())
           .When("trying to create unregistered IMenu", f =>
            {
                var family = f.GetFamily("light");
                return family.TryCreate<IMenu>(out _);
            })
           .Then("returns false", ok => !ok)
           .AssertPassed();

    [Scenario("Create throws for unregistered product type")]
    [Fact]
    public Task AbstractFactory_Create_ThrowsUnregisteredType()
        => Given("a family with only button registered", () =>
            AbstractFactory<string>.Create()
                .Family("light")
                    .Product<IButton>(() => new LightButton())
                .Build())
           .When("creating unregistered IMenu", f =>
            {
                try
                {
                    f.GetFamily("light").Create<IMenu>();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            })
           .Then("throws InvalidOperationException", threw => threw)
           .AssertPassed();

    [Scenario("HasFamily returns correct status")]
    [Fact]
    public Task AbstractFactory_HasFamily()
        => Given("a factory with light family", () =>
            AbstractFactory<string>.Create()
                .Family("light")
                    .Product<IButton>(() => new LightButton())
                .Build())
           .When("checking family existence", f => (
               hasLight: f.HasFamily("light"),
               hasDark: f.HasFamily("dark")
           ))
           .Then("light exists", r => r.hasLight)
           .And("dark does not exist", r => !r.hasDark)
           .AssertPassed();

    [Scenario("CanCreate returns correct status")]
    [Fact]
    public Task AbstractFactory_CanCreate()
        => Given("a family with button but not menu", () =>
            AbstractFactory<string>.Create()
                .Family("light")
                    .Product<IButton>(() => new LightButton())
                .Build())
           .When("checking product availability", f =>
            {
                var family = f.GetFamily("light");
                return (
                    canButton: family.CanCreate<IButton>(),
                    canMenu: family.CanCreate<IMenu>()
                );
            })
           .Then("can create button", r => r.canButton)
           .And("cannot create menu", r => !r.canMenu)
           .AssertPassed();

    [Scenario("FamilyKeys returns all registered keys")]
    [Fact]
    public Task AbstractFactory_FamilyKeys()
        => Given("a factory with light and dark families", () =>
            AbstractFactory<string>.Create()
                .Family("light")
                    .Product<IButton>(() => new LightButton())
                .Family("dark")
                    .Product<IButton>(() => new DarkButton())
                .Build())
           .When("getting family keys", f => f.FamilyKeys.OrderBy(k => k).ToArray())
           .Then("contains both keys", keys => keys.Length == 2 && keys[0] == "dark" && keys[1] == "light")
           .AssertPassed();

    [Scenario("Custom comparer for case-insensitive keys")]
    [Fact]
    public Task AbstractFactory_CustomComparer()
        => Given("a factory with case-insensitive comparer", () =>
            AbstractFactory<string>.Create(StringComparer.OrdinalIgnoreCase)
                .Family("LIGHT")
                    .Product<IButton>(() => new LightButton())
                .Build())
           .When("getting family with different case", f => f.GetFamily("light").Create<IButton>().Render())
           .Then("finds family regardless of case", r => r == "[Light Button]")
           .AssertPassed();
}
