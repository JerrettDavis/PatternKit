namespace PatternKit.Structural.Bridge;

/// <summary>
/// Fluent, allocation-light Bridge that separates an <b>Abstraction</b> (pre/post/validate) from an <b>Implementation</b>
/// (provider + operation). Build once, then call <see cref="Execute"/> or <see cref="TryExecute"/>.
/// </summary>
/// <typeparam name="TIn">The input passed by <c>in</c> to avoid copies for structs.</typeparam>
/// <typeparam name="TOut">The result type produced by the operation.</typeparam>
/// <typeparam name="TImpl">The implementation type (e.g., device, driver, renderer).</typeparam>
public sealed class Bridge<TIn, TOut, TImpl>
{
    // Implementation side
    public delegate TImpl Provider();

    public delegate TImpl ProviderFrom(in TIn input);

    public delegate TOut Operation(in TIn input, TImpl impl);

    // Abstraction side hooks & rules
    public delegate void Pre(in TIn input, TImpl impl);

    public delegate TOut Post(in TIn input, TImpl impl, TOut result);

    public delegate string? Validate(in TIn input, TImpl impl);

    public delegate string? ValidateResult(in TIn input, TImpl impl, in TOut result);

    private readonly Provider? _provider;
    private readonly ProviderFrom? _providerFrom;
    private readonly Operation _operation;
    private readonly Pre[] _pres;
    private readonly Post[] _posts;
    private readonly Validate[] _validators;
    private readonly ValidateResult[] _resultValidators;

    private Bridge(
        Provider? provider,
        ProviderFrom? providerFrom,
        Operation operation,
        Pre[] pres,
        Post[] posts,
        Validate[] validators,
        ValidateResult[] resultValidators)
    {
        _provider = provider;
        _providerFrom = providerFrom;
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        _pres = pres;
        _posts = posts;
        _validators = validators;
        _resultValidators = resultValidators;
    }

    /// <summary>Execute with exceptions on first validation failure.</summary>
    public TOut Execute(in TIn input)
    {
        var impl = _provider is not null ? _provider() : _providerFrom!(in input);

        // Validate before doing work
        foreach (var t in _validators)
        {
            var msg = t(in input, impl);
            if (!string.IsNullOrEmpty(msg)) throw new InvalidOperationException(msg);
        }

        // Pre hooks
        foreach (var t in _pres)
            t(in input, impl);

        // Operation
        var result = _operation(in input, impl);

        // Post hooks can transform the result
        foreach (var t in _posts)
            result = t(in input, impl, result);

        // Validate the result
        foreach (var t in _resultValidators)
        {
            var msg = t(in input, impl, in result);
            if (!string.IsNullOrEmpty(msg)) throw new InvalidOperationException(msg);
        }

        return result;
    }

    /// <summary>Try-execute without throwing; returns false and sets <paramref name="error"/> when a validation fails.</summary>
    public bool TryExecute(in TIn input, out TOut output, out string? error)
    {
        try
        {
            var impl = _provider is not null ? _provider() : _providerFrom!(in input);

            foreach (var t in _validators)
            {
                var msg = t(in input, impl);
                if (string.IsNullOrEmpty(msg))
                    continue;

                output = default!;
                error = msg;
                return false;
            }

            foreach (var t in _pres)
                t(in input, impl);

            var result = _operation(in input, impl);

            foreach (var t in _posts)
                result = t(in input, impl, result);

            foreach (var t in _resultValidators)
            {
                var msg = t(in input, impl, in result);
                if (string.IsNullOrEmpty(msg))
                    continue;
                
                output = default!;
                error = msg;
                return false;
            }

            output = result;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            output = default!;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Create a builder with an implementation provider.</summary>
    public static Builder Create(Provider provider) => new(provider, providerFrom: null);

    /// <summary>Create a builder with an implementation provider that depends on the input.</summary>
    public static Builder Create(ProviderFrom providerFrom) => new(provider: null, providerFrom);

    /// <summary>Fluent builder for <see cref="Bridge{TIn, TOut, TImpl}"/>.</summary>
    public sealed class Builder
    {
        private readonly Provider? _provider;
        private readonly ProviderFrom? _providerFrom;
        private Operation? _operation;
        private readonly List<Pre> _pres = new(4);
        private readonly List<Post> _posts = new(2);
        private readonly List<Validate> _validators = new(2);
        private readonly List<ValidateResult> _resultValidators = new(2);

        internal Builder(Provider? provider, ProviderFrom? providerFrom)
        {
            _provider = provider;
            _providerFrom = providerFrom;
            if (_provider is null && _providerFrom is null) throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Set the core operation that uses the implementation to produce a result.</summary>
        public Builder Operation(Operation op)
        {
            _operation = op;
            return this;
        }

        /// <summary>Add a pre-operation hook that can interact with the implementation (e.g., connect, warmup).</summary>
        public Builder Before(Pre hook)
        {
            _pres.Add(hook);
            return this;
        }

        /// <summary>Add a post-operation hook that can transform the result (e.g., wrap, annotate, log).</summary>
        public Builder After(Post hook)
        {
            _posts.Add(hook);
            return this;
        }

        /// <summary>Validate before the operation runs; non-empty string fails.</summary>
        public Builder Require(Validate v)
        {
            _validators.Add(v);
            return this;
        }

        /// <summary>Validate after the operation completes; non-empty string fails.</summary>
        public Builder RequireResult(ValidateResult v)
        {
            _resultValidators.Add(v);
            return this;
        }

        /// <summary>Build an immutable bridge wrapper.</summary>
        public Bridge<TIn, TOut, TImpl> Build()
        {
            if (_operation is null) throw new InvalidOperationException("Operation(...) must be configured.");
            return new Bridge<TIn, TOut, TImpl>(
                _provider, _providerFrom, _operation,
                _pres.ToArray(), _posts.ToArray(), _validators.ToArray(), _resultValidators.ToArray());
        }
    }
}