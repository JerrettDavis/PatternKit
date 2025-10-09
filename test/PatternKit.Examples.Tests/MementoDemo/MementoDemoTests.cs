using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;
using Editor = PatternKit.Examples.MementoDemo.MementoDemo.TextEditor;
using Demo = PatternKit.Examples.MementoDemo.MementoDemo;

namespace PatternKit.Examples.Tests.MementoDemo;

[Feature("Text Editor History (Memento) Demo")]
public sealed class MementoDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Demo Run produces expected final line and branching occurs")]
    [Fact]
    public Task Demo_Run_Final_State()
        => Given("the demo Run()", () => (Func<IReadOnlyList<string>>)Demo.Run)
            .When("executing it", run => run())
            .Then("final line exists", log => log.Last().StartsWith("FINAL:"))
            .And("final text contains !!!", log => log.Last().Contains("!!!"))
            .And("log includes branch insert tag", log => log.Any(l => l.Contains("branch insert !!!")))
            .AssertPassed();

    [Scenario("Branching after undo clears redo path")]
    [Fact]
    public Task Branching_Truncates_Redo()
        => Given("a new editor", () => new Editor())
            .When("insert A", ed =>
            {
                ed.Insert("A");
                return ed;
            })
            .When("insert B", ed =>
            {
                ed.Insert("B");
                return ed;
            })
            .When("insert C", ed =>
            {
                ed.Insert("C");
                return ed;
            })
            .When("undo", ed =>
            {
                ed.Undo();
                return ed;
            })
            .When("capture canRedo before branch", ed => ed)
            .Then("can redo is true", ed => ed.CanRedo)
            .When("insert X (branch)", ed =>
            {
                ed.Insert("X");
                return ed;
            })
            .When("inspect post-branch", ed => ed)
            .Then("redo no longer possible", ed => !ed.CanRedo)
            .And("text ends with ABX", ed => ed.State.Text.EndsWith("ABX"))
            .AssertPassed();

    [Scenario("Batch groups multiple edits into single snapshot")]
    [Fact]
    public Task Batch_Single_Snapshot()
        => Given("a new editor", () => new Editor())
            .When("baseline version", ed => (ed, v: ed.Version))
            .When("perform batch", t =>
            {
                var (ed, _) = t;
                ed.Batch("batch:multi", e =>
                {
                    e.Insert("Hello");
                    e.Insert(" ");
                    e.Insert("World");
                    return true;
                });
                return ed;
            })
            .When("capture post batch", ed => ed)
            .Then("text is Hello World", ed => ed.State.Text == "Hello World")
            .And("history contains a single non-init snapshot", ed => ed.History.Count == 2) // init + batch
            .AssertPassed();

    [Scenario("Capacity trim + monotonic versions")]
    [Fact]
    public Task Capacity_Monotonic()
        => Given("small capacity editor", () => new Editor(capacity: 5))
            .When("perform 20 inserts", ed =>
            {
                for (var i = 0; i < 20; i++) ed.Insert(i.ToString());
                return ed;
            })
            .When("snapshot history", ed => ed.History.ToArray())
            .Then("history count <= 5", snaps => snaps.Length <= 5)
            .And("versions strictly increasing", snaps =>
            {
                for (var i = 1; i < snaps.Length; i++)
                    if (snaps[i].Version <= snaps[i - 1].Version)
                        return false;
                return true;
            })
            .AssertPassed();
}