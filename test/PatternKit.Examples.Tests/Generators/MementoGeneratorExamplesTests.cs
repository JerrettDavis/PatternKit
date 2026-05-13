using PatternKit.Examples.Generators.Memento;

namespace PatternKit.Examples.Tests.GeneratorTests;

public sealed class MementoGeneratorExamplesTests
{
    [Fact]
    public void EditorState_EditingOperations_AreImmutableAndClampRanges()
    {
        var initial = EditorStateDemo.EditorState.Empty();

        var inserted = initial.Insert("Hello").Insert(" world");
        var selected = inserted.Select(0, 50);
        var replaced = selected.Insert("Hi");
        var backspaced = replaced.Backspace();

        Assert.Equal("", initial.Text);
        Assert.Equal("Hello world", inserted.Text);
        Assert.True(selected.HasSelection);
        Assert.Equal(0, selected.SelectionStart);
        Assert.Equal(inserted.Text.Length, selected.SelectionEnd);
        Assert.Equal("Hi", replaced.Text);
        Assert.Equal("H", backspaced.Text);
        Assert.Equal("Text='H' Cursor=1", backspaced.ToString());
    }

    [Fact]
    public void TextEditor_TracksUndoRedoAndClear()
    {
        var editor = new EditorStateDemo.TextEditor();

        editor.Apply(state => state.Insert("Hello"));
        editor.Apply(state => state.Insert(" world"));

        Assert.Equal("Hello world", editor.Current.Text);
        Assert.True(editor.CanUndo);
        Assert.False(editor.CanRedo);

        Assert.True(editor.Undo());
        Assert.Equal("Hello", editor.Current.Text);
        Assert.True(editor.Redo());
        Assert.Equal("Hello world", editor.Current.Text);

        editor.Clear();

        Assert.Equal(EditorStateDemo.EditorState.Empty(), editor.Current);
        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
    }

    [Fact]
    public void EditorStateDemo_RunAndManualSnapshot_ReturnExpectedLogEntries()
    {
        var runLog = EditorStateDemo.Run();
        var manualLog = EditorStateDemo.RunManualSnapshot();

        Assert.Contains(runLog, line => line.StartsWith("Final:", StringComparison.Ordinal));
        Assert.Contains(runLog, line => line.Contains("Redo failed", StringComparison.Ordinal));
        Assert.Contains(manualLog, line => line == "Restored equals State1: True");
    }

    [Fact]
    public void GameState_MutatorsAndSaveLoad_WorkAsExpected()
    {
        var state = new GameStateDemo.GameState();

        state.MovePlayer(3, 4);
        state.TakeDamage(25);
        state.Heal(10);
        state.AddScore(250);
        state.AdvanceLevel();

        Assert.Equal(3, state.PlayerX);
        Assert.Equal(4, state.PlayerY);
        Assert.Equal(100, state.Health);
        Assert.Equal(250, state.Score);
        Assert.Equal(2, state.Level);
        Assert.Equal("Level 2: Health=100, Score=250, Pos=(3,4)", state.ToString());
    }

    [Fact]
    public void GameSession_PerformActionMutatesStateAndReportsNoRedoWhenNoForwardHistoryExists()
    {
        var session = new GameStateDemo.GameSession();

        session.PerformAction(state => state.MovePlayer(5, 3), "move");
        session.PerformAction(state => state.TakeDamage(10), "damage");

        Assert.Equal(5, session.State.PlayerX);
        Assert.Equal(3, session.State.PlayerY);
        Assert.Equal(90, session.State.Health);
        Assert.False(session.RedoToCheckpoint());
    }

    [Fact]
    public void GameStateDemo_RunAndSaveLoad_ReturnExpectedLogEntries()
    {
        var runLog = GameStateDemo.Run();
        var saveLoadLog = GameStateDemo.RunSaveLoad();

        Assert.Contains(runLog, line => line.StartsWith("Final:", StringComparison.Ordinal));
        Assert.Contains(runLog, line => line.Contains("Redo failed", StringComparison.Ordinal));
        Assert.Contains(saveLoadLog, line => line.StartsWith("Game loaded:", StringComparison.Ordinal));
    }
}
