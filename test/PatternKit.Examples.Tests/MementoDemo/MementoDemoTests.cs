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

    [Scenario("No-op edits, null input guards, selections, and delete-forward paths are explicit")]
    [Fact]
    public Task Editor_EdgeCases_AreStable()
        => Given("an editor with text and a selection", () =>
            {
                var editor = new Editor();
                editor.Insert("abcdef");
                editor.Select(1, 3);
                return editor;
            })
            .When("exercise no-op and guarded operations", ed =>
            {
                var versionBeforeNoOps = ed.Version;
                var emptyInsertVersion = ed.Insert("");
                var sameSelectionVersion = ed.Select(1, 3);
                var negativeBackspaceVersion = ed.Backspace(0);
                ArgumentNullException? insertNull = null;
                ArgumentNullException? replaceNull = null;

                try
                {
                    ed.Insert(null!);
                }
                catch (ArgumentNullException ex)
                {
                    insertNull = ex;
                }

                try
                {
                    ed.ReplaceSelection(null!);
                }
                catch (ArgumentNullException ex)
                {
                    replaceNull = ex;
                }

                return (ed, versionBeforeNoOps, emptyInsertVersion, sameSelectionVersion, negativeBackspaceVersion, insertNull, replaceNull);
            })
            .Then("no-op edits keep the current version", r =>
                r.emptyInsertVersion == r.versionBeforeNoOps
                && r.sameSelectionVersion == r.versionBeforeNoOps
                && r.negativeBackspaceVersion == r.versionBeforeNoOps)
            .And("null text is rejected", r => r.insertNull is not null && r.replaceNull is not null)
            .When("delete the selection and attempt delete at end", r =>
            {
                var editor = r.ed;
                editor.DeleteForward();
                var textAfterSelectionDelete = editor.State.Text;
                editor.MoveCaret(editor.State.Text.Length);
                var versionAtEnd = editor.Version;
                var endDeleteVersion = editor.DeleteForward();
                return (editor, textAfterSelectionDelete, versionAtEnd, endDeleteVersion);
            })
            .Then("delete-forward removes the selected text", r => r.textAfterSelectionDelete == "aef")
            .And("delete-forward at end is a no-op", r => r.endDeleteVersion == r.versionAtEnd)
            .AssertPassed();

    [Scenario("Batch rejects nesting and can decline commits")]
    [Fact]
    public Task Editor_Batch_EdgeCases_AreStable()
        => Given("a new editor", () => new Editor())
            .When("running non-committing and nested batches", ed =>
            {
                var startVersion = ed.Version;
                var noCommitVersion = ed.Batch("no-commit", e =>
                {
                    e.Insert("draft");
                    return false;
                });

                InvalidOperationException? nested = null;
                try
                {
                    ed.Batch("outer", e =>
                    {
                        e.Batch("inner", _ => true);
                        return true;
                    });
                }
                catch (InvalidOperationException ex)
                {
                    nested = ex;
                }

                return (ed, startVersion, noCommitVersion, nested);
            })
            .Then("declined batch does not commit a new version", r => r.noCommitVersion == r.startVersion)
            .And("nested batches are rejected", r => r.nested is not null)
            .AssertPassed();

    [Scenario("Caret movement, replacement, and delete variants cover editor state edges")]
    [Fact]
    public Task Editor_StateAndDeleteVariants_AreStable()
        => Given("an editor with sample text", () =>
            {
                var editor = new Editor();
                editor.Insert("abcdef");
                return editor;
            })
            .When("moving outside document bounds", ed =>
            {
                ed.MoveCaret(-100);
                var clampedStart = ed.State;
                var versionAtStart = ed.Version;
                var repeatStartVersion = ed.MoveCaret(0);
                ed.MoveCaret(100);
                var clampedEnd = ed.State;
                return (ed, clampedStart, versionAtStart, repeatStartVersion, clampedEnd);
            })
            .Then("caret positions are clamped and repeated movement is a no-op", r =>
                r.clampedStart.Caret == 0
                && r.repeatStartVersion == r.versionAtStart
                && r.clampedEnd.Caret == r.ed.State.Text.Length)
            .When("replacing without a selection and formatting selected state", r =>
            {
                var editor = r.ed;
                editor.MoveCaret(3);
                editor.ReplaceSelection("XYZ");
                var afterInsertReplace = editor.State;
                editor.Select(2, 4);
                var selected = editor.State;
                var selectedText = selected.ToString();
                editor.Backspace();
                var afterSelectionBackspace = editor.State;
                return (editor, afterInsertReplace, selected, selectedText, afterSelectionBackspace);
            })
            .Then("replace without selection behaves like insert", r => r.afterInsertReplace.Text == "abcXYZdef")
            .And("selected state exposes selection range and formatted selection", r =>
                r.selected.HasSelection
                && r.selected.SelectionStart == 2
                && r.selected.SelectionEnd == 6
                && r.selectedText.Contains("Sel=[2,6)"))
            .And("backspace deletes selected text", r => r.afterSelectionBackspace.Text == "abdef")
            .When("using multi-character backspace and delete-forward", r =>
            {
                var editor = r.editor;
                editor.MoveCaret(editor.State.Text.Length);
                editor.Backspace(2);
                var afterMultiBackspace = editor.State;
                editor.MoveCaret(1);
                editor.DeleteForward(10);
                var afterMultiDelete = editor.State;
                return (editor, afterMultiBackspace, afterMultiDelete);
            })
            .Then("multi-character backspace removes the requested prefix before the caret", r => r.afterMultiBackspace.Text == "abd")
            .And("delete-forward clamps count to remaining text", r => r.afterMultiDelete.Text == "a")
            .AssertPassed();

    [Scenario("Undo and redo report false when no snapshot movement is available")]
    [Fact]
    public Task Editor_UndoRedo_NoMovement_ReturnFalse()
        => Given("a new editor", () => new Editor())
            .When("undo and redo are requested before edits", ed =>
            {
                var undo = ed.Undo();
                var redo = ed.Redo();
                return (ed, undo, redo, state: ed.State.ToString());
            })
            .Then("undo is unavailable", r => !r.undo)
            .And("redo is unavailable", r => !r.redo)
            .And("unselected state formatting omits a selection", r => !r.state.Contains("Sel="))
            .AssertPassed();
}
