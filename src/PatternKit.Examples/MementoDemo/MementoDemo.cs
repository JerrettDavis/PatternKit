using PatternKit.Behavioral.Memento;

namespace PatternKit.Examples.MementoDemo;

/// <summary>
/// A minimal, in-memory text editor showcasing the <see cref="Memento{TState}"/> pattern to provide
/// snapshot history, undo/redo, branching edits (truncate forward on divergent change), tagging, capacity
/// limits, and duplicate suppression.
/// </summary>
public static class MementoDemo
{
    /// <summary>Immutable editor state captured in snapshots.</summary>
    public readonly struct DocumentState(string text, int caret, int selectionLength)
    {
        public string Text { get; } = text;
        public int Caret { get; } = caret;
        public int SelectionLength { get; } = selectionLength;
        public bool HasSelection => SelectionLength > 0;
        public int SelectionStart => Caret;
        public int SelectionEnd => Caret + SelectionLength; // exclusive

        public override string ToString() => HasSelection
            ? $"Text='{Text}' Caret={Caret} Sel=[{Caret},{Caret + SelectionLength})"
            : $"Text='{Text}' Caret={Caret}";
    }

    private sealed class StateEquality : IEqualityComparer<DocumentState>
    {
        public bool Equals(DocumentState x, DocumentState y)
            => x.Caret == y.Caret && x.SelectionLength == y.SelectionLength && string.Equals(x.Text, y.Text, StringComparison.Ordinal);

        public int GetHashCode(DocumentState obj)
            => HashCode.Combine(obj.Text, obj.Caret, obj.SelectionLength);
    }

    /// <summary>
    /// Text editor encapsulating editing operations and history management.
    /// Methods return the snapshot version created (or the current version if skipped).
    /// </summary>
    public sealed class TextEditor
    {
        private readonly object _sync = new();
        private DocumentState _state;
        private readonly Memento<DocumentState> _history;

        private bool _batching;
        private DocumentState _batchBefore;

        public TextEditor(int capacity = 500, bool skipDuplicates = true)
        {
            _state = new DocumentState(string.Empty, 0, 0);
            var builder = Memento<DocumentState>.Create()
                .CloneWith(static (in s) => new DocumentState(s.Text, s.Caret, s.SelectionLength))
                .Capacity(capacity);
            if (skipDuplicates)
                builder.Equality(new StateEquality());
            _history = builder.Build();
            _history.Save(in _state, tag: "init");
        }

        public DocumentState State
        {
            get
            {
                lock (_sync) return _state;
            }
        }

        public int Version => _history.CurrentVersion;
        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public IReadOnlyList<Memento<DocumentState>.Snapshot> History => _history.History;

        private int Commit(string? tag = null)
        {
            if (_batching) return Version; // defer until batch end
            return _history.Save(in _state, tag);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

        private void SetState(string text, int caret, int selLen)
            => _state = new DocumentState(text, caret, selLen);

        public int MoveCaret(int position)
        {
            lock (_sync)
            {
                var newCaret = Clamp(position, 0, _state.Text.Length);
                if (newCaret == _state.Caret && _state.SelectionLength == 0) return Version; // no change
                SetState(_state.Text, newCaret, 0);
                return Commit(tag: $"caret:{newCaret}");
            }
        }

        public int Select(int start, int length)
        {
            lock (_sync)
            {
                start = Clamp(start, 0, _state.Text.Length);
                var end = Clamp(start + length, 0, _state.Text.Length);
                var len = end - start;
                if (start == _state.Caret && len == _state.SelectionLength) return Version;
                SetState(_state.Text, start, len);
                return Commit(tag: len > 0 ? $"select:{start}-{end}" : "select:empty");
            }
        }

        public int Insert(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            if (text.Length == 0) return Version; // ignore
            lock (_sync)
            {
                var doc = _state.Text;
                int caret = _state.Caret;
                string newText;
                if (_state.HasSelection)
                {
                    newText = doc.Remove(_state.SelectionStart, _state.SelectionLength)
                        .Insert(_state.SelectionStart, text);
                    caret = _state.SelectionStart + text.Length;
                }
                else
                {
                    newText = doc.Insert(caret, text);
                    caret += text.Length;
                }

                SetState(newText, caret, 0);
                return Commit(tag: $"insert:{Short(text)}");
            }
        }

        public int ReplaceSelection(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            lock (_sync)
            {
                if (!_state.HasSelection) return Insert(text);
                var doc = _state.Text;
                var newText = doc.Remove(_state.SelectionStart, _state.SelectionLength)
                    .Insert(_state.SelectionStart, text);
                var caret = _state.SelectionStart + text.Length;
                SetState(newText, caret, 0);
                return Commit(tag: $"replace:{Short(text)}");
            }
        }

        public int Backspace(int count = 1)
        {
            lock (_sync)
            {
                if (count <= 0) return Version;
                if (_state.HasSelection)
                {
                    var doc = _state.Text.Remove(_state.SelectionStart, _state.SelectionLength);
                    SetState(doc, _state.SelectionStart, 0);
                    return Commit(tag: "delete:sel");
                }

                if (_state.Caret == 0) return Version; // nothing
                var take = Math.Min(count, _state.Caret);
                var start = _state.Caret - take;
                var newText = _state.Text.Remove(start, take);
                SetState(newText, start, 0);
                return Commit(tag: take == 1 ? "backspace" : $"backspace:{take}");
            }
        }

        public int DeleteForward(int count = 1)
        {
            lock (_sync)
            {
                if (count <= 0) return Version;
                if (_state.HasSelection)
                {
                    var doc = _state.Text.Remove(_state.SelectionStart, _state.SelectionLength);
                    SetState(doc, _state.SelectionStart, 0);
                    return Commit(tag: "delete:sel");
                }

                if (_state.Caret >= _state.Text.Length) return Version;
                var take = Math.Min(count, _state.Text.Length - _state.Caret);
                var newText = _state.Text.Remove(_state.Caret, take);
                SetState(newText, _state.Caret, 0);
                return Commit(tag: take == 1 ? "del" : $"del:{take}");
            }
        }

        public bool Undo()
        {
            lock (_sync)
            {
                if (!_history.Undo(ref _state)) return false;
                return true;
            }
        }

        public bool Redo()
        {
            lock (_sync)
            {
                if (!_history.Redo(ref _state)) return false;
                return true;
            }
        }

        /// <summary>
        /// Perform multiple edits as a grouped logical step (single snapshot at end) when the lambda returns true.
        /// </summary>
        public int Batch(string tag, Func<TextEditor, bool> action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));
            lock (_sync)
            {
                if (_batching) throw new InvalidOperationException("Already batching.");
                _batching = true;
                _batchBefore = _state;
                bool changed;
                bool commit;
                try
                {
                    commit = action(this);
                    changed = !new StateEquality().Equals(_batchBefore, _state);
                }
                finally
                {
                    _batching = false;
                }

                if (!commit || !changed) return Version;
                return _history.Save(in _state, tag);
            }
        }

        private static string Short(string s) => s.Length <= 8 ? s : s[..8] + "…";
    }

    /// <summary>
    /// Runs a sample editing session and returns a log of snapshot tags and final text.
    /// </summary>
    public static IReadOnlyList<string> Run()
    {
        var editor = new TextEditor(capacity: 100);
        var log = new List<string>();

        void Capture(string action)
            => log.Add($"v{editor.Version}:{action} -> '{editor.State.Text}' (caret {editor.State.Caret})");

        editor.Insert("Hello");
        Capture("insert Hello");
        editor.Insert(", world");
        Capture("insert , world");
        editor.MoveCaret(5);
        Capture("move caret");
        editor.Insert(" brave new");
        Capture("insert brave new");
        editor.Select(0, 5);
        Capture("select first word");
        editor.ReplaceSelection("Hi");
        Capture("replace selection");
        editor.Backspace();
        Capture("backspace");
        editor.Undo();
        Capture("undo");
        editor.Undo();
        Capture("undo");
        editor.Redo();
        Capture("redo");
        // Divergent branch: change after undo (truncate forward history)
        editor.MoveCaret(editor.State.Text.Length);
        Capture("caret end");
        editor.Insert("!!!");
        Capture("branch insert !!!");

        log.Add($"FINAL:'{editor.State.Text}' version={editor.Version} history={editor.History.Count}");
        return log;
    }
}