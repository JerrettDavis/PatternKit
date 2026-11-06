namespace PatternKit.Messaging.Transformation;

/// <summary>
/// Outcome returned by a message translator.
/// </summary>
public sealed class MessageTranslationResult<TPayload>
{
    private MessageTranslationResult(Message<TPayload>? message, bool translated, Exception? exception)
    {
        Message = message;
        Translated = translated;
        Exception = exception;
    }

    public Message<TPayload>? Message { get; }
    public bool Translated { get; }
    public bool Failed => !Translated;
    public Exception? Exception { get; }

    public static MessageTranslationResult<TPayload> Success(Message<TPayload> message)
        => new(message ?? throw new ArgumentNullException(nameof(message)), true, null);

    public static MessageTranslationResult<TPayload> Failure(Exception exception)
        => new(null, false, exception ?? throw new ArgumentNullException(nameof(exception)));
}

/// <summary>
/// Translates one message contract into another while applying explicit header policies.
/// </summary>
public sealed class MessageTranslator<TInput, TOutput>
{
    public delegate TOutput Translator(Message<TInput> message, MessageContext context);
    public delegate MessageHeaders HeaderPolicy(MessageHeaders headers);

    private readonly Translator _translator;
    private readonly HeaderPolicy _headerPolicy;

    private MessageTranslator(string name, Translator translator, HeaderPolicy headerPolicy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Message translator name is required.", nameof(name));

        Name = name;
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _headerPolicy = headerPolicy ?? throw new ArgumentNullException(nameof(headerPolicy));
    }

    public string Name { get; }

    public static Builder Create(string name = "message-translator") => new(name);

    public MessageTranslationResult<TOutput> Translate(Message<TInput> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        try
        {
            var payload = _translator(message, effectiveContext);
            if (payload is null)
                return MessageTranslationResult<TOutput>.Failure(new InvalidOperationException("Message translator returned a null payload."));

            var headers = _headerPolicy(message.Headers);
            if (headers is null)
                return MessageTranslationResult<TOutput>.Failure(new InvalidOperationException("Message translator header policy returned null."));

            return MessageTranslationResult<TOutput>.Success(new Message<TOutput>(payload, headers));
        }
        catch (Exception ex)
        {
            return MessageTranslationResult<TOutput>.Failure(ex);
        }
    }

    public async ValueTask<MessageTranslationResult<TOutput>> TranslateAsync(
        Message<TInput> message,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await new ValueTask<MessageTranslationResult<TOutput>>(Translate(message, context)).ConfigureAwait(false);
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<HeaderPolicy> _policies = [];
        private Translator? _translator;
        private bool _preserveHeaders = true;

        internal Builder(string name) => _name = name;

        public Builder TranslateWith(Translator translator)
        {
            _translator = translator ?? throw new ArgumentNullException(nameof(translator));
            return this;
        }

        public Builder PreserveHeaders(bool preserve = true)
        {
            _preserveHeaders = preserve;
            return this;
        }

        public Builder DropHeader(string name)
        {
            RequireHeaderName(name);
            _policies.Add(headers => headers.Without(name));
            return this;
        }

        public Builder KeepHeaders(params string[] names)
        {
            if (names is null)
                throw new ArgumentNullException(nameof(names));

            var allowed = new HashSet<string>(names.Select(RequireHeaderName), StringComparer.OrdinalIgnoreCase);
            _policies.Add(headers => new MessageHeaders(headers.Where(pair => allowed.Contains(pair.Key))));
            return this;
        }

        public Builder SetHeader(string name, object? value)
        {
            RequireHeaderName(name);
            _policies.Add(headers => headers.With(name, value));
            return this;
        }

        public Builder ConfigureHeaders(HeaderPolicy policy)
        {
            _policies.Add(policy ?? throw new ArgumentNullException(nameof(policy)));
            return this;
        }

        public MessageTranslator<TInput, TOutput> Build()
        {
            if (_translator is null)
                throw new InvalidOperationException("Message translator delegate is required.");

            return new(_name, _translator, ApplyPolicies);
        }

        private MessageHeaders ApplyPolicies(MessageHeaders headers)
        {
            var current = _preserveHeaders ? headers : MessageHeaders.Empty;
            foreach (var policy in _policies)
                current = policy(current) ?? throw new InvalidOperationException("Message translator header policy returned null.");

            return current;
        }

        private static string RequireHeaderName(string name)
            => string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Header name is required.", nameof(name))
                : name;
    }
}
