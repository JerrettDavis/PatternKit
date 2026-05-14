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

    [Scenario("Reactive primitives suppress duplicate values, publish list changes, and stop after disposal")]
    [Fact]
    public async Task ReactivePrimitives_CoverListAndDisposalBranches()
    {
        await Given("observable primitives with subscribers", () =>
            {
                var propertyHub = new PropertyChangedHub();
                var count = new ObservableVar<int>(1);
                var list = new ObservableList<string>();
                var properties = new List<string>();
                var values = new List<(int Old, int New)>();
                var changes = new List<string>();
                var propertySub = propertyHub.Subscribe(properties.Add);
                var valueSub = count.Subscribe((oldValue, newValue) => values.Add((oldValue, newValue)));
                var listSub = list.Subscribe((action, item) => changes.Add($"{action}:{item}"));

                return new ReactiveHarness(propertyHub, count, list, properties, values, changes, propertySub, valueSub, listSub);
            })
            .When("raising changes and disposing subscriptions", harness =>
            {
                harness.PropertyHub.Raise("Name");
                harness.Count.Value = 1;
                harness.Count.Value = 2;
                harness.List.Add("alpha");
                var removedMissing = harness.List.Remove("missing");
                var removedExisting = harness.List.Remove("alpha");
                var snapshot = harness.List.Snapshot();
                var enumerated = harness.List.ToArray();

                harness.PropertySubscription.Dispose();
                harness.ValueSubscription.Dispose();
                harness.ListSubscription.Dispose();

                harness.PropertyHub.Raise("Ignored");
                harness.Count.Value = 3;
                harness.List.Add("beta");

                return (harness, removedMissing, removedExisting, snapshot, enumerated);
            })
            .Then("property notifications stop after disposal", result => result.harness.Properties.SequenceEqual(["Name"]))
            .And("duplicate observable values are suppressed", result => result.harness.Values.SequenceEqual([(1, 2)]))
            .And("list add/remove notifications are published only for real changes", result =>
                !result.removedMissing
                && result.removedExisting
                && result.harness.Changes.SequenceEqual(["add:alpha", "remove:alpha"]))
            .And("snapshot and enumeration reflect list state at the time", result =>
                result.snapshot.Count == 0
                && result.enumerated.Length == 0
                && result.harness.List.Count == 1)
            .AssertPassed();
    }

    private sealed record ReactiveHarness(
        PropertyChangedHub PropertyHub,
        ObservableVar<int> Count,
        ObservableList<string> List,
        List<string> Properties,
        List<(int Old, int New)> Values,
        List<string> Changes,
        IDisposable PropertySubscription,
        IDisposable ValueSubscription,
        IDisposable ListSubscription);
}

