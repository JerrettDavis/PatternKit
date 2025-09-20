using System.Collections.ObjectModel;

namespace PatternKit.Creational.Factory;

/// <summary>
/// Low-overhead, immutable factory: map a <typeparamref name="TKey"/> to a constructor <see cref="Creator"/>.
/// Built once, then safe for concurrent use. Lookups are O(1) via a read-only <see cref="IReadOnlyDictionary{TKey,TValue}"/>.
/// </summary>
/// <typeparam name="TKey">Key used to select a creator (e.g., string, enum).</typeparam>
/// <typeparam name="TOut">The constructed product type.</typeparam>
public sealed class Factory<TKey, TOut> where TKey : notnull
{
    /// <summary>Creator delegate that constructs the given type.</summary>
    public delegate TOut Creator();

    private readonly IReadOnlyDictionary<TKey, Creator> _creators;
    private readonly Creator _default;
    private readonly bool _hasDefault;

    private Factory(IReadOnlyDictionary<TKey, Creator> creators, bool hasDefault, Creator @default)
        => (_creators, _hasDefault, _default) = (creators, hasDefault, @default);

    /// <summary>
    /// Creates a new <see cref="Builder"/>. Provide an optional <paramref name="comparer"/> for key semantics
    /// (e.g., <see cref="StringComparer.OrdinalIgnoreCase"/>).
    /// </summary>
    public static Builder Create(IEqualityComparer<TKey>? comparer = null) => new(comparer ?? EqualityComparer<TKey>.Default);

    /// <summary>Create an instance for <paramref name="key"/>; uses Default if not found; throws if no Default.</summary>
    public TOut Create(TKey key)
    {
        if (_creators.TryGetValue(key, out var ctor)) return ctor();
        if (_hasDefault) return _default();
        ThrowNoMapping(key);
        return default!; // unreachable
    }

    /// <summary>Try to create an instance for <paramref name="key"/>. Returns false only if no mapping and no default.</summary>
    public bool TryCreate(TKey key, out TOut value)
    {
        if (_creators.TryGetValue(key, out var ctor))
        {
            value = ctor();
            return true;
        }

        if (_hasDefault)
        {
            value = _default();
            return true;
        }

        value = default!;
        return false;
    }

    private static void ThrowNoMapping(TKey key)
        => throw new InvalidOperationException($"No factory mapping for key '{key}'. Configure a mapping or Default().");

    /// <summary>Fluent builder for <see cref="Factory{TKey,TOut}"/>.</summary>
    public sealed class Builder
    {
        private readonly Dictionary<TKey, Creator> _map;
        private Creator? _default;

        internal Builder(IEqualityComparer<TKey> comparer) => _map = new(comparer);

        /// <summary>Add or replace the mapping for <paramref name="key"/>.</summary>
        public Builder Map(TKey key, Creator creator)
        {
            _map[key] = creator; // last wins
            return this;
        }

        /// <summary>Set or replace the default creator used when no key mapping exists.</summary>
        public Builder Default(Creator creator)
        {
            _default = creator;
            return this;
        }

        /// <summary>Build an immutable factory snapshot.</summary>
        public Factory<TKey, TOut> Build()
        {
            var readOnly = new ReadOnlyDictionary<TKey, Creator>(new Dictionary<TKey, Creator>(_map, _map.Comparer));
            var hasDefault = _default is not null;
            var def = _default ?? (static () => ThrowBeforeDefault());
            return new Factory<TKey, TOut>(readOnly, hasDefault, def);

            static TOut ThrowBeforeDefault() => throw new InvalidOperationException("No Default() configured.");
        }
    }
}

/// <summary>
/// Low-overhead, immutable factory with an input context: map <typeparamref name="TKey"/> to a constructor taking
/// <typeparamref name="TIn"/> and producing <typeparamref name="TOut"/>.
/// </summary>
/// <typeparam name="TKey">Key used to select a creator (e.g., string, enum).</typeparam>
/// <typeparam name="TIn">Input context passed by <c>in</c> to the creator (kept as a readonly argument).</typeparam>
/// <typeparam name="TOut">The constructed product type.</typeparam>
public sealed class Factory<TKey, TIn, TOut> where TKey : notnull
{
    /// <summary>Creator delegate that constructs an object from an input context.</summary>
    public delegate TOut Creator(in TIn input);

    private readonly IReadOnlyDictionary<TKey, Creator> _creators;
    private readonly Creator _default;
    private readonly bool _hasDefault;

    private Factory(IReadOnlyDictionary<TKey, Creator> creators, bool hasDefault, Creator @default)
        => (_creators, _hasDefault, _default) = (creators, hasDefault, @default);

    /// <summary>
    /// Creates a new <see cref="Builder"/>. Provide an optional <paramref name="comparer"/> for key semantics
    /// (e.g., <see cref="StringComparer.OrdinalIgnoreCase"/>).
    /// </summary>
    public static Builder Create(IEqualityComparer<TKey>? comparer = null) => new(comparer ?? EqualityComparer<TKey>.Default);

    /// <summary>Create an instance for <paramref name="key"/> using <paramref name="input"/>. Uses Default if not found; throws if no Default.</summary>
    public TOut Create(TKey key, in TIn input)
    {
        if (_creators.TryGetValue(key, out var ctor)) return ctor(in input);
        if (_hasDefault) return _default(in input);
        ThrowNoMapping(key);
        return default!; // unreachable
    }

    /// <summary>Try to create an instance for <paramref name="key"/> using <paramref name="input"/>. Returns false only if no mapping and no default.</summary>
    public bool TryCreate(TKey key, in TIn input, out TOut value)
    {
        if (_creators.TryGetValue(key, out var ctor))
        {
            value = ctor(in input);
            return true;
        }

        if (_hasDefault)
        {
            value = _default(in input);
            return true;
        }

        value = default!;
        return false;
    }

    private static void ThrowNoMapping(TKey key)
        => throw new InvalidOperationException($"No factory mapping for key '{key}'. Configure a mapping or Default().");

    /// <summary>Fluent builder for <see cref="Factory{TKey,TIn,TOut}"/>.</summary>
    public sealed class Builder
    {
        private readonly Dictionary<TKey, Creator> _map;
        private Creator? _default;

        internal Builder(IEqualityComparer<TKey> comparer) => _map = new(comparer);

        /// <summary>Add or replace the mapping for <paramref name="key"/>.</summary>
        public Builder Map(TKey key, Creator creator)
        {
            _map[key] = creator; // last wins
            return this;
        }

        /// <summary>Set or replace the default creator used when no key mapping exists.</summary>
        public Builder Default(Creator creator)
        {
            _default = creator;
            return this;
        }

        /// <summary>Build an immutable factory snapshot.</summary>
        public Factory<TKey, TIn, TOut> Build()
        {
            var readOnly = new ReadOnlyDictionary<TKey, Creator>(new Dictionary<TKey, Creator>(_map, _map.Comparer));
            var hasDefault = _default is not null;
            var def = _default ?? (static (in _) => ThrowBeforeDefault());
            return new Factory<TKey, TIn, TOut>(readOnly, hasDefault, def);

            static TOut ThrowBeforeDefault() => throw new InvalidOperationException("No Default() configured.");
        }
    }
}