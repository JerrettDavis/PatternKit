namespace PatternKit.Structural.Bridge;

/// <summary>
/// Action Bridge that separates an <b>Abstraction</b> (pre/post/validate) from an <b>Implementation</b>
/// for void-returning operations. Build once, then call <see cref="Execute"/> or <see cref="TryExecute"/>.
/// </summary>
/// <typeparam name="TIn">The input type.</typeparam>
/// <typeparam name="TImpl">The implementation type (e.g., device, driver, renderer).</typeparam>
/// <remarks>
/// <para>
/// This is the action (void-returning) counterpart to <see cref="Bridge{TIn,TOut,TImpl}"/>.
/// Use when the operation produces side effects rather than values.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var bridge = ActionBridge&lt;LogEntry, ILogger&gt;
///     .Create(() => loggerFactory.GetLogger())
///     .Operation((entry, logger) => logger.Log(entry.Level, entry.Message))
///     .Before((entry, logger) => logger.BeginScope(entry.Scope))
///     .Build();
///
/// bridge.Execute(new LogEntry("Info", "Hello", "Main"));
/// </code>
/// </example>
public sealed class ActionBridge<TIn, TImpl>
{
    /// <summary>Provider delegate for obtaining the implementation.</summary>
    public delegate TImpl Provider();

    /// <summary>Provider delegate that depends on input.</summary>
    public delegate TImpl ProviderFrom(in TIn input);

    /// <summary>Action operation delegate.</summary>
    public delegate void Operation(in TIn input, TImpl impl);

    /// <summary>Pre-operation hook.</summary>
    public delegate void Pre(in TIn input, TImpl impl);

    /// <summary>Post-operation hook.</summary>
    public delegate void Post(in TIn input, TImpl impl);

    /// <summary>Validator delegate.</summary>
    public delegate string? Validate(in TIn input, TImpl impl);

    private readonly Provider? _provider;
    private readonly ProviderFrom? _providerFrom;
    private readonly Operation _operation;
    private readonly Pre[] _pres;
    private readonly Post[] _posts;
    private readonly Validate[] _validators;

    private ActionBridge(
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
    public void Execute(in TIn input)
    {
        var impl = _provider is not null ? _provider() : _providerFrom!(in input);

        foreach (var v in _validators)
        {
            var msg = v(in input, impl);
            if (!string.IsNullOrEmpty(msg))
                throw new InvalidOperationException(msg);
        }

        foreach (var pre in _pres)
            pre(in input, impl);

        _operation(in input, impl);

        foreach (var post in _posts)
            post(in input, impl);
    }

    /// <summary>Try-execute without throwing; returns success status and error message.</summary>
    public bool TryExecute(in TIn input, out string? error)
    {
        try
        {
            var impl = _provider is not null ? _provider() : _providerFrom!(in input);

            foreach (var v in _validators)
            {
                var msg = v(in input, impl);
                if (!string.IsNullOrEmpty(msg))
                {
                    error = msg;
                    return false;
                }
            }

            foreach (var pre in _pres)
                pre(in input, impl);

            _operation(in input, impl);

            foreach (var post in _posts)
                post(in input, impl);

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Create a builder with an implementation provider.</summary>
    public static Builder Create(Provider provider) => new(provider, providerFrom: null);

    /// <summary>Create a builder with an implementation provider that depends on input.</summary>
    public static Builder Create(ProviderFrom providerFrom) => new(provider: null, providerFrom);

    /// <summary>Fluent builder for <see cref="ActionBridge{TIn, TImpl}"/>.</summary>
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

        /// <summary>Set the core action operation.</summary>
        public Builder Operation(Operation op)
        {
            _operation = op;
            return this;
        }

        /// <summary>Add a pre-operation hook.</summary>
        public Builder Before(Pre hook)
        {
            _pres.Add(hook);
            return this;
        }

        /// <summary>Add a post-operation hook.</summary>
        public Builder After(Post hook)
        {
            _posts.Add(hook);
            return this;
        }

        /// <summary>Add a validator before the operation.</summary>
        public Builder Require(Validate v)
        {
            _validators.Add(v);
            return this;
        }

        /// <summary>Build an immutable action bridge.</summary>
        public ActionBridge<TIn, TImpl> Build()
        {
            if (_operation is null)
                throw new InvalidOperationException("Operation(...) must be configured.");
            return new ActionBridge<TIn, TImpl>(
                _provider, _providerFrom, _operation,
                _pres.ToArray(), _posts.ToArray(), _validators.ToArray());
        }
    }
}
