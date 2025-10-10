using PatternKit.Examples.ObserverDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ObserverDemo;

[Feature("Reactive ViewModel (dependent properties + enablement) using Observer")]
public sealed class ReactiveViewModelTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Ctx(ProfileViewModel Vm, List<string> Notified)
    {
        public Ctx() : this(new ProfileViewModel(), new List<string>()) { }
    }

    private static Ctx Build() => new();

    [Scenario("FullName recomputes from FirstName/LastName; CanSave toggles")]
    [Fact]
    public async Task FullName_And_Enablement()
    {
        await Given("an empty viewmodel", Build)
            .When("subscribing to property changed", c =>
            {
                var list = c.Notified;
                c.Vm.PropertyChanged.Subscribe(name => list.Add(name));
                return c;
            })
            .When("set names", c => { c.Vm.FirstName.Value = "Ada"; c.Vm.LastName.Value = "Lovelace"; return c; })
            .Then("FullName is composed", c => c.Vm.FullName.Value == "Ada Lovelace")
            .And("CanSave is true", c => c.Vm.CanSave.Value)
            .And("notifications include FullName and CanSave", c => c.Notified.Contains("FullName") && c.Notified.Contains("CanSave"))
            .When("clear last name", c => { c.Vm.LastName.Value = ""; return c; })
            .Then("CanSave is false", c => !c.Vm.CanSave.Value)
            .AssertPassed();
    }
}

