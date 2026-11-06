using PatternKit.Examples.Generators.Memento;
using TinyBDD;

namespace PatternKit.Examples.Tests.GeneratorTests;

public sealed class MementoGeneratorExamplesTests
{
    [Scenario("EditorState EditingOperations AreImmutableAndClampRanges")]
    [Fact]
    public void EditorState_EditingOperations_AreImmutableAndClampRanges()
    {
        var initial = EditorStateDemo.EditorState.Empty();

        var inserted = initial.Insert("Hello").Insert(" world");
        var selected = inserted.Select(0, 50);
        var replaced = selected.Insert("Hi");
        var backspaced = replaced.Backspace();

        ScenarioExpect.Equal("", initial.Text);
        ScenarioExpect.Equal("Hello world", inserted.Text);
        ScenarioExpect.True(selected.HasSelection);
        ScenarioExpect.Equal(0, selected.SelectionStart);
        ScenarioExpect.Equal(inserted.Text.Length, selected.SelectionEnd);
        ScenarioExpect.Equal("Hi", replaced.Text);
        ScenarioExpect.Equal("H", backspaced.Text);
        ScenarioExpect.Equal("Text='H' Cursor=1", backspaced.ToString());
    }

    [Scenario("TextEditor TracksUndoRedoAndClear")]
    [Fact]
    public void TextEditor_TracksUndoRedoAndClear()
    {
        var editor = new EditorStateDemo.TextEditor();

        editor.Apply(state => state.Insert("Hello"));
        editor.Apply(state => state.Insert(" world"));

        ScenarioExpect.Equal("Hello world", editor.Current.Text);
        ScenarioExpect.True(editor.CanUndo);
        ScenarioExpect.False(editor.CanRedo);

        ScenarioExpect.True(editor.Undo());
        ScenarioExpect.Equal("Hello", editor.Current.Text);
        ScenarioExpect.True(editor.Redo());
        ScenarioExpect.Equal("Hello world", editor.Current.Text);

        editor.Clear();

        ScenarioExpect.Equal(EditorStateDemo.EditorState.Empty(), editor.Current);
        ScenarioExpect.False(editor.CanUndo);
        ScenarioExpect.False(editor.CanRedo);
    }

    [Scenario("EditorStateDemo RunAndManualSnapshot ReturnExpectedLogEntries")]
    [Fact]
    public void EditorStateDemo_RunAndManualSnapshot_ReturnExpectedLogEntries()
    {
        var runLog = EditorStateDemo.Run();
        var manualLog = EditorStateDemo.RunManualSnapshot();

        ScenarioExpect.Contains(runLog, line => line.StartsWith("Final:", StringComparison.Ordinal));
        ScenarioExpect.Contains(runLog, line => line.Contains("Redo failed", StringComparison.Ordinal));
        ScenarioExpect.Contains(manualLog, line => line == "Restored equals State1: True");
    }

    [Scenario("GameState MutatorsAndSaveLoad WorkAsExpected")]
    [Fact]
    public void GameState_MutatorsAndSaveLoad_WorkAsExpected()
    {
        var state = new GameStateDemo.GameState();

        state.MovePlayer(3, 4);
        state.TakeDamage(25);
        state.Heal(10);
        state.AddScore(250);
        state.AdvanceLevel();

        ScenarioExpect.Equal(3, state.PlayerX);
        ScenarioExpect.Equal(4, state.PlayerY);
        ScenarioExpect.Equal(100, state.Health);
        ScenarioExpect.Equal(250, state.Score);
        ScenarioExpect.Equal(2, state.Level);
        ScenarioExpect.Equal("Level 2: Health=100, Score=250, Pos=(3,4)", state.ToString());
    }

    [Scenario("GameSession PerformActionMutatesStateAndReportsNoRedoWhenNoForwardHistoryExists")]
    [Fact]
    public void GameSession_PerformActionMutatesStateAndReportsNoRedoWhenNoForwardHistoryExists()
    {
        var session = new GameStateDemo.GameSession();

        session.PerformAction(state => state.MovePlayer(5, 3), "move");
        session.PerformAction(state => state.TakeDamage(10), "damage");

        ScenarioExpect.Equal(5, session.State.PlayerX);
        ScenarioExpect.Equal(3, session.State.PlayerY);
        ScenarioExpect.Equal(90, session.State.Health);
        ScenarioExpect.False(session.RedoToCheckpoint());
    }

    [Scenario("GameSession DoesNotUndoWhenGeneratedHistorySkipsDuplicateMutableState")]
    [Fact]
    public void GameSession_DoesNotUndoWhenGeneratedHistorySkipsDuplicateMutableState()
    {
        var session = new GameStateDemo.GameSession();

        ScenarioExpect.False(session.UndoToCheckpoint());
        session.PerformAction(state => state.MovePlayer(2, 3), "move");
        session.PerformAction(state =>
        {
            state.TakeDamage(15);
            state.IsPaused = true;
        }, "damage and pause");

        ScenarioExpect.False(session.UndoToCheckpoint());
        ScenarioExpect.Equal(2, session.State.PlayerX);
        ScenarioExpect.Equal(3, session.State.PlayerY);
        ScenarioExpect.Equal(85, session.State.Health);
        ScenarioExpect.True(session.State.IsPaused);
        ScenarioExpect.False(session.RedoToCheckpoint());
    }

    [Scenario("GameState HealthIsClampedAndSaveLoadRestoresCapturedValues")]
    [Fact]
    public void GameState_HealthIsClampedAndSaveLoadRestoresCapturedValues()
    {
        var state = new GameStateDemo.GameState();

        state.TakeDamage(500);
        ScenarioExpect.Equal(0, state.Health);
        state.Heal(500);
        ScenarioExpect.Equal(100, state.Health);

        state.MovePlayer(1, 1);
        state.AddScore(10);
        var save = GameStateMemento.Capture(in state);

        state.MovePlayer(9, 9);
        state.TakeDamage(25);
        state.AddScore(90);
        save.Restore(state);

        ScenarioExpect.Equal(1, state.PlayerX);
        ScenarioExpect.Equal(1, state.PlayerY);
        ScenarioExpect.Equal(100, state.Health);
        ScenarioExpect.Equal(10, state.Score);
    }

    [Scenario("GameStateDemo RunAndSaveLoad ReturnExpectedLogEntries")]
    [Fact]
    public void GameStateDemo_RunAndSaveLoad_ReturnExpectedLogEntries()
    {
        var runLog = GameStateDemo.Run();
        var saveLoadLog = GameStateDemo.RunSaveLoad();

        ScenarioExpect.Contains(runLog, line => line.StartsWith("Final:", StringComparison.Ordinal));
        ScenarioExpect.Contains(runLog, line => line.Contains("Redo failed", StringComparison.Ordinal));
        ScenarioExpect.Contains(saveLoadLog, line => line.StartsWith("Game loaded:", StringComparison.Ordinal));
    }
}
