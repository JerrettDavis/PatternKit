using System.Collections.ObjectModel;

namespace PatternKit.Behavioral.Memento;

/// <summary>
/// <para><b>Memento (generic, allocation-aware)</b></para>
/// Captures and restores immutable snapshots of a mutable "originator" state (<typeparamref name="TState"/>),
/// enabling <c>Undo</c>/<c>Redo</c>, point-in-time restore, and tagged/versioned history.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the classic GoF example (which often bakes caretaker/originator types), this implementation is a
/// standalone history engine you compose <em>around</em> an existing state object/value. You decide how to copy
/// (<see cref="Builder.CloneWith"/>) and optionally how to compare consecutive states (<see cref="Builder.Equality"/>).
/// </para>
/// <para>
/// The engine stores <em>snapshots</em> (deep or shallow – your cloner decides) and exposes allocation‑free
/// ref-based APIs for hot paths (<c>in</c>/<c>ref</c> usage for large structs). History is thread-safe: all mutating
/// operations take a monitor lock; snapshot publication uses simple list operations (no lock-free ring buffer yet).
/// </para>
/// <para>
/// <b>Undo/Redo semantics:</b> When you call <see cref="Undo"/>, the internal cursor moves backward and the prior
/// snapshot is applied via <see cref="Applier"/> (or plain assignment by default). <see cref="Redo"/> moves forward.
/// If you call <see cref="Save"/> while not at the end (i.e., after one or more undos), the forward branch is
/// truncated—standard editor behavior.
/// </para>
/// <para>
/// <b>Capacity:</b> If a positive capacity is configured, the oldest snapshot is evicted when the limit is exceeded.
/// Version numbers are monotonic (never reused) even when older entries are dropped; consumers can treat them as
/// logical timestamps.
/// </para>
/// <para>
/// <b>Equality skip:</b> If an equality comparer is supplied, consecutive duplicate states (according to comparer)
/// are ignored (the prior version is returned). This reduces noise in histories where transient mutations produce
/// the same logical state.
/// </para>
/// <para>
/// <b>Typical uses:</b> text buffers, document/view-model editing, drawing canvases, configuration editors,
/// domain aggregates with reversible workflows, speculative algorithm branches.
/// </para>
/// <para>
/// <b>Not a transaction manager:</b> It does not automatically compose multiple disparate objects; wrap them in an
/// aggregate state or snapshot each independently.
/// </para>
/// </remarks>
/// <typeparam name="TState">State type to snapshot. For reference types provide a deep clone if later mutation is expected.</typeparam>
public sealed class Memento<TState>
{
    /// <summary>Represents an immutable point-in-time snapshot.</summary>
    public readonly struct Snapshot
    {
        public int Version { get; }
        public TState State { get; }
        public DateTime TimestampUtc { get; }
        public string? Tag { get; }
        public bool HasTag => Tag is { Length: > 0 };

        internal Snapshot(int version, TState state, DateTime tsUtc, string? tag)
            => (Version, State, TimestampUtc, Tag) = (version, state, tsUtc, tag);
    }

    /// <summary>Delegate for cloning the provided state into an owned snapshot.</summary>
    public delegate TState Cloner(in TState state);

    /// <summary>Delegate for applying a snapshot to the target mutable state (default: assignment).</summary>
    public delegate void Applier(ref TState target, TState snapshotState);

    private readonly Cloner _cloner;
    private readonly Applier _applier;
    private readonly IEqualityComparer<TState>? _equality;
    private readonly int _capacity; // 0 = unbounded
    private readonly object _sync = new();

    private readonly List<Snapshot> _history = new();
    private int _cursor = -1;       // index of current snapshot
    private int _nextVersion = 1;   // monotonically increasing version id (start at 1 for readability)
    private int _baseVersionOffset; // versions below this were evicted (for future extension)

    private Memento(Cloner cloner, Applier applier, IEqualityComparer<TState>? equality, int capacity)
        => (_cloner, _applier, _equality, _capacity) = (cloner, applier, equality, capacity);

    /// <summary>The current snapshot version; 0 when no snapshots exist yet.</summary>
    public int CurrentVersion
    {
        get
        {
            lock (_sync) return _cursor >= 0 ? _history[_cursor].Version : 0;
        }
    }

    /// <summary>Total snapshots retained (after capacity trimming).</summary>
    public int Count
    {
        get
        {
            lock (_sync) return _history.Count;
        }
    }

    /// <summary>True if an Undo operation is currently possible.</summary>
    public bool CanUndo
    {
        get
        {
            lock (_sync) return _cursor > 0;
        }
    }

    /// <summary>True if a Redo operation is currently possible.</summary>
    public bool CanRedo
    {
        get
        {
            lock (_sync) return _cursor >= 0 && _cursor < _history.Count - 1;
        }
    }

