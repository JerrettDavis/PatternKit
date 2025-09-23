using PatternKit.Common;

namespace PatternKit.Behavioral.Iterator;

/// <summary>
/// A replayable, forkable, lookahead-capable sequence abstraction that augments <see cref="IEnumerable{T}"/> when
/// you need multi-pass / speculative traversal without re-enumerating or re-materializing the original source.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
/// <remarks>
/// <para>
/// <b>Why?</b> Standard <see cref="IEnumerable{T}"/> is single forward pass per enumerator. To implement lookahead,
/// backtracking, or parallel cursors you typically buffer, or you fully materialize into an array/list and index it.
/// <see cref="ReplayableSequence{T}"/> sits in between: it buffers <em>on demand</em> as cursors ask for items.
/// Every element is pulled from the underlying source at most once, appended to an internal buffer, then served from
/// that buffer to any number of forks / cursors.
/// </para>
/// <para>
/// <b>Core ideas:</b>
/// </para>
/// <list type="bullet">
///   <item><description><b>Cursor</b>: lightweight value struct holding an index into a shared buffer.</description></item>
///   <item><description><b>On-demand buffering</b>: elements materialize only when first requested (via <c>TryNext</c>, <c>Peek</c> or <c>Lookahead</c>).</description></item>
///   <item><description><b>Fork</b>: create an independent cursor snapshot at the current position.</description></item>
///   <item><description><b>Lookahead</b>: inspect future elements without advancing (parser / tokenizer friendly).</description></item>
///   <item><description><b>LINQ interop</b>: any cursor can be projected to an <see cref="IEnumerable{T}"/> without changing its own position.</description></item>
/// </list>
/// <para><b>Not thread-safe.</b> Treat instance + its cursors as confined to one logical thread of use.</para>
/// </remarks>
public sealed class ReplayableSequence<T>
{
    private readonly List<T> _buffer = [];
    private IEnumerator<T>? _source; // null when exhausted / disposed

    private ReplayableSequence(IEnumerable<T> source) => _source = source.GetEnumerator();

    /// <summary>Create a replayable wrapper over <paramref name="source"/>.</summary>
    public static ReplayableSequence<T> From(IEnumerable<T> source) => new(source);

    /// <summary>Obtain a fresh cursor at the start (position 0).</summary>
    public Cursor GetCursor() => new(this, 0);

    /// <summary>Enumerates the entire (shared) sequence as an <see cref="IEnumerable{T}"/>.</summary>
    /// <remarks>
    /// Each enumeration uses a new starting cursor. Already buffered elements are reused; remaining ones stream in.
    /// </remarks>
    public IEnumerable<T> AsEnumerable()
    {
        var c = GetCursor();
        while (c.TryNext(out var v, out var next))
        {
            yield return v;
            c = next;
        }
    }

    internal bool EnsureBuffered(int index)
    {
        if (index < _buffer.Count) return true; // already present
        if (_source is null) return false;      // already exhausted

        while (index >= _buffer.Count)
        {
            if (_source.MoveNext())
            {
                _buffer.Add(_source.Current);
            }
            else
            {
                _source.Dispose();
                _source = null;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A lightweight position inside a <see cref="ReplayableSequence{T}"/>. Value-type; copying is cheap.
    /// </summary>
    /// <remarks>
    /// Enumerating via a cursor is <b>non-destructive</b> to other cursors; all share the underlying buffer.
    /// </remarks>
    public readonly struct Cursor
    {
        private readonly ReplayableSequence<T> _owner;
        private readonly int _index; // current position (next read position)

        internal Cursor(ReplayableSequence<T> owner, int index) => (_owner, _index) = (owner, index);

        /// <summary>Current (next) index (0-based) inside the sequence.</summary>
        public int Position => _index;

        /// <summary>Create an independent cursor at the same position.</summary>
        public Cursor Fork() => new(_owner, _index);

        /// <summary>Try to read the next element. Returns an advanced cursor when successful.</summary>
        /// <param name="value">The next value when present.</param>
        /// <param name="next">The advanced cursor (only valid when result is true).</param>
        /// <returns><see langword="true"/> when an element was available; otherwise <see langword="false"/>.</returns>
        public bool TryNext(out T value, out Cursor next)
        {
            if (_owner.EnsureBuffered(_index))
            {
                value = _owner._buffer[_index];
                next = new Cursor(_owner, _index + 1);
                return true;
            }

            value = default!;
            next = this;
            return false;
        }

        /// <summary>Peek the next element without advancing.</summary>
        public bool Peek(out T value)
        {
            if (_owner.EnsureBuffered(_index))
            {
                value = _owner._buffer[_index];
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>Look ahead (offset >= 0) without advancing. Returns an <see cref="Option{T}"/>.</summary>
        public Option<T> Lookahead(int offset)
        {
            Throw.IfNegative(offset);
            var idx = _index + offset;
            return _owner.EnsureBuffered(idx) ? Option<T>.Some(_owner._buffer[idx]) : Option<T>.None();
        }

        /// <summary>
        /// Enumerate from this position forward as an <see cref="IEnumerable{T}"/>. The original cursor
        /// is not advanced (a copy is enumerated).
        /// </summary>
        public IEnumerable<T> AsEnumerable()
        {
            var c = this; // snapshot
            while (c.TryNext(out var v, out var adv))
            {
                yield return v;
                c = adv;
            }
        }
    }
}

/// <summary>
/// LINQ-like and utility extensions over <see cref="ReplayableSequence{T}.Cursor"/>.
/// </summary>
public static class ReplayableSequenceExtensions
{
    /// <summary>Project elements from a cursor using <paramref name="selector"/>.</summary>
    public static IEnumerable<TOut> Select<T, TOut>(
        this ReplayableSequence<T>.Cursor cursor,
        Func<T, TOut> selector)
        => cursor.AsEnumerable().Select(selector);

    /// <summary>Filter elements from a cursor using <paramref name="predicate"/>.</summary>
    public static IEnumerable<T> Where<T>(
        this ReplayableSequence<T>.Cursor cursor, 
        Func<T, bool> predicate)
        => cursor.AsEnumerable().Where(predicate);

    /// <summary>Batch elements into fixed-size chunks (last batch may be smaller).</summary>
    public static IEnumerable<IReadOnlyList<T>> Batch<T>(this ReplayableSequence<T>.Cursor cursor, int size)
    {
        Throw.IfNegativeOrZero(size);
        var batch = new List<T>(size);
        foreach (var item in cursor.AsEnumerable())
        {
            batch.Add(item);
            if (batch.Count != size)
                continue;
            
            yield return batch.ToArray(); // immutable snapshot
            batch.Clear();
        }

        if (batch.Count > 0)
            yield return batch.ToArray();
    }
}