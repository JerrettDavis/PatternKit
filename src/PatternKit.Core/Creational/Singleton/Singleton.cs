namespace PatternKit.Creational.Singleton;

/// <summary>
/// Fluent, low-overhead, thread-safe singleton for <typeparamref name="T"/>. Configure a factory and optional
/// init mutations; choose lazy (default) or eager creation; then access via <see cref="Instance"/>.
/// </summary>
/// <typeparam name="T">The instance type.</typeparam>
public sealed class Singleton<T>
{
    /// <summary>Factory delegate used to create the singleton instance.</summary>
    public delegate T Factory();

    private readonly Factory _factory;
    private readonly Action<T>? _init;

    private T _value = default!;
    private bool _created; // guarded via Volatile.Read/Write
    private readonly object _sync = new();

    private Singleton(Factory factory, Action<T>? init, bool eager)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _init = init;

        if (!eager)
            return;

        // Eagerly create and initialize once during construction
        var v = _factory();
        _init?.Invoke(v);
        _value = v;
        Volatile.Write(ref _created, true);
    }

    /// <summary>Gets the singleton instance, creating it on first access when configured for lazy creation.</summary>
    public T Instance => Volatile.Read(ref _created) ? _value : CreateSlow();

    private T CreateSlow()
    {
        if (Volatile.Read(ref _created)) return _value; // re-check
        lock (_sync)
        {
            if (_created)
                return _value;

            var v = _factory();
            _init?.Invoke(v);
            _value = v;
            Volatile.Write(ref _created, true);

            return _value;
        }
    }

    /// <summary>Create a builder with the required <paramref name="factory"/>.</summary>
    public static Builder Create(Factory factory) => new(factory);

    /// <summary>Fluent builder for <see cref="Singleton{T}"/>.</summary>
    public sealed class Builder
    {
        private readonly Factory _factory;
        private Action<T>? _init;
        private bool _eager;

        internal Builder(Factory factory) => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        /// <summary>Add a default initialization action applied exactly once when the instance is created.</summary>
        public Builder Init(Action<T> initializer)
        {
            _init = _init is null ? initializer : (Action<T>)Delegate.Combine(_init, initializer);
            return this;
        }

        /// <summary>Create the instance eagerly during <see cref="Build"/> instead of on first access.</summary>
        public Builder Eager()
        {
            _eager = true;
            return this;
        }

        /// <summary>Build an immutable, thread-safe singleton wrapper.</summary>
        public Singleton<T> Build() => new(_factory, _init, _eager);
    }
}