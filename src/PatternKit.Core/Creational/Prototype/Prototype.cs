using System.Collections.ObjectModel;

namespace PatternKit.Creational.Prototype;

/// <summary>
/// Fluent, low-overhead prototype that clones a configured <typeparamref name="T"/> using a supplied <see cref="Cloner"/>,
/// then applies optional default mutations. Immutable after <see cref="Builder.Build"/>.
/// </summary>
/// <typeparam name="T">The prototype type to clone.</typeparam>
public sealed class Prototype<T>
{
    /// <summary>Delegate that clones a source instance into a new instance.</summary>
    public delegate T Cloner(in T source);

    private readonly T _source;
    private readonly Cloner _cloner;
    private readonly Action<T>? _mutations;

    private Prototype(T source, Cloner cloner, Action<T>? mutations)
        => (_source, _cloner, _mutations) = (source, cloner, mutations);

    /// <summary>Create a new builder from a <paramref name="source"/> and <paramref name="cloner"/>.</summary>
    public static Builder Create(T source, Cloner cloner) => new(source, cloner);

    /// <summary>Clone the configured prototype and apply default mutations (if any).</summary>
    public T Create()
    {
        var clone = _cloner(in _source);
        _mutations?.Invoke(clone);
        return clone;
    }

    /// <summary>Clone and then apply both default mutations and an extra, per-call <paramref name="mutate"/>.</summary>
    public T Create(Action<T>? mutate)
    {
        var clone = _cloner(in _source);
        _mutations?.Invoke(clone);
        mutate?.Invoke(clone);
        return clone;
    }

    /// <summary>Fluent builder for <see cref="Prototype{T}"/>.</summary>
    public sealed class Builder
    {
        private readonly T _source;
        private readonly Cloner _cloner;
        private Action<T>? _mutations;

        internal Builder(T source, Cloner cloner) => (_source, _cloner) = (source, cloner);

        /// <summary>Add a default mutation to apply to every clone.</summary>
        public Builder With(Action<T> mutate)
        {
            _mutations = _mutations is null ? mutate : (Action<T>)Delegate.Combine(_mutations, mutate);
            return this;
        }

        /// <summary>Build an immutable prototype instance.</summary>
        public Prototype<T> Build() => new(_source, _cloner, _mutations);
    }
}

/// <summary>
/// Prototype registry that maps a <typeparamref name="TKey"/> to a (prototype, cloner, mutations) family. Immutable after build.
/// </summary>
/// <typeparam name="TKey">Key used to select a family (e.g., enum, string).</typeparam>
/// <typeparam name="T">Prototype type.</typeparam>
public sealed class Prototype<TKey, T> where TKey : notnull
{
    /// <summary>Delegate that clones a source instance into a new instance.</summary>
    public delegate T Cloner(in T source);

    private readonly IReadOnlyDictionary<TKey, Family> _families;
    private readonly Family _default;
    private readonly bool _hasDefault;

    private readonly struct Family(
        T source,
        Cloner clone,
        Action<T>? mutations
    )
    {
        public readonly T Source = source;
        public readonly Cloner Clone = clone;
        public readonly Action<T>? Mutations = mutations;

        public T Create()
        {
            var obj = Clone(in Source);
            Mutations?.Invoke(obj);
            return obj;
        }

        public T Create(Action<T>? mutate)
        {
            var obj = Clone(in Source);
            Mutations?.Invoke(obj);
            mutate?.Invoke(obj);
            return obj;
        }

        public Family WithMutation(Action<T> m)
            => new(Source, Clone, Mutations is null ? m : (Action<T>)Delegate.Combine(Mutations, m));
    }

    private Prototype(IReadOnlyDictionary<TKey, Family> families, bool hasDefault, Family @default)
        => (_families, _hasDefault, _default) = (families, hasDefault, @default);

    /// <summary>Create a new builder; pass a key comparer for custom key semantics.</summary>
    public static Builder Create(IEqualityComparer<TKey>? comparer = null) => new(comparer ?? EqualityComparer<TKey>.Default);

    /// <summary>Create a clone for <paramref name="key"/>; throws if missing and no default exists.</summary>
    public T Create(TKey key)
    {
        if (_families.TryGetValue(key, out var fam)) return fam.Create();
        if (_hasDefault) return _default.Create();
        ThrowNoFamily(key);
        return default!;
    }

    /// <summary>Create a clone and apply a per-call <paramref name="mutate"/>.</summary>
    public T Create(TKey key, Action<T>? mutate)
    {
        if (_families.TryGetValue(key, out var fam)) return fam.Create(mutate);
        if (_hasDefault) return _default.Create(mutate);
        ThrowNoFamily(key);
        return default!;
    }

    /// <summary>Try to create a clone for <paramref name="key"/>.</summary>
    public bool TryCreate(TKey key, out T value)
    {
        if (_families.TryGetValue(key, out var fam))
        {
            value = fam.Create();
            return true;
        }

        if (_hasDefault)
        {
            value = _default.Create();
            return true;
        }

        value = default!;
        return false;
    }

    private static void ThrowNoFamily(TKey key)
        => throw new InvalidOperationException($"No prototype family for key '{key}'. Configure Map(...) or Default(...).");

    /// <summary>Fluent builder for the registry prototype.</summary>
    public sealed class Builder
    {
        private readonly Dictionary<TKey, Family> _map;
        private Family? _default;

        internal Builder(IEqualityComparer<TKey> comparer) => _map = new(comparer);

        /// <summary>Register or replace a prototype family for <paramref name="key"/>.</summary>
        public Builder Map(TKey key, T source, Cloner cloner)
        {
            _map[key] = new Family(source, cloner, mutations: null);
            return this;
        }

        /// <summary>Add or append a mutation to the family under <paramref name="key"/>.</summary>
        public Builder Mutate(TKey key, Action<T> mutate)
        {
            if (_map.TryGetValue(key, out var fam))
                _map[key] = fam.WithMutation(mutate);
            else
                _map[key] = new Family(source: default(T)!, static (in _) => default(T)!, mutations: mutate);
            return this;
        }

        /// <summary>Set or replace the default prototype family.</summary>
        public Builder Default(T source, Cloner cloner)
        {
            _default = new Family(source, cloner, mutations: null);
            return this;
        }

        /// <summary>Add or append a default mutation.</summary>
        public Builder DefaultMutate(Action<T> mutate)
        {
            _default = _default is { } fam ? fam.WithMutation(mutate) : new Family(default!, static (in _) => default!, mutate);
            return this;
        }

        /// <summary>Build an immutable registry snapshot.</summary>
        public Prototype<TKey, T> Build()
        {
            // Validate: ensure families without source/cloner are not left half-configured
            foreach (var item in _map.ToArray())
            {
                if (Equals(item.Value.Source, default(T)) || item.Value.Clone is null)
                    throw new InvalidOperationException($"Map(key, source, cloner) must be called before Mutate for key '{item.Key}'.");
            }

            var ro = new ReadOnlyDictionary<TKey, Family>(new Dictionary<TKey, Family>(_map, _map.Comparer));
            var hasDefault = _default is { } df && !Equals(df.Source, default(T));
            var def = hasDefault ? _default : new Family(default!, static (in _) => ThrowBeforeDefault(), null);
            return new Prototype<TKey, T>(ro, hasDefault, def ?? throw new InvalidOperationException("No Default() configured."));

            static T ThrowBeforeDefault() => throw new InvalidOperationException("No Default() configured.");
        }
    }
}