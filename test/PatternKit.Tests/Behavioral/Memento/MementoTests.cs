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

#region Additional Memento Tests

public sealed class MementoBuilderTests
{
    [Fact]
    public void CanUndo_Empty_ReturnsFalse()
    {
        var h = Memento<int>.Create().Build();
        Assert.False(h.CanUndo);
    }

    [Fact]
    public void CanUndo_SingleSnapshot_ReturnsFalse()
    {
        var h = Memento<int>.Create().Build();
        var s = 1;
        h.Save(in s);
        Assert.False(h.CanUndo);
    }

    [Fact]
    public void CanUndo_MultipleSnapshots_ReturnsTrue()
    {
        var h = Memento<int>.Create().Build();
        var s = 1;
        h.Save(in s);
        s = 2;
        h.Save(in s);
        Assert.True(h.CanUndo);
    }

    [Fact]
    public void CanRedo_Empty_ReturnsFalse()
    {
        var h = Memento<int>.Create().Build();
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void CanRedo_AtEnd_ReturnsFalse()
    {
        var h = Memento<int>.Create().Build();
        var s = 1;
        h.Save(in s);
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void CanRedo_AfterUndo_ReturnsTrue()
    {
        var h = Memento<int>.Create().Build();
        var s = 1;
        h.Save(in s);
        s = 2;
        h.Save(in s);
        h.Undo(ref s);
        Assert.True(h.CanRedo);
    }

    [Fact]
    public void Undo_Empty_ReturnsFalse()
    {
        var h = Memento<int>.Create().Build();
        var s = 42;
        var result = h.Undo(ref s);
        Assert.False(result);
        Assert.Equal(42, s);
    }

    [Fact]
    public void Undo_SingleSnapshot_ReturnsFalse()
    {
        var h = Memento<int>.Create().Build();
        var s = 1;
        h.Save(in s);
        var result = h.Undo(ref s);
        Assert.False(result);
    }

    [Fact]
    public void Redo_Empty_ReturnsFalse()
    {
        var h = Memento<int>.Create().Build();
        var s = 42;
        var result = h.Redo(ref s);
        Assert.False(result);
        Assert.Equal(42, s);
    }

    [Fact]
    public void Redo_AtEnd_ReturnsFalse()
    {
        var h = Memento<int>.Create().Build();
        var s = 1;
        h.Save(in s);
        var result = h.Redo(ref s);
        Assert.False(result);
    }

    [Fact]
    public void TryGetCurrent_Empty_ReturnsFalse()
    {
        var h = Memento<int>.Create().Build();
        var result = h.TryGetCurrent(out var snapshot);
        Assert.False(result);
        Assert.Equal(default, snapshot);
    }

    [Fact]
    public void TryGetCurrent_WithSnapshot_ReturnsTrue()
    {
        var h = Memento<int>.Create().Build();
        var s = 42;
        h.Save(in s, tag: "test");
        var result = h.TryGetCurrent(out var snapshot);
        Assert.True(result);
        Assert.Equal(42, snapshot.State);
        Assert.Equal("test", snapshot.Tag);
    }

    [Fact]
    public void CurrentVersion_Empty_ReturnsZero()
    {
        var h = Memento<int>.Create().Build();
        Assert.Equal(0, h.CurrentVersion);
    }

    [Fact]
    public void Count_Empty_ReturnsZero()
    {
        var h = Memento<int>.Create().Build();
        Assert.Equal(0, h.Count);
    }

    [Fact]
    public void Capacity_NegativeValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Memento<int>.Create().Capacity(-1));
    }

    [Fact]
    public void Restore_EmptyHistory_ReturnsFalse()
    {
        var h = Memento<int>.Create().Build();
        var s = 42;
        var result = h.Restore(1, ref s);
        Assert.False(result);
        Assert.Equal(42, s);
    }

    [Fact]
    public void History_ReturnsImmutableCopy()
    {
        var h = Memento<int>.Create().Build();
        var s = 1;
        h.Save(in s);
        s = 2;
        h.Save(in s);

        var history1 = h.History;
        var history2 = h.History;

        Assert.NotSame(history1, history2);
        Assert.Equal(2, history1.Count);
        Assert.Equal(2, history2.Count);
    }

    [Fact]
    public void ApplyWith_CustomApplier()
    {
        var log = new List<string>();
        var h = Memento<int>.Create()
            .ApplyWith((ref int target, int snap) =>
            {
                log.Add($"apply:{snap}");
                target = snap;
            })
            .Build();

        var s = 1;
        h.Save(in s);
        s = 2;
        h.Save(in s);
        h.Undo(ref s);

        Assert.Contains("apply:1", log);
        Assert.Equal(1, s);
    }

    [Fact]
    public void DefaultCloner_ShallowCopies()
    {
        // Default cloner just returns the value (identity for value types)
        var h = Memento<int>.Create().Build();
        var s = 42;
        h.Save(in s);
        h.TryGetCurrent(out var snap);
        Assert.Equal(42, snap.State);
    }

    [Fact]
    public void Snapshot_HasTag_Property()
    {
        var h = Memento<int>.Create().Build();
        var s = 1;
        h.Save(in s, tag: "tagged");
        s = 2;
        h.Save(in s); // no tag

        var history = h.History;
        Assert.True(history[0].HasTag);
        Assert.False(history[1].HasTag);
    }

    [Fact]
    public void Snapshot_TimestampUtc_IsSet()
    {
        var before = DateTime.UtcNow;
        var h = Memento<int>.Create().Build();
        var s = 1;
        h.Save(in s);
        var after = DateTime.UtcNow;

        h.TryGetCurrent(out var snap);
        Assert.True(snap.TimestampUtc >= before);
        Assert.True(snap.TimestampUtc <= after);
    }

    [Fact]
    public void MultipleUndo_ReturnsToFirstState()
    {
        var h = Memento<int>.Create().Build();
        var s = 0;
        h.Save(in s); // v1
        s = 1;
        h.Save(in s); // v2
        s = 2;
        h.Save(in s); // v3
        s = 3;
        h.Save(in s); // v4

        Assert.True(h.Undo(ref s));
        Assert.Equal(2, s);
        Assert.True(h.Undo(ref s));
        Assert.Equal(1, s);
        Assert.True(h.Undo(ref s));
        Assert.Equal(0, s);
        Assert.False(h.Undo(ref s));
        Assert.Equal(0, s);
    }

    [Fact]
    public void MultipleRedo_ReturnsToLastState()
    {
        var h = Memento<int>.Create().Build();
        var s = 0;
        h.Save(in s);
        s = 1;
        h.Save(in s);
        s = 2;
        h.Save(in s);

        h.Undo(ref s);
        h.Undo(ref s);

        Assert.True(h.Redo(ref s));
        Assert.Equal(1, s);
        Assert.True(h.Redo(ref s));
        Assert.Equal(2, s);
        Assert.False(h.Redo(ref s));
    }

    [Fact]
    public void SaveAfterUndo_TruncatesForwardHistory()
    {
        var h = Memento<int>.Create().Build();
        var s = 0;
        h.Save(in s); // v1
        s = 1;
        h.Save(in s); // v2
        s = 2;
        h.Save(in s); // v3

        h.Undo(ref s); // cursor at v2
        Assert.Equal(1, s);

        s = 10;
        h.Save(in s); // v4, should truncate v3

        Assert.Equal(3, h.Count);
        Assert.False(h.CanRedo);
    }
}

#endregion