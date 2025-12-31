namespace PatternKit.Structural.Bridge;

/// <summary>
/// Async Bridge that separates an <b>Abstraction</b> (pre/post/validate) from an async <b>Implementation</b>
/// (provider + async operation). Build once, then call <see cref="ExecuteAsync"/> or <see cref="TryExecuteAsync"/>.
/// </summary>
/// <typeparam name="TIn">The input type.</typeparam>
/// <typeparam name="TOut">The result type produced by the operation.</typeparam>
/// <typeparam name="TImpl">The implementation type (e.g., device, driver, renderer).</typeparam>
/// <remarks>
/// <para>
/// This is the async counterpart to <see cref="Bridge{TIn,TOut,TImpl}"/>.
/// Use when the operation or hooks need to perform async work.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var bridge = AsyncBridge&lt;Query, Result, IDbConnection&gt;
///     .Create(async ct => await connectionPool.GetConnectionAsync(ct))
///     .Operation(async (query, conn, ct) => await conn.ExecuteAsync(query, ct))
///     .Before(async (query, conn, ct) => await conn.OpenAsync(ct))
///     .After(async (query, conn, result, ct) => { await LogQueryAsync(query, ct); return result; })
///     .Build();
///
/// var result = await bridge.ExecuteAsync(new Query("SELECT * FROM Users"));
/// </code>
/// </example>
public sealed class AsyncBridge<TIn, TOut, TImpl>
{
    /// <summary>Async provider delegate for obtaining the implementation.</summary>
    public delegate ValueTask<TImpl> Provider(CancellationToken ct);

    /// <summary>Async provider delegate that depends on input.</summary>
    public delegate ValueTask<TImpl> ProviderFrom(TIn input, CancellationToken ct);

    /// <summary>Async operation delegate.</summary>
    public delegate ValueTask<TOut> Operation(TIn input, TImpl impl, CancellationToken ct);

    /// <summary>Async pre-operation hook.</summary>
    public delegate ValueTask Pre(TIn input, TImpl impl, CancellationToken ct);

    /// <summary>Async post-operation hook that can transform the result.</summary>
    public delegate ValueTask<TOut> Post(TIn input, TImpl impl, TOut result, CancellationToken ct);

    /// <summary>Async validator delegate.</summary>
    public delegate ValueTask<string?> Validate(TIn input, TImpl impl, CancellationToken ct);

    /// <summary>Async result validator delegate.</summary>
    public delegate ValueTask<string?> ValidateResult(TIn input, TImpl impl, TOut result, CancellationToken ct);

    private readonly Provider? _provider;
    private readonly ProviderFrom? _providerFrom;
    private readonly Operation _operation;
    private readonly Pre[] _pres;
    private readonly Post[] _posts;
    private readonly Validate[] _validators;
    private readonly ValidateResult[] _resultValidators;

    private AsyncBridge(
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
        _operation = operation;
        _pres = pres;
        _posts = posts;
        _validators = validators;
        _resultValidators = resultValidators;
    }

    /// <summary>Execute with exceptions on first validation failure.</summary>
    public async ValueTask<TOut> ExecuteAsync(TIn input, CancellationToken ct = default)
    {
        var impl = _provider is not null
            ? await _provider(ct).ConfigureAwait(false)
            : await _providerFrom!(input, ct).ConfigureAwait(false);

        foreach (var v in _validators)
        {
            var msg = await v(input, impl, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(msg))
                throw new InvalidOperationException(msg);
        }

        foreach (var pre in _pres)
            await pre(input, impl, ct).ConfigureAwait(false);

        var result = await _operation(input, impl, ct).ConfigureAwait(false);

        foreach (var post in _posts)
            result = await post(input, impl, result, ct).ConfigureAwait(false);

        foreach (var v in _resultValidators)
        {
            var msg = await v(input, impl, result, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(msg))
                throw new InvalidOperationException(msg);
        }

        return result;
    }

