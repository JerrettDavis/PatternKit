namespace PatternKit.Messaging.Transformation;

/// <summary>
/// Result returned by <see cref="Normalizer{TRaw,TCanonical}.NormalizeAsync"/>.
/// </summary>
public sealed class NormalizerResult<TCanonical>
{
    private NormalizerResult(TCanonical? canonical, bool normalized, string? handlerName, string? missReason)
    {
        Canonical = canonical;
        Normalized = normalized;
        HandlerName = handlerName;
        MissReason = missReason;
    }

    /// <summary>The normalized canonical value when <see cref="Normalized"/> is true.</summary>
    public TCanonical? Canonical { get; }

    /// <summary>Whether normalization succeeded.</summary>
    public bool Normalized { get; }

    /// <summary>The name of the format handler that matched, if any.</summary>
    public string? HandlerName { get; }

    /// <summary>The reason normalization did not succeed, when <see cref="Normalized"/> is false.</summary>
    public string? MissReason { get; }

    internal static NormalizerResult<TCanonical> Success(TCanonical canonical, string handlerName)
        => new(canonical, true, handlerName, null);

    internal static NormalizerResult<TCanonical> Miss(string reason)
        => new(default, false, null, reason);
}

/// <summary>
/// Content-predicate dispatcher that normalizes a raw value into a canonical form.
/// Unlike <c>CanonicalDataModel</c>, format detection is content-based (predicate), not CLR-type-based.
/// Registration order determines priority; first matching format wins.
/// </summary>
/// <typeparam name="TRaw">The incoming raw type (e.g., <see langword="string"/>, <see langword="byte[]"/>).</typeparam>
/// <typeparam name="TCanonical">The target canonical type produced after normalization.</typeparam>
public sealed class Normalizer<TRaw, TCanonical>
{
    /// <summary>Content predicate that identifies a raw value's format.</summary>
    public delegate bool FormatPredicate(TRaw raw);

    /// <summary>Async normalizer handler for a specific format.</summary>
    public delegate ValueTask<TCanonical> AsyncNormalizerHandler(TRaw raw, CancellationToken cancellationToken);

    private readonly string _name;
    private readonly FormatEntry[] _entries;
    private readonly AsyncNormalizerHandler? _default;

    private Normalizer(string name, FormatEntry[] entries, AsyncNormalizerHandler? @default)
        => (_name, _entries, _default) = (name, entries, @default);

    /// <summary>Creates a new normalizer builder.</summary>
    public static Builder Create(string name = "normalizer") => new(name);

    /// <summary>
    /// Normalizes <paramref name="raw"/> by finding the first matching format predicate
    /// and invoking its async handler.
    /// </summary>
    public async ValueTask<NormalizerResult<TCanonical>> NormalizeAsync(
        TRaw raw,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var entry in _entries)
        {
            if (entry.Predicate(raw))
            {
                var canonical = await entry.Handler(raw, cancellationToken).ConfigureAwait(false);
                return NormalizerResult<TCanonical>.Success(canonical, entry.Name);
            }
        }

        if (_default is not null)
        {
            var canonical = await _default(raw, cancellationToken).ConfigureAwait(false);
            return NormalizerResult<TCanonical>.Success(canonical, "default");
        }

        return NormalizerResult<TCanonical>.Miss($"No format handler matched the raw input for normalizer '{_name}'.");
    }

    /// <summary>Fluent builder for <see cref="Normalizer{TRaw,TCanonical}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<FormatEntry> _entries = new(4);
        private AsyncNormalizerHandler? _default;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Normalizer name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>Begins a format clause with an optional name label.</summary>
        public WhenClause When(FormatPredicate predicate, string? label = null)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return new WhenClause(this, predicate, label ?? $"format-{_entries.Count + 1}");
        }

        /// <summary>Sets the default handler used when no format predicate matches.</summary>
        public Builder Default(AsyncNormalizerHandler handler)
        {
            _default = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        /// <summary>Builds an immutable normalizer.</summary>
        public Normalizer<TRaw, TCanonical> Build()
        {
            if (_entries.Count == 0 && _default is null)
                throw new InvalidOperationException("Normalizer requires at least one format handler or a default handler.");

            return new Normalizer<TRaw, TCanonical>(_name, _entries.ToArray(), _default);
        }

        internal Builder AddEntry(string name, FormatPredicate predicate, AsyncNormalizerHandler handler)
        {
            _entries.Add(new FormatEntry(name, predicate, handler));
            return this;
        }

        /// <summary>Fluent when-clause for chaining a normalizer handler.</summary>
        public sealed class WhenClause
        {
            private readonly Builder _builder;
            private readonly FormatPredicate _predicate;
            private readonly string _name;

            internal WhenClause(Builder builder, FormatPredicate predicate, string name)
                => (_builder, _predicate, _name) = (builder, predicate, name);

            /// <summary>Registers the async normalization handler for this format.</summary>
            public Builder Normalize(AsyncNormalizerHandler handler)
            {
                if (handler is null)
                    throw new ArgumentNullException(nameof(handler));

                return _builder.AddEntry(_name, _predicate, handler);
            }
        }
    }

    private sealed class FormatEntry
    {
        internal FormatEntry(string name, FormatPredicate predicate, AsyncNormalizerHandler handler)
            => (Name, Predicate, Handler) = (name, predicate, handler);

        internal string Name { get; }
        internal FormatPredicate Predicate { get; }
        internal AsyncNormalizerHandler Handler { get; }
    }
}
