namespace PatternKit.Structural.Bridge;

/// <summary>
/// Async Action Bridge that separates an <b>Abstraction</b> from an async <b>Implementation</b>
/// for void-returning async operations. Build once, then call <see cref="ExecuteAsync"/> or <see cref="TryExecuteAsync"/>.
/// </summary>
/// <typeparam name="TIn">The input type.</typeparam>
/// <typeparam name="TImpl">The implementation type (e.g., device, driver, renderer).</typeparam>
/// <remarks>
/// <para>
/// This combines features of <see cref="AsyncBridge{TIn,TOut,TImpl}"/> and <see cref="ActionBridge{TIn,TImpl}"/>
/// for async void-returning operations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var bridge = AsyncActionBridge&lt;Message, IMessageQueue&gt;
///     .Create(async ct => await queueFactory.GetQueueAsync(ct))
///     .Operation(async (msg, queue, ct) => await queue.PublishAsync(msg, ct))
///     .Before(async (msg, queue, ct) => await queue.EnsureConnectedAsync(ct))
///     .Build();
///
/// await bridge.ExecuteAsync(new Message("Hello"));
/// </code>
/// </example>
public sealed class AsyncActionBridge<TIn, TImpl>
{
    /// <summary>Async provider delegate for obtaining the implementation.</summary>
    public delegate ValueTask<TImpl> Provider(CancellationToken ct);

    /// <summary>Async provider delegate that depends on input.</summary>
    public delegate ValueTask<TImpl> ProviderFrom(TIn input, CancellationToken ct);

    /// <summary>Async action operation delegate.</summary>
    public delegate ValueTask Operation(TIn input, TImpl impl, CancellationToken ct);

    /// <summary>Async pre-operation hook.</summary>
    public delegate ValueTask Pre(TIn input, TImpl impl, CancellationToken ct);

    /// <summary>Async post-operation hook.</summary>
    public delegate ValueTask Post(TIn input, TImpl impl, CancellationToken ct);

    /// <summary>Async validator delegate.</summary>
    public delegate ValueTask<string?> Validate(TIn input, TImpl impl, CancellationToken ct);

    private readonly Provider? _provider;
    private readonly ProviderFrom? _providerFrom;
    private readonly Operation _operation;
    private readonly Pre[] _pres;
    private readonly Post[] _posts;
    private readonly Validate[] _validators;

    private AsyncActionBridge(
        Provider? provider,
        ProviderFrom? providerFrom,
        Operation operation,
        Pre[] pres,
        Post[] posts,
        Validate[] validators)
    {
        _provider = provider;
        _providerFrom = providerFrom;
        _operation = operation;
        _pres = pres;
        _posts = posts;
        _validators = validators;
    }

    /// <summary>Execute with exceptions on first validation failure.</summary>
    public async ValueTask ExecuteAsync(TIn input, CancellationToken ct = default)
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

        await _operation(input, impl, ct).ConfigureAwait(false);

        foreach (var post in _posts)
            await post(input, impl, ct).ConfigureAwait(false);
    }

    /// <summary>Try-execute without throwing; returns success status and error message.</summary>
    public async ValueTask<(bool success, string? error)> TryExecuteAsync(TIn input, CancellationToken ct = default)
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
                    return (false, msg);
            }

            foreach (var pre in _pres)
                await pre(input, impl, ct).ConfigureAwait(false);

            await _operation(input, impl, ct).ConfigureAwait(false);

            foreach (var post in _posts)
                await post(input, impl, ct).ConfigureAwait(false);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Create a builder with an async implementation provider.</summary>
    public static Builder Create(Provider provider) => new(provider, providerFrom: null);

    /// <summary>Create a builder with an async implementation provider that depends on input.</summary>
    public static Builder Create(ProviderFrom providerFrom) => new(provider: null, providerFrom);

    /// <summary>Create a builder with a sync implementation provider.</summary>
    public static Builder Create(Func<TImpl> provider) =>
        new(_ => new ValueTask<TImpl>(provider()), providerFrom: null);

    /// <summary>Fluent builder for <see cref="AsyncActionBridge{TIn, TImpl}"/>.</summary>
    public sealed class Builder
    {
        private readonly Provider? _provider;
        private readonly ProviderFrom? _providerFrom;
        private Operation? _operation;
        private readonly List<Pre> _pres = new(4);
        private readonly List<Post> _posts = new(2);
        private readonly List<Validate> _validators = new(2);

        internal Builder(Provider? provider, ProviderFrom? providerFrom)
        {
            _provider = provider;
            _providerFrom = providerFrom;
            if (_provider is null && _providerFrom is null)
                throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Set the async action operation.</summary>
        public Builder Operation(Operation op)
        {
            _operation = op;
            return this;
        }

        /// <summary>Set a sync action operation.</summary>
        public Builder Operation(Action<TIn, TImpl> op)
        {
            _operation = (input, impl, _) =>
            {
                op(input, impl);
                return default;
            };
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
        public Builder After(Action<TIn, TImpl> hook)
        {
            _posts.Add((input, impl, _) =>
            {
                hook(input, impl);
                return default;
            });
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

        /// <summary>Build an immutable async action bridge.</summary>
        public AsyncActionBridge<TIn, TImpl> Build()
        {
            if (_operation is null)
                throw new InvalidOperationException("Operation(...) must be configured.");
            return new AsyncActionBridge<TIn, TImpl>(
                _provider, _providerFrom, _operation,
                _pres.ToArray(), _posts.ToArray(), _validators.ToArray());
        }
    }
}
