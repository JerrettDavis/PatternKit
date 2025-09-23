using PatternKit.Common;

namespace PatternKit.Behavioral.Iterator;

/// <summary>
/// A fluent, functional pipeline ("flow") over an <see cref="IEnumerable{T}"/> supporting
/// transformation (<see cref="Map"/>), filtering (<see cref="Filter"/>), flattening (<see cref="FlatMap"/>),
/// side-effects (<see cref="Tee"/>), sharing + forking (<see cref="Share"/>, <see cref="SharedFlow{T}.Fork()"/>), and logical branching (<see cref="SharedFlow{T}.Branch"/>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Flow{T}"/> composes *lazy* LINQ-style transformations without immediately enumerating the
/// underlying source. Calling <see cref="Share"/> turns the pipeline into a <see cref="SharedFlow{T}"/>,
/// which materializes elements through a <see cref="ReplayableSequence{T}"/> only once and enables safe
/// multi-consumer forking + partitioning without re-enumerating upstream.
/// </para>
/// <para>
/// The goal is to demonstrate a custom iterator abstraction similar in spirit to an Rx <c>pipe</c>, but
/// with synchronous, pull-based semantics and explicit replay / branching control.
/// </para>
/// <para><b>Thread-safety:</b> Flows and shared flows are not thread-safe; confine to a single logical thread.</para>
/// </remarks>
public sealed class Flow<T> : IEnumerable<T>
{
    private readonly Func<IEnumerable<T>> _factory;

    private Flow(Func<IEnumerable<T>> factory) => _factory = factory;

    /// <summary>Create a flow from an existing source (no defensive copy; deferred).</summary>
    public static Flow<T> From(IEnumerable<T> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return new Flow<T>(() => source);
    }

    /// <summary>Map each element via <paramref name="selector"/>.</summary>
    public Flow<TOut> Map<TOut>(Func<T, TOut> selector)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return new Flow<TOut>(() => _factory().Select(selector));
    }

    /// <summary>Filter elements via <paramref name="predicate"/>.</summary>
    public Flow<T> Filter(Func<T, bool> predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return new Flow<T>(() => _factory().Where(predicate));
    }

    /// <summary>Flat-map each element to a (possibly empty) inner sequence.</summary>
    public Flow<TOut> FlatMap<TOut>(Func<T, IEnumerable<TOut>> selector)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return new Flow<TOut>(() => _factory().SelectMany(selector));
    }

    /// <summary>Run a side-effect for each element while preserving the element.</summary>
    public Flow<T> Tee(Action<T> effect)
    {
        if (effect is null) throw new ArgumentNullException(nameof(effect));
        return new Flow<T>(Iterate);

        IEnumerable<T> Iterate()
        {
            foreach (var v in _factory())
            {
                effect(v);
                yield return v;
            }
        }
    }

    /// <summary>Turn this flow into a shared replayable stream for safe forking / branching.</summary>
    public SharedFlow<T> Share() => new(ReplayableSequence<T>.From(_factory()));

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => _factory().GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// A shared, replayable flow (backed by <see cref="ReplayableSequence{T}"/>) enabling multi-consumer forking,
/// branching (partition), and additional functional transformation while guaranteeing each upstream element is
/// materialized at most once.
/// </summary>
public sealed class SharedFlow<T>
{
    private readonly ReplayableSequence<T> _sequence;

    internal SharedFlow(ReplayableSequence<T> sequence) => _sequence = sequence;

    /// <summary>Create a new fork (cursor snapshot) that behaves as an independent <see cref="Flow{T}"/>.</summary>
    public Flow<T> Fork()
        => Flow<T>.From(_sequence.GetCursor().AsEnumerable());

    /// <summary>Create <paramref name="count"/> forks at the current root.</summary>
    public Flow<T>[] Fork(int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var arr = new Flow<T>[count];
        for (int i = 0; i < count; i++) arr[i] = Fork();
        return arr;
    }

    /// <summary>
    /// Partition the stream into two flows based on <paramref name="predicate"/> without re-enumerating upstream.
    /// </summary>
    public (Flow<T> True, Flow<T> False) Branch(Func<T, bool> predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return (
            Flow<T>.From(FilterIter(true)),
            Flow<T>.From(FilterIter(false))
        );

        IEnumerable<T> FilterIter(bool polarity)
        {
            var cursor = _sequence.GetCursor();
            foreach (var item in cursor.AsEnumerable())
            {
                var pass = predicate(item);
                if (pass == polarity) yield return item;
            }
        }
    }

    /// <summary>Map values in a shared fashion (still uses replay cursor per enumeration).</summary>
    public Flow<TOut> Map<TOut>(Func<T, TOut> selector)
        => Fork().Map(selector);

    /// <summary>Filter values in a shared fashion.</summary>
    public Flow<T> Filter(Func<T, bool> predicate)
        => Fork().Filter(predicate);

    /// <summary>Expose the underlying enumeration (replayable) as a regular flow.</summary>
    public Flow<T> AsFlow() => Fork();
}

/// <summary>
/// Helper extensions for flow composition sugar / interop.
/// </summary>
public static class FlowExtensions
{
    /// <summary>
    /// Terminal reduce (aggregate) that folds the sequence into a single value.
    /// </summary>
    public static TAcc Fold<T, TAcc>(this Flow<T> flow, TAcc seed, Func<TAcc, T, TAcc> folder)
    {
        if (flow is null) throw new ArgumentNullException(nameof(flow));
        if (folder is null) throw new ArgumentNullException(nameof(folder));
        var acc = seed;
        foreach (var v in flow) acc = folder(acc, v);
        return acc;
    }

    /// <summary>Fold for a shared flow (fork first so the enumeration does not interfere with other consumers).</summary>
    public static TAcc Fold<T, TAcc>(this SharedFlow<T> flow, TAcc seed, Func<TAcc, T, TAcc> folder)
        => flow.AsFlow().Fold(seed, folder);

    /// <summary>Return the first value or default.</summary>
    public static T? FirstOrDefault<T>(this Flow<T> flow, Func<T, bool>? predicate = null)
    {
        if (flow is null) throw new ArgumentNullException(nameof(flow));
        predicate ??= static _ => true;
        foreach (var v in flow) if (predicate(v)) return v;
        return default;
    }

    /// <summary>Convert flow to <see cref="Option{T}"/> (first element).</summary>
    public static Option<T> FirstOption<T>(this Flow<T> flow)
    {
        if (flow is null) throw new ArgumentNullException(nameof(flow));
        var e = flow.GetEnumerator();
        return e.MoveNext() ? Option<T>.Some(e.Current) : Option<T>.None();
    }
}
