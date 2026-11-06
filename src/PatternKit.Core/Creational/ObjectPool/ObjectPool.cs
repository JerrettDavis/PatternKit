using System.Collections.Concurrent;

namespace PatternKit.Creational.ObjectPool;

/// <summary>
/// Thread-safe object pool for reusing expensive objects with explicit rent/return lease semantics.
/// </summary>
public sealed class ObjectPool<T> : IDisposable
{
    private readonly ConcurrentQueue<T> _items = new();
    private readonly object _gate = new();
    private readonly Func<T> _factory;
    private readonly Action<T>? _onRent;
    private readonly Action<T>? _onReturn;
    private readonly Func<T, bool>? _shouldReturn;
    private readonly int _maxRetained;
    private int _retained;
    private bool _disposed;

    private ObjectPool(Func<T> factory, Action<T>? onRent, Action<T>? onReturn, Func<T, bool>? shouldReturn, int maxRetained)
    {
        _factory = factory;
        _onRent = onRent;
        _onReturn = onReturn;
        _shouldReturn = shouldReturn;
        _maxRetained = maxRetained;
    }

    /// <summary>Creates a fluent pool builder.</summary>
    public static Builder Create() => new();

    /// <summary>Current number of retained instances available for future rents.</summary>
    public int RetainedCount => Volatile.Read(ref _retained);

    /// <summary>Rents an item from the pool. Dispose the returned lease to return the item.</summary>
    public ObjectPoolLease<T> Rent()
    {
        T value;
        var hasValue = false;
        lock (_gate)
        {
            ThrowIfDisposed();

            if (_items.TryDequeue(out var pooled))
            {
                Interlocked.Decrement(ref _retained);
                value = pooled;
                hasValue = true;
            }
            else
            {
                value = default!;
            }
        }

        if (!hasValue)
            value = _factory();

        var disposeAfterRent = false;
        lock (_gate)
        {
            if (_disposed)
                disposeAfterRent = true;
        }

        if (disposeAfterRent)
        {
            DisposeIfNeeded(value);
            throw new ObjectDisposedException(nameof(ObjectPool<T>));
        }

        _onRent?.Invoke(value);
        return new ObjectPoolLease<T>(this, value);
    }

    internal void Return(T value)
    {
        var disposeImmediately = false;
        lock (_gate)
        {
            disposeImmediately = _disposed;
        }

        if (disposeImmediately)
        {
            DisposeIfNeeded(value);
            return;
        }

        try
        {
            _onReturn?.Invoke(value);
            if (_shouldReturn is not null && !_shouldReturn(value))
            {
                DisposeIfNeeded(value);
                return;
            }
        }
        catch
        {
            DisposeIfNeeded(value);
            throw;
        }

        var dispose = false;
        lock (_gate)
        {
            if (_disposed)
            {
                dispose = true;
            }
            else
            {
                var retained = Interlocked.Increment(ref _retained);
                if (retained <= _maxRetained)
                {
                    _items.Enqueue(value);
                    return;
                }

                Interlocked.Decrement(ref _retained);
                dispose = true;
            }
        }

        if (dispose)
            DisposeIfNeeded(value);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var drained = new List<T>();
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            while (_items.TryDequeue(out var value))
            {
                Interlocked.Decrement(ref _retained);
                drained.Add(value);
            }
        }

        foreach (var value in drained)
            DisposeIfNeeded(value);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed))
            throw new ObjectDisposedException(nameof(ObjectPool<T>));
    }

    private static void DisposeIfNeeded(T value)
    {
        if (value is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>Fluent builder for <see cref="ObjectPool{T}"/>.</summary>
    public sealed class Builder
    {
        private Func<T> _factory = static () => Activator.CreateInstance<T>();
        private Action<T>? _onRent;
        private Action<T>? _onReturn;
        private Func<T, bool>? _shouldReturn;
        private int _maxRetained = Environment.ProcessorCount * 2;

        /// <summary>Configures the factory used when the pool has no retained item.</summary>
        public Builder WithFactory(Func<T> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        /// <summary>Configures a callback invoked whenever an item is rented.</summary>
        public Builder OnRent(Action<T> onRent)
        {
            _onRent = onRent ?? throw new ArgumentNullException(nameof(onRent));
            return this;
        }

        /// <summary>Configures a callback invoked before an item is returned to the retained pool.</summary>
        public Builder OnReturn(Action<T> onReturn)
        {
            _onReturn = onReturn ?? throw new ArgumentNullException(nameof(onReturn));
            return this;
        }

        /// <summary>Configures the predicate that decides whether a returned item should be retained.</summary>
        public Builder RetainWhen(Func<T, bool> shouldReturn)
        {
            _shouldReturn = shouldReturn ?? throw new ArgumentNullException(nameof(shouldReturn));
            return this;
        }

        /// <summary>Configures the maximum number of items retained by the pool.</summary>
        public Builder WithMaxRetained(int maxRetained)
        {
            if (maxRetained < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetained), maxRetained, "Max retained must be greater than or equal to zero.");

            _maxRetained = maxRetained;
            return this;
        }

        /// <summary>Builds the configured object pool.</summary>
        public ObjectPool<T> Build() => new(_factory, _onRent, _onReturn, _shouldReturn, _maxRetained);
    }
}

/// <summary>
/// Disposable lease that returns an object to its owning pool exactly once.
/// </summary>
public sealed class ObjectPoolLease<T> : IDisposable
{
    private ObjectPool<T>? _owner;

    internal ObjectPoolLease(ObjectPool<T> owner, T value)
    {
        _owner = owner;
        Value = value;
    }

    /// <summary>The rented value.</summary>
    public T Value { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        var owner = Interlocked.Exchange(ref _owner, null);
        owner?.Return(Value);
    }
}
