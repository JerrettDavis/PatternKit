namespace PatternKit.Behavioral.Iterator;

/// <summary>
/// Sliding / striding window iterator over an <see cref="IEnumerable{T}"/> that yields immutable (or buffer-reused) window views.
/// Demonstrates creating a custom enumerator with additional semantics while still presenting standard IEnumerable API.
/// </summary>
/// <remarks>
/// <para>Use <see cref="Windows{T}(IEnumerable{T}, int, int, bool, bool)"/> to produce fixed-size windows with an optional stride and partial inclusion.</para>
/// <para><b>Design goals:</b> clarity and extensibility over micro-optimizations; showcases custom enumerator + reusable buffer option.</para>
/// </remarks>
public static class WindowSequence
{
    /// <summary>
    /// Produces a sequence of sliding (or striding) windows from <paramref name="source"/>.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Underlying source sequence (enumerated exactly once).</param>
    /// <param name="size">Window size (&gt; 0).</param>
    /// <param name="stride">Elements to advance between windows (default 1; must be &gt; 0).</param>
    /// <param name="includePartial">When true, a trailing window smaller than <paramref name="size"/> is yielded.</param>
    /// <param name="reuseBuffer">When true, the same underlying array is reused for each full window (call <see cref="Window{T}.ToArray"/> if you need a snapshot).</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="Window{T}"/> values.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> or <paramref name="stride"/> are not positive.</exception>
    /// <remarks>
    /// <para><b>Reuse buffer trade-off:</b> Setting <paramref name="reuseBuffer"/> reduces allocations for hot paths; consumers MUST copy data they intend to keep.</para>
    /// <para>The implementation is intentionally allocation-light (queue nodes only) and simple; a ring buffer would be faster but more complex.</para>
    /// </remarks>
    public static IEnumerable<Window<T>> Windows<T>(
        this IEnumerable<T> source,
        int size,
        int stride = 1,
        bool includePartial = false,
        bool reuseBuffer = false)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));

        return Enumerate(); // deferred

        IEnumerable<Window<T>> Enumerate()
        {
            using var e = source.GetEnumerator();
            var queue = new Queue<T>(size);
            var buffer = reuseBuffer ? new T[size] : null;

            // Prime initial window
            while (queue.Count < size && e.MoveNext())
                queue.Enqueue(e.Current);

            if (queue.Count == size)
            {
                while (true)
                {
                    yield return MakeWindow(queue, size, partial: false);

                    // advance by stride (drop elements or empty queue)
                    for (var i = 0; i < stride && queue.Count > 0; i++)
                        queue.Dequeue();

                    // refill
                    while (queue.Count < size && e.MoveNext())
                        queue.Enqueue(e.Current);

                    if (queue.Count < size) break; // done with full windows
                }
            }

            if (includePartial && queue.Count > 0)
                yield return MakeWindow(queue, queue.Count, partial: true);

            yield break;

            // local factory
            Window<T> MakeWindow(Queue<T> q, int count, bool partial)
            {
                if (reuseBuffer && buffer is not null && !partial && count == size)
                {
                    q.CopyTo(buffer, 0);
                    return new Window<T>(buffer, count, partial, reusable: true);
                }
                var arr = new T[count];
                q.CopyTo(arr, 0);
                return new Window<T>(arr, count, partial, reusable: false);
            }
        }
    }

    /// <summary>
    /// Represents a fixed-size (or trailing partial) window of elements.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    public readonly struct Window<T>
    {
        private readonly T[] _buffer;
        /// <summary>Number of meaningful elements in the window.</summary>
        public int Count { get; }
        /// <summary>True when this is a trailing partial (smaller than requested size).</summary>
        public bool IsPartial { get; }
        /// <summary>True when the underlying buffer is reused across windows (caller must copy).</summary>
        public bool IsBufferReused { get; }

        internal Window(T[] buffer, int count, bool partial, bool reusable)
        {
            _buffer = buffer;
            Count = count;
            IsPartial = partial;
            IsBufferReused = reusable;
        }

        /// <summary>Indexed element access (0-based).</summary>
        public T this[int index]
            => (uint)index < (uint)Count ? _buffer[index] : throw new ArgumentOutOfRangeException(nameof(index));

        /// <summary>
        /// Materializes the window as a fresh array (always copies to guarantee immutability even when buffer reused).
        /// </summary>
        public T[] ToArray()
        {
            var copy = new T[Count];
            Array.Copy(_buffer, 0, copy, 0, Count);
            return copy;
        }

        /// <summary>Enumerates the elements in this window (snapshot view—may reflect later changes if buffer is reused).</summary>
        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < Count; i++) yield return _buffer[i];
        }
    }
}
