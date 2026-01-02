namespace PatternKit.Structural.Adapter;

/// <summary>
/// Async Adapter that maps an input <typeparamref name="TIn"/> to an output <typeparamref name="TOut"/>
/// via ordered, async mapping steps and optional validations.
/// Build once, then call <see cref="AdaptAsync"/> or <see cref="TryAdaptAsync"/>.
/// </summary>
/// <typeparam name="TIn">Input type to adapt from.</typeparam>
/// <typeparam name="TOut">Output type to adapt to.</typeparam>
/// <remarks>
/// <para>
/// This is the async counterpart to <see cref="Adapter{TIn,TOut}"/>.
/// Use when mapping steps need to perform async work (API calls, database lookups, etc.).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var adapter = AsyncAdapter&lt;ExternalOrder, InternalOrder&gt;
///     .Create(async (ext, ct) => new InternalOrder { Id = ext.Id })
///     .Map(async (ext, dest, ct) => dest.Customer = await LookupCustomerAsync(ext.CustomerId, ct))
///     .Map(async (ext, dest, ct) => dest.Items = await MapItemsAsync(ext.Items, ct))
///     .Require(async (ext, dest, ct) => dest.Items.Any() ? null : "Order must have items")
///     .Build();
///
/// var internalOrder = await adapter.AdaptAsync(externalOrder);
/// </code>
/// </example>
public sealed class AsyncAdapter<TIn, TOut>
{
    /// <summary>Async seed delegate that creates the destination object.</summary>
    public delegate ValueTask<TOut> Seed(CancellationToken ct);

    /// <summary>Async seed delegate that creates the destination from input.</summary>
    public delegate ValueTask<TOut> SeedFrom(TIn input, CancellationToken ct);

    /// <summary>Async mapping step delegate.</summary>
    public delegate ValueTask MapStep(TIn input, TOut output, CancellationToken ct);

    /// <summary>Async validator delegate.</summary>
    public delegate ValueTask<string?> Validator(TIn input, TOut output, CancellationToken ct);

    private readonly Seed? _seed;
    private readonly SeedFrom? _seedFrom;
    private readonly MapStep[] _maps;
    private readonly Validator[] _validators;

    private AsyncAdapter(Seed? seed, SeedFrom? seedFrom, MapStep[] maps, Validator[] validators)
    {
        _seed = seed;
        _seedFrom = seedFrom;
        _maps = maps;
        _validators = validators;
    }

    /// <summary>
    /// Create an output instance from input asynchronously, run mapping steps, then run validations.
    /// Throws <see cref="InvalidOperationException"/> on the first validation failure.
    /// </summary>
    public async ValueTask<TOut> AdaptAsync(TIn input, CancellationToken ct = default)
    {
        var dest = _seed is not null
            ? await _seed(ct).ConfigureAwait(false)
            : await _seedFrom!(input, ct).ConfigureAwait(false);

        foreach (var map in _maps)
            await map(input, dest, ct).ConfigureAwait(false);

        foreach (var v in _validators)
        {
            var msg = await v(input, dest, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(msg))
                throw new InvalidOperationException(msg);
        }

        return dest;
    }

    /// <summary>
    /// Like <see cref="AdaptAsync"/>, but returns success status and error message instead of throwing.
    /// </summary>
    public async ValueTask<(bool success, TOut? output, string? error)> TryAdaptAsync(TIn input, CancellationToken ct = default)
    {
        try
        {
            var dest = _seed is not null
                ? await _seed(ct).ConfigureAwait(false)
                : await _seedFrom!(input, ct).ConfigureAwait(false);

            foreach (var map in _maps)
                await map(input, dest, ct).ConfigureAwait(false);

            foreach (var v in _validators)
            {
                var msg = await v(input, dest, ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(msg))
                    return (false, default, msg);
            }

            return (true, dest, null);
        }
        catch (Exception ex)
        {
            return (false, default, ex.Message);
        }
    }

    /// <summary>Create a builder with an async parameterless seed.</summary>
    public static Builder Create(Seed seed) => new(seed, seedFrom: null);

    /// <summary>Create a builder with an async seed that depends on input.</summary>
    public static Builder Create(SeedFrom seedFrom) => new(seed: null, seedFrom);

    /// <summary>Create a builder with a sync parameterless seed.</summary>
    public static Builder Create(Func<TOut> seed) =>
        new(_ => new ValueTask<TOut>(seed()), seedFrom: null);

    /// <summary>Create a builder with a sync seed that depends on input.</summary>
    public static Builder Create(Func<TIn, TOut> seedFrom) =>
        new(seed: null, (input, _) => new ValueTask<TOut>(seedFrom(input)));

    /// <summary>Fluent builder for <see cref="AsyncAdapter{TIn,TOut}"/>.</summary>
    public sealed class Builder
    {
        private readonly Seed? _seed;
        private readonly SeedFrom? _seedFrom;
        private readonly List<MapStep> _maps = new(8);
        private readonly List<Validator> _validators = new(4);

        internal Builder(Seed? seed, SeedFrom? seedFrom)
        {
            _seed = seed;
            _seedFrom = seedFrom;
            if (_seed is null && _seedFrom is null)
                throw new ArgumentNullException(nameof(seed));
        }

        /// <summary>Append an async mapping step that mutates the destination based on the input.</summary>
        public Builder Map(MapStep step)
        {
            _maps.Add(step);
            return this;
        }

        /// <summary>Append a sync mapping step.</summary>
        public Builder Map(Action<TIn, TOut> step)
        {
            _maps.Add((input, output, _) =>
            {
                step(input, output);
                return default;
            });
            return this;
        }

        /// <summary>Append an async validator; return a non-null/empty message to fail adaptation.</summary>
        public Builder Require(Validator validator)
        {
            _validators.Add(validator);
            return this;
        }

        /// <summary>Append a sync validator.</summary>
        public Builder Require(Func<TIn, TOut, string?> validator)
        {
            _validators.Add((input, output, _) => new ValueTask<string?>(validator(input, output)));
            return this;
        }

        /// <summary>Build an immutable async adapter.</summary>
        public AsyncAdapter<TIn, TOut> Build()
            => new(_seed, _seedFrom, _maps.ToArray(), _validators.ToArray());
    }
}
