using PatternKit.Generators;

namespace PatternKit.Examples.Generators.Memento;

/// <summary>
/// Demonstrates the Memento pattern source generator with a text editor scenario.
/// Shows snapshot capture, restore, and undo/redo functionality using generated code.
/// </summary>
public static class EditorStateDemo
{
    /// <summary>
    /// Immutable editor state using record class with generated memento support.
    /// The [Memento] attribute generates:
    /// - EditorStateMemento struct for capturing snapshots
    /// - EditorStateHistory class for undo/redo management
    /// </summary>
    [Memento(GenerateCaretaker = true, Capacity = 100, SkipDuplicates = true)]
    public partial record class EditorState(string Text, int Cursor, int SelectionLength)
    {
        public bool HasSelection => SelectionLength > 0;

        public int SelectionStart => Cursor;

        public int SelectionEnd => Cursor + SelectionLength;

        /// <summary>
        /// Creates an initial empty state.
        /// </summary>
        public static EditorState Empty() => new("", 0, 0);

        /// <summary>
        /// Inserts text at the cursor position (or replaces selection).
        /// Returns a new state with the text inserted.
        /// </summary>
        public EditorState Insert(string text)
        {
            if (string.IsNullOrEmpty(text))
                return this;

            string newText;
            int newCursor;

            if (HasSelection)
            {
                // Replace selection
                newText = Text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, text);
                newCursor = SelectionStart + text.Length;
            }
            else
            {
                // Insert at cursor
                newText = Text.Insert(Cursor, text);
                newCursor = Cursor + text.Length;
            }

            return this with { Text = newText, Cursor = newCursor, SelectionLength = 0 };
        }

        /// <summary>
        /// Moves the cursor to a new position.
        /// </summary>
        public EditorState MoveCursor(int position)
        {
            var newCursor = Math.Clamp(position, 0, Text.Length);
            return this with { Cursor = newCursor, SelectionLength = 0 };
        }

        /// <summary>
        /// Selects text from start position with the given length.
        /// </summary>
        public EditorState Select(int start, int length)
        {
            start = Math.Clamp(start, 0, Text.Length);
            var end = Math.Clamp(start + length, 0, Text.Length);
            var selLength = end - start;
            return this with { Cursor = start, SelectionLength = selLength };
        }

        /// <summary>
        /// Deletes the selection or one character before the cursor.
        /// </summary>
        public EditorState Backspace()
        {
            if (HasSelection)
            {
                var newText = Text.Remove(SelectionStart, SelectionLength);
                return this with { Text = newText, Cursor = SelectionStart, SelectionLength = 0 };
            }

            if (Cursor == 0)
                return this;

            var newText2 = Text.Remove(Cursor - 1, 1);
            return this with { Text = newText2, Cursor = Cursor - 1, SelectionLength = 0 };
        }

        public override string ToString() => HasSelection
            ? $"Text='{Text}' Cursor={Cursor} Sel=[{SelectionStart},{SelectionEnd})"
            : $"Text='{Text}' Cursor={Cursor}";
    }

    /// <summary>
    /// Text editor using the generated caretaker for undo/redo.
    /// </summary>
    public sealed class TextEditor
    {
        // The generated EditorStateHistory class manages undo/redo
        private readonly EditorStateHistory _history;

        public TextEditor()
        {
            _history = new EditorStateHistory(EditorState.Empty());
        }

        public EditorState Current => _history.Current;

        public bool CanUndo => _history.CanUndo;

        public bool CanRedo => _history.CanRedo;

        public int HistoryCount => _history.Count;

        /// <summary>
        /// Applies an editing operation and captures it in history.
        /// </summary>
        public void Apply(Func<EditorState, EditorState> operation)
        {
            var newState = operation(Current);
            _history.Capture(newState);
        }

        /// <summary>
        /// Undoes the last operation.
        /// </summary>
        public bool Undo()
        {
            return _history.Undo();
        }

        /// <summary>
        /// Redoes the last undone operation.
        /// </summary>
        public bool Redo()
        {
            return _history.Redo();
        }

        /// <summary>
        /// Clears all history and resets to empty state.
        /// </summary>
        public void Clear()
        {
            _history.Clear(EditorState.Empty());
        }
    }

    /// <summary>
    /// Runs a demonstration of the text editor with undo/redo.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();
        var editor = new TextEditor();

        void LogState(string action)
        {
            log.Add($"{action}: {editor.Current}");
        }

        // Initial state
        LogState("Initial");

        // Type "Hello"
        editor.Apply(s => s.Insert("Hello"));
        LogState("Insert 'Hello'");

        // Type " world"
        editor.Apply(s => s.Insert(" world"));
        LogState("Insert ' world'");

        // Move cursor to position 5 (after "Hello")
        editor.Apply(s => s.MoveCursor(5));
        LogState("Move cursor to 5");

        // Insert " brave new"
        editor.Apply(s => s.Insert(" brave new"));
        LogState("Insert ' brave new'");

        // Select "Hello" (0-5)
        editor.Apply(s => s.Select(0, 5));
        LogState("Select 'Hello'");

        // Replace with "Hi"
        editor.Apply(s => s.Insert("Hi"));
        LogState("Replace with 'Hi'");

        // Undo (restore "Hello brave new world" with selection)
        if (editor.Undo())
        {
            LogState("Undo");
        }

        // Undo (restore no selection)
        if (editor.Undo())
        {
            LogState("Undo");
        }

        // Undo (restore "Hello world")
        if (editor.Undo())
        {
            LogState("Undo");
        }

        // Redo
        if (editor.Redo())
        {
            LogState("Redo");
        }

        // Create divergent branch: make a new edit
        editor.Apply(s => s.MoveCursor(s.Text.Length));
        LogState("Move to end (divergent)");

        editor.Apply(s => s.Insert("!!!"));
        LogState("Insert '!!!' (clears redo)");

        // Try to redo (should fail - redo history was truncated)
        if (!editor.Redo())
        {
            log.Add("Redo failed (as expected - forward history was truncated)");
        }

        log.Add($"Final: {editor.Current}");
        log.Add($"CanUndo: {editor.CanUndo}, CanRedo: {editor.CanRedo}");
        log.Add($"History count: {editor.HistoryCount}");

        return log;
    }

    /// <summary>
    /// Demonstrates manual memento capture/restore without the caretaker.
    /// </summary>
    public static List<string> RunManualSnapshot()
    {
        var log = new List<string>();

        var state1 = new EditorState("Hello", 5, 0);
        log.Add($"State1: {state1}");

        // Manually capture a memento
        var memento = EditorStateMemento.Capture(in state1);
        log.Add($"Captured memento: Version={memento.MementoVersion}");

        // Modify state
        var state2 = state1.Insert(" world");
        log.Add($"State2: {state2}");

        // Restore from memento
        var restored = memento.RestoreNew();
        log.Add($"Restored: {restored}");
        log.Add($"Restored equals State1: {restored == state1}");

        return log;
    }
}
