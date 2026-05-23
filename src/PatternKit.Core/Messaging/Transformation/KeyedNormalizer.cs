namespace PatternKit.Messaging.Transformation;

/// <summary>
/// Key-based dispatcher that normalizes a raw value into a canonical form using a dictionary lookup.
/// Unlike <see cref="Normalizer{TRaw,TCanonical}"/> (predicate-first), dispatch here is O(1) keyed
/// lookup — useful for format-tagged messages where the discriminator is known at call time
/// (e.g., content-type strings, routing keys, source system identifiers).
/// </summary>
/// <typeparam name="TKey">The key type used to select the handler. Must be non-nullable.</typeparam>
/// <typeparam name="TRaw">The incoming raw type (e.g., <see langword="string"/>, <see langword="byte[]"/>).</typeparam>
/// <typeparam name="TCanonical">The target canonical type produced after normalization.</typeparam>
public sealed class KeyedNormalizer<TKey, TRaw, TCanonical>
    where TKey : notnull
{
    /// <summary>Async normalizer handler for a specific key.</summary>
    public delegate ValueTask<TCanonical> AsyncNormalizerHandler(TRaw raw, CancellationToken cancellationToken);

    private readonly string _name;
    private readonly Dictionary<TKey, AsyncNormalizerHandler> _handlers;
    private readonly AsyncNormalizerHandler? _default;

    private KeyedNormalizer(
        string name,
        Dictionary<TKey, AsyncNormalizerHandler> handlers,
        AsyncNormalizerHandler? @default)
    {
        _name = name;
        _handlers = handlers;
        _default = @default;
    }

    /// <summary>Creates a new keyed normalizer builder.</summary>
    public static Builder Create(string name = "keyed-normalizer") => new(name);

    /// <summary>
    /// Normalizes <paramref name="raw"/> by dispatching to the handler registered for
    /// <paramref name="key"/>. Falls back to the default handler when no key match is found.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when <paramref name="key"/> has no registered handler and no default handler
    /// was configured.
    /// </exception>
    public ValueTask<TCanonical> NormalizeAsync(TKey key, TRaw raw, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_handlers.TryGetValue(key, out var handler))
            return handler(raw, ct);

        if (_default is not null)
            return _default(raw, ct);

        throw new KeyNotFoundException(
            $"No format handler matched the raw input for normalizer '{_name}'.");
    }

    /// <summary>Fluent builder for <see cref="KeyedNormalizer{TKey,TRaw,TCanonical}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private readonly Dictionary<TKey, AsyncNormalizerHandler> _entries;
        private AsyncNormalizerHandler? _default;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Keyed normalizer name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
            _entries = new Dictionary<TKey, AsyncNormalizerHandler>();
        }

        /// <summary>
        /// Registers a handler for <paramref name="key"/>. Throws
        /// <see cref="ArgumentException"/> when the same key is registered more than once —
        /// duplicate registrations are almost always a configuration mistake.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="key"/> is already registered.
        /// </exception>
        public Builder When(TKey key, Func<TRaw, CancellationToken, ValueTask<TCanonical>> handler)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            if (_entries.ContainsKey(key))
                throw new ArgumentException(
                    $"A handler for key '{key}' is already registered. Each key may only have one handler.",
                    nameof(key));

            _entries[key] = (raw, ct) => handler(raw, ct);
            return this;
        }

        /// <summary>Sets the default handler used when no key matches.</summary>
        public Builder Default(Func<TRaw, CancellationToken, ValueTask<TCanonical>> handler)
        {
            _default = (raw, ct) => (handler ?? throw new ArgumentNullException(nameof(handler)))(raw, ct);
            return this;
        }

        /// <summary>Builds an immutable keyed normalizer.</summary>
        public KeyedNormalizer<TKey, TRaw, TCanonical> Build()
        {
            if (_entries.Count == 0 && _default is null)
                throw new InvalidOperationException(
                    "KeyedNormalizer requires at least one key handler or a default handler.");

            // Snapshot the dictionary so the built instance is immutable-after-build.
            var snapshot = new Dictionary<TKey, AsyncNormalizerHandler>(_entries);
            return new KeyedNormalizer<TKey, TRaw, TCanonical>(_name, snapshot, _default);
        }
    }
}
