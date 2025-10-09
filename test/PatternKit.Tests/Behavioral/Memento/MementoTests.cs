using PatternKit.Behavioral.Memento;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Memento;

[Feature("Memento<TState> (snapshot history: undo, redo, restore)")]
public sealed class MementoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed class Doc
    {
        public string Text = string.Empty;
        public int Caret;
    }

    private static Memento<Doc> NewHistory() => Memento<Doc>.Create()
        .CloneWith(static (in d) => new Doc { Text = d.Text, Caret = d.Caret })
        .Equality(new DocComparer())
        .Capacity(10)
        .Build();

    private sealed class DocComparer : IEqualityComparer<Doc>
    {
        public bool Equals(Doc? x, Doc? y) => x!.Text == y!.Text && x.Caret == y.Caret;
        public int GetHashCode(Doc obj) => HashCode.Combine(obj.Text, obj.Caret);
    }

    [Scenario("Save -> Undo -> Redo basic traversal")]
    [Fact]
    public async Task UndoRedo()
    {
        await Given("history + doc", () => (H: NewHistory(), D: new Doc()))
            .When("initial save", s =>
            {
                s.H.Save(in s.D);
                return s;
            })
            .When("apply edit1", s =>
            {
                s.D.Text = "A";
                s.D.Caret = 1;
                s.H.Save(in s.D);
                return s;
            })
            .When("apply edit2", s =>
            {
                s.D.Text = "AB";
                s.D.Caret = 2;
                s.H.Save(in s.D);
                return s;
            })
            .When("undo", s =>
            {
                s.H.Undo(ref s.D);
                return s;
            })
            .Then("doc is 'A'", s => s.D is { Text: "A", Caret: 1 })
            .When("redo", s =>
            {
                s.H.Redo(ref s.D);
                return s;
            })
            .Then("doc is 'AB'", s => s.D is { Text: "AB", Caret: 2 })
            .AssertPassed();
    }

    [Scenario("Duplicate suppression via equality comparer")]
    [Fact]
    public async Task DuplicateSuppression()
    {
        await Given("history + doc", () => (H: NewHistory(), D: new Doc()))
            .When("save empty", s =>
            {
                s.H.Save(in s.D);
                return s;
            })
            .When("no logical change", s =>
            {
                s.D.Caret = 0;
                s.H.Save(in s.D);
                return s;
            })
            .When("check count still 1", s => s)
            .Then("count still 1", s => s.H.Count == 1)
            .When("change text", s =>
            {
                s.D.Text = "X";
                s.D.Caret = 1;
                s.H.Save(in s.D);
                return s;
            })
            .When("inspect after change", s => s)
            .Then("count now 2", s => s.H.Count == 2)
            .AssertPassed();
    }

    [Scenario("Capacity eviction drops oldest")]
    [Fact]
    public async Task CapacityEviction()
    {
        var h = Memento<int>.Create().Capacity(3).Build();
        for (var i = 0; i < 5; i++)
        {
            h.Save(in i);
        }

        await Given("bounded history", () => h)
            .When("inspect", m => m)
            .Then("count == 3", m => m.Count == 3)
            .And("current version == 5", m => m.CurrentVersion == 5)
            .AssertPassed();
    }

    [Scenario("Restore by version + forward truncation after divergent save")]
    [Fact]
    public async Task Restore_TruncatesForwardBranch()
    {
        var h = Memento<int>.Create().Build();
        var s = 0;
        h.Save(in s); // v1
        s = 1;
        h.Save(in s); // v2
        s = 2;
        h.Save(in s); // v3
        s = 99;
        h.Restore(2, ref s); // back to v2
        s = 5;
        h.Save(in s); // new v4, forward (v3) truncated

        await Given("history", () => h)
            .When("inspect", x => x)
            .Then("count == 3 (v1,v2,v4)", x => x.Count == 3)
            .And("current version == 4", x => x.CurrentVersion == 4)
            .AssertPassed();
    }

    [Scenario("Restore invalid version returns false")]
    [Fact]
    public async Task RestoreInvalid()
    {
        var h = Memento<string>.Create().Build();
        var s = "a";
        h.Save(in s); // v1
        s = "b";
        h.Save(in s); // v2
        var before = s;
        var local = s;
        var ok = h.Restore(999, ref local);
        await Given("attempt invalid restore", () => (ok, before, after: local))
            .When("inspect tuple", t => t)
            .Then("restore failed", x => x.ok == false)
            .And("state unchanged", x => x.after == x.before)
            .AssertPassed();
    }

    [Scenario("Tags captured when provided")]
    [Fact]
    public async Task Tags()
    {
        var h = Memento<string>.Create().Build();
        var s = "";
        h.Save(in s, tag: "start");
        s = "A";
        h.Save(in s);
        s = "AB";
        h.Save(in s, tag: "milestone");

        await Given("history", () => h)
            .When("inspect", x => x)
            .Then("two tagged snapshots", x => x.History.Count(sn => sn.HasTag) == 2)
            .AssertPassed();
    }
}