    /// <summary>Enumerate a stable snapshot of the history (copy). Safe for external enumeration without locks.</summary>
    public IReadOnlyList<Snapshot> History
    {
        get
        {
            lock (_sync) return new ReadOnlyCollection<Snapshot>(_history.ToArray());
        }
    }

    /// <summary>
    /// Save (capture) the provided <paramref name="state"/> as a new snapshot. If an equality comparer was configured
    /// and the state is equal to the current snapshot, no new snapshot is added and the existing version is returned.
    /// </summary>
    /// <param name="state">Current state value (captured via cloner).</param>
    /// <param name="tag">Optional label for the snapshot (milestone, checkpoint, user action).</param>
    /// <returns>The version number representing this (or the previous if skipped) snapshot.</returns>
    public int Save(in TState state, string? tag = null)
    {
        lock (_sync)
        {
            // If not at end (after undos) drop forward branch
            if (_cursor < _history.Count - 1)
                _history.RemoveRange(_cursor + 1, _history.Count - _cursor - 1);

            if (_equality is not null && _cursor >= 0 && _equality.Equals(_history[_cursor].State, state))
                return _history[_cursor].Version; // skip duplicate

            var snapshotState = _cloner(in state);
            var version = _nextVersion++;
            var snap = new Snapshot(version, snapshotState, DateTime.UtcNow, tag);
            _history.Add(snap);
            _cursor = _history.Count - 1;

            if (_capacity > 0 && _history.Count > _capacity)
            {
                // Evict oldest (always index 0). Adjust cursor.
                _history.RemoveAt(0);
                _baseVersionOffset = _history[0].Version - 1;
                _cursor--;
            }

            return version;
        }
    }

    /// <summary>Attempt to undo to the previous snapshot, applying it to <paramref name="state"/>.</summary>
    /// <param name="state">Reference to the mutable state to overwrite.</param>
    /// <returns><c>true</c> if a prior snapshot existed and was applied; otherwise <c>false</c>.</returns>
    public bool Undo(ref TState state)
    {
        lock (_sync)
        {
            if (_cursor <= 0) return false;
            _cursor--;
            var snap = _history[_cursor];
            _applier(ref state, snap.State);
            return true;
        }
    }

    /// <summary>Attempt to redo to the next snapshot, applying it to <paramref name="state"/>.</summary>
    public bool Redo(ref TState state)
    {
        lock (_sync)
        {
            if (_cursor < 0 || _cursor >= _history.Count - 1) return false;
            _cursor++;
            var snap = _history[_cursor];
            _applier(ref state, snap.State);
            return true;
        }
    }

    /// <summary>
    /// Restore the snapshot with the specified <paramref name="version"/>, applying it to <paramref name="state"/>.
    /// Returns <c>false</c> if that version is not retained (evicted or invalid).
    /// </summary>
    public bool Restore(int version, ref TState state)
    {
        lock (_sync)
        {
            if (_history.Count == 0) return false;
            for (int i = 0; i < _history.Count; i++)
            {
                if (_history[i].Version == version)
                {
                    _cursor = i;
                    var snap = _history[i];
                    _applier(ref state, snap.State);
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Try to get the current snapshot without copying. Returns false if empty.</summary>
    public bool TryGetCurrent(out Snapshot snapshot)
    {
        lock (_sync)
        {
            if (_cursor < 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = _history[_cursor];
            return true;
        }
    }

    /// <summary>Create a new builder for <see cref="Memento{TState}"/>.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder (single-thread configure then build).</summary>
    public sealed class Builder
    {
        private Cloner _cloner = static (in s) => s; // shallow / value copy
        private Applier _applier = static (ref target, snap) => target = snap;
        private IEqualityComparer<TState>? _equality;
        private int _capacity;

        /// <summary>Configure a cloning function. Provide deep copy for mutable reference graphs.</summary>
        public Builder CloneWith(Cloner cloner)
        {
            _cloner = cloner;
            return this;
        }

        /// <summary>Configure a custom applier (e.g., partial mutation) instead of whole-assignment.</summary>
        public Builder ApplyWith(Applier applier)
        {
            _applier = applier;
            return this;
        }

        /// <summary>Provide an equality comparer; consecutive equal states are skipped.</summary>
        public Builder Equality(IEqualityComparer<TState> comparer)
        {
            _equality = comparer;
            return this;
        }

        /// <summary>Limit retained snapshots (FIFO eviction of oldest). 0 = unbounded.</summary>
        public Builder Capacity(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            return this;
        }

        /// <summary>Build the immutable history engine.</summary>
        public Memento<TState> Build() => new(_cloner, _applier, _equality, _capacity);
    }
}