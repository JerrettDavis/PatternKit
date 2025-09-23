#if !NETSTANDARD2_0
using System.Runtime.CompilerServices;
using PatternKit.Common;

namespace PatternKit.Behavioral.Iterator;

/// <summary>
/// Async counterpart to <see cref="Flow{T}"/> built on <see cref="IAsyncEnumerable{T}"/>.
/// Provides Map / Filter / FlatMap / Tee and a replayable <see cref="Share"/> that allows
/// multiple forks to enumerate without re-running upstream side-effects.
/// </summary>
public sealed class AsyncFlow<T> : IAsyncEnumerable<T>
{
    private readonly Func<IAsyncEnumerable<T>> _factory;
    private AsyncFlow(Func<IAsyncEnumerable<T>> factory) => _factory = factory;

    public static AsyncFlow<T> From(IAsyncEnumerable<T> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return new AsyncFlow<T>(() => source);
    }

    public AsyncFlow<TOut> Map<TOut>(Func<T, TOut> selector)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return new AsyncFlow<TOut>(() => Core());

        async IAsyncEnumerable<TOut> Core([EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var v in _factory().WithCancellation(ct).ConfigureAwait(false))
                yield return selector(v);
        }
    }

    public AsyncFlow<T> Filter(Func<T, bool> predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return new AsyncFlow<T>(() => Core());

        async IAsyncEnumerable<T> Core([EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var v in _factory().WithCancellation(ct).ConfigureAwait(false))
                if (predicate(v)) yield return v;
        }
    }

    public AsyncFlow<TOut> FlatMap<TOut>(Func<T, IAsyncEnumerable<TOut>> selector)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return new AsyncFlow<TOut>(() => Core());

        async IAsyncEnumerable<TOut> Core([EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var v in _factory().WithCancellation(ct).ConfigureAwait(false))
            {
                await foreach (var inner in selector(v).WithCancellation(ct).ConfigureAwait(false))
                    yield return inner;
            }
        }
    }

    public AsyncFlow<T> Tee(Action<T> effect)
    {
        if (effect is null) throw new ArgumentNullException(nameof(effect));
        return new AsyncFlow<T>(() => Core());

        async IAsyncEnumerable<T> Core([EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var v in _factory().WithCancellation(ct).ConfigureAwait(false))
            {
                effect(v);
                yield return v;
            }
        }
    }

    public SharedAsyncFlow<T> Share() => new(AsyncReplayBuffer<T>.Create(_factory()));

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => _factory().GetAsyncEnumerator(cancellationToken);
}

/// <summary>
/// Thread-safe async replay buffer that enumerates the upstream exactly once and serves buffered elements
/// to any number of concurrent consumers.
/// </summary>
internal sealed class AsyncReplayBuffer<T>
{
    private readonly IAsyncEnumerator<T> _source;
    private readonly List<T> _buffer = new(64);
    private bool _completed;
    private Exception? _error;
    private readonly object _sync = new();
    private readonly List<TaskCompletionSource<bool>> _waiters = new();

    private AsyncReplayBuffer(IAsyncEnumerator<T> source) => _source = source;

    public static AsyncReplayBuffer<T> Create(IAsyncEnumerable<T> source)
        => new(source.GetAsyncEnumerator());

    public async ValueTask<bool> TryGetAsync(int index, CancellationToken ct)
    {
        if (index < 0) return false;
        while (true)
        {
            TaskCompletionSource<bool>? waiter = null;
            lock (_sync)
            {
                if (index < _buffer.Count)
                    return true;
                if (_completed)
                    return false;
                waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add(waiter);
            }

            bool shouldProduce = false;
            lock (_sync)
            {
                if (_waiters.Count == 1 && !_completed)
                    shouldProduce = true;
            }

            if (shouldProduce)
            {
                try
                {
                    if (await _source.MoveNextAsync().ConfigureAwait(false))
                    {
                        lock (_sync)
                        {
                            _buffer.Add(_source.Current);
                            var ws = _waiters.ToArray();
                            _waiters.Clear();
                            foreach (var w in ws) w.TrySetResult(true);
                        }
                        continue;
                    }

                    await DisposeSourceAsync().ConfigureAwait(false);
                    lock (_sync)
                    {
                        _completed = true;
                        var ws = _waiters.ToArray();
                        _waiters.Clear();
                        foreach (var w in ws) w.TrySetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    await DisposeSourceAsync().ConfigureAwait(false);
                    lock (_sync)
                    {
                        _completed = true;
                        _error = ex;
                        var ws = _waiters.ToArray();
                        _waiters.Clear();
                        foreach (var w in ws) w.TrySetException(ex);
                    }
                }
            }

            using var reg = ct.Register(static s => ((TaskCompletionSource<bool>)s!).TrySetCanceled(), waiter);
            var signaled = await waiter.Task.ConfigureAwait(false);
            if (!signaled)
                return false;
        }
    }

    private async ValueTask DisposeSourceAsync()
    {
        try
        {
            await _source.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // swallow dispose errors
        }
    }

    public T Get(int index)
    {
        lock (_sync)
        {
            if (index < _buffer.Count) return _buffer[index];
            if (_error is not null) throw _error;
            throw new InvalidOperationException("Element not buffered yet.");
        }
    }
}

public sealed class SharedAsyncFlow<T>
{
    private readonly AsyncReplayBuffer<T> _buffer;
    internal SharedAsyncFlow(AsyncReplayBuffer<T> buffer) => _buffer = buffer;

    public AsyncFlow<T> Fork()
        => AsyncFlow<T>.From(Enumerate());

    public (AsyncFlow<T> True, AsyncFlow<T> False) Branch(Func<T, bool> predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return (
            AsyncFlow<T>.From(Filter(true)),
            AsyncFlow<T>.From(Filter(false))
        );

        async IAsyncEnumerable<T> Filter(bool polarity, [EnumeratorCancellation] CancellationToken ct = default)
        {
            int i = 0;
            while (await _buffer.TryGetAsync(i, ct).ConfigureAwait(false))
            {
                var v = _buffer.Get(i);
                if (predicate(v) == polarity) yield return v;
                i++;
            }
        }
    }

    private async IAsyncEnumerable<T> Enumerate([EnumeratorCancellation] CancellationToken ct = default)
    {
        int i = 0;
        while (await _buffer.TryGetAsync(i, ct).ConfigureAwait(false))
        {
            yield return _buffer.Get(i);
            i++;
        }
    }
}

public static class AsyncFlowExtensions
{
    public static async ValueTask<TAcc> FoldAsync<T, TAcc>(this AsyncFlow<T> flow, TAcc seed, Func<TAcc, T, TAcc> folder, CancellationToken ct = default)
    {
        if (flow is null) throw new ArgumentNullException(nameof(flow));
        if (folder is null) throw new ArgumentNullException(nameof(folder));
        var acc = seed;
        await foreach (var v in flow.WithCancellation(ct).ConfigureAwait(false))
            acc = folder(acc, v);
        return acc;
    }

    public static async ValueTask<Option<T>> FirstOptionAsync<T>(this AsyncFlow<T> flow, CancellationToken ct = default)
    {
        if (flow is null) throw new ArgumentNullException(nameof(flow));
        await foreach (var v in flow.WithCancellation(ct).ConfigureAwait(false))
            return Option<T>.Some(v);
        return Option<T>.None();
    }
}
#endif
