namespace PatternKit.Structural.Adapter;

/// <summary>
/// Fluent, allocation-light adapter that maps an input <typeparamref name="TIn"/> to an output <typeparamref name="TOut"/>
/// via ordered, side-effecting mapping steps and optional validations. Build once, then call <see cref="Adapt"/> or
/// <see cref="TryAdapt"/>.
/// </summary>
/// <typeparam name="TIn">Input type to adapt from.</typeparam>
/// <typeparam name="TOut">Output type to adapt to.</typeparam>
public sealed class Adapter<TIn, TOut>
{
    public delegate TOut Seed();

    public delegate TOut SeedFrom(in TIn input);

    public delegate void MapStep(in TIn input, TOut output);

    public delegate string? Validator(in TIn input, TOut output);

    private readonly Seed? _seed;
    private readonly SeedFrom? _seedFrom;
    private readonly MapStep[] _maps;
    private readonly Validator[] _validators;

    private Adapter(Seed? seed, SeedFrom? seedFrom, MapStep[] maps, Validator[] validators)
    {
        _seed = seed;
        _seedFrom = seedFrom;
        _maps = maps;
        _validators = validators;
    }

    /// <summary>
    /// Create an output instance from <paramref name="input"/>, run mapping steps in order, then run validations. Throws
    /// <see cref="InvalidOperationException"/> on the first validation failure.
    /// </summary>
    public TOut Adapt(in TIn input)
    {
        var dest = _seed is not null ? _seed() : _seedFrom!(in input);
        foreach (var t in _maps)
            t(in input, dest);

        foreach (var t in _validators)
        {
            var msg = t(in input, dest);
            if (!string.IsNullOrEmpty(msg)) throw new InvalidOperationException(msg);
        }

        return dest;
    }

    /// <summary>
    /// Like <see cref="Adapt"/>, but returns <c>false</c> with the error message instead of throwing. On failure, <paramref name="output"/>
    /// is set to <c>default</c>.
    /// </summary>
    public bool TryAdapt(in TIn input, out TOut output, out string? error)
    {
        TOut dest;
        try
        {
            dest = _seed is not null ? _seed() : _seedFrom!(in input);
            foreach (var t in _maps)
                t(in input, dest);

            foreach (var t in _validators)
            {
                var msg = t(in input, dest);
                if (string.IsNullOrEmpty(msg))
                    continue;
                output = default!;
                error = msg;
                return false;
            }
        }
        catch (Exception ex)
        {
            output = default!;
            error = ex.Message;
            return false;
        }

        output = dest;
        error = null;
        return true;
    }

    /// <summary>Create a builder with a parameterless seed for the destination.</summary>
    public static Builder Create(Seed seed) => new(seed, seedFrom: null);

    /// <summary>Create a builder with a seed that depends on the input value.</summary>
    public static Builder Create(SeedFrom seedFrom) => new(seed: null, seedFrom);

    /// <summary>Fluent builder for <see cref="Adapter{TIn,TOut}"/>.</summary>
    public sealed class Builder
    {
        private readonly Seed? _seed;
        private readonly SeedFrom? _seedFrom;
        private readonly List<MapStep> _maps = new(8);
        private readonly List<Validator> _validators = new(4);

        internal Builder(Seed? seed, SeedFrom? seedFrom)
        {
            _seed = seed ?? null;
            _seedFrom = seedFrom ?? null;
            if (_seed is null && _seedFrom is null) throw new ArgumentNullException(nameof(seed));
        }

        /// <summary>Append a mapping step that mutates the destination based on the input.</summary>
        public Builder Map(MapStep step)
        {
            _maps.Add(step);
            return this;
        }

        /// <summary>Append a validator; return a non-null/empty message to fail adaptation.</summary>
        public Builder Require(Validator validator)
        {
            _validators.Add(validator);
            return this;
        }

        /// <summary>Build an immutable adapter with snapshot arrays of steps and validators.</summary>
        public Adapter<TIn, TOut> Build()
            => new(_seed, _seedFrom, _maps.ToArray(), _validators.ToArray());
    }
}