    /// <summary>Try-execute without throwing; returns success status and error message.</summary>
    public async ValueTask<(bool success, TOut? result, string? error)> TryExecuteAsync(TIn input, CancellationToken ct = default)
    {
        try
        {
            var impl = _provider is not null
                ? await _provider(ct).ConfigureAwait(false)
                : await _providerFrom!(input, ct).ConfigureAwait(false);

            foreach (var v in _validators)
            {
                var msg = await v(input, impl, ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(msg))
                    return (false, default, msg);
            }

            foreach (var pre in _pres)
                await pre(input, impl, ct).ConfigureAwait(false);

            var result = await _operation(input, impl, ct).ConfigureAwait(false);

            foreach (var post in _posts)
                result = await post(input, impl, result, ct).ConfigureAwait(false);

            foreach (var v in _resultValidators)
            {
                var msg = await v(input, impl, result, ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(msg))
                    return (false, default, msg);
            }

            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, default, ex.Message);
        }
    }

    /// <summary>Create a builder with an async implementation provider.</summary>
    public static Builder Create(Provider provider) => new(provider, providerFrom: null);

    /// <summary>Create a builder with an async implementation provider that depends on input.</summary>
    public static Builder Create(ProviderFrom providerFrom) => new(provider: null, providerFrom);

    /// <summary>Create a builder with a sync implementation provider.</summary>
    public static Builder Create(Func<TImpl> provider) =>
        new(_ => new ValueTask<TImpl>(provider()), providerFrom: null);

    /// <summary>Fluent builder for <see cref="AsyncBridge{TIn, TOut, TImpl}"/>.</summary>
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
            if (_provider is null && _providerFrom is null)
                throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Set the async core operation.</summary>
        public Builder Operation(Operation op)
        {
            _operation = op;
            return this;
        }

        /// <summary>Set a sync core operation.</summary>
        public Builder Operation(Func<TIn, TImpl, TOut> op)
        {
            _operation = (input, impl, _) => new ValueTask<TOut>(op(input, impl));
            return this;
        }

        /// <summary>Add an async pre-operation hook.</summary>
        public Builder Before(Pre hook)
        {
            _pres.Add(hook);
            return this;
        }

        /// <summary>Add a sync pre-operation hook.</summary>
        public Builder Before(Action<TIn, TImpl> hook)
        {
            _pres.Add((input, impl, _) =>
            {
                hook(input, impl);
                return default;
            });
            return this;
        }

        /// <summary>Add an async post-operation hook.</summary>
        public Builder After(Post hook)
        {
            _posts.Add(hook);
            return this;
        }

        /// <summary>Add a sync post-operation hook.</summary>
        public Builder After(Func<TIn, TImpl, TOut, TOut> hook)
        {
            _posts.Add((input, impl, result, _) => new ValueTask<TOut>(hook(input, impl, result)));
            return this;
        }

        /// <summary>Add an async validator before the operation.</summary>
        public Builder Require(Validate v)
        {
            _validators.Add(v);
            return this;
        }

        /// <summary>Add a sync validator before the operation.</summary>
        public Builder Require(Func<TIn, TImpl, string?> v)
        {
            _validators.Add((input, impl, _) => new ValueTask<string?>(v(input, impl)));
            return this;
        }

        /// <summary>Add an async validator after the operation.</summary>
        public Builder RequireResult(ValidateResult v)
        {
            _resultValidators.Add(v);
            return this;
        }

        /// <summary>Add a sync validator after the operation.</summary>
        public Builder RequireResult(Func<TIn, TImpl, TOut, string?> v)
        {
            _resultValidators.Add((input, impl, result, _) => new ValueTask<string?>(v(input, impl, result)));
            return this;
        }

        /// <summary>Build an immutable async bridge.</summary>
        public AsyncBridge<TIn, TOut, TImpl> Build()
        {
            if (_operation is null)
                throw new InvalidOperationException("Operation(...) must be configured.");
            return new AsyncBridge<TIn, TOut, TImpl>(
                _provider, _providerFrom, _operation,
                _pres.ToArray(), _posts.ToArray(), _validators.ToArray(), _resultValidators.ToArray());
        }
    }
}
