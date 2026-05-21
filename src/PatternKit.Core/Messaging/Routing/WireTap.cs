namespace PatternKit.Messaging.Routing;

/// <summary>
/// Wire tap that observes messages with named side-channel handlers while preserving the original message.
/// </summary>
public sealed class WireTap<TPayload>
{
    /// <summary>Handler invoked for each tapped message.</summary>
    public delegate void TapHandler(Message<TPayload> message, MessageContext context);

    private readonly string _name;
    private readonly Tap[] _taps;

    private WireTap(string name, Tap[] taps)
        => (_name, _taps) = (name, taps);

    /// <summary>Publishes <paramref name="message"/> to all taps and returns the unchanged message.</summary>
    public WireTapResult<TPayload> Publish(Message<TPayload> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var invoked = new string[_taps.Length];
        for (var i = 0; i < _taps.Length; i++)
        {
            _taps[i].Handler(message, effectiveContext);
            invoked[i] = _taps[i].Name;
        }

        return new WireTapResult<TPayload>(message, _name, invoked);
    }

    /// <summary>Creates a new wire-tap builder.</summary>
    public static Builder Create(string name = "wire-tap") => new(name);

    /// <summary>Fluent builder for <see cref="WireTap{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<Tap> _taps = new(4);

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Wire tap name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>Adds a named side-channel tap.</summary>
        public Builder AddTap(string name, TapHandler handler)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Wire tap handler name cannot be null, empty, or whitespace.", nameof(name));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            _taps.Add(new Tap(name, handler));
            return this;
        }

        /// <summary>Builds an immutable wire tap.</summary>
        public WireTap<TPayload> Build()
        {
            if (_taps.Count == 0)
                throw new InvalidOperationException("Wire tap must have at least one tap handler.");

            return new WireTap<TPayload>(_name, _taps.ToArray());
        }
    }

    private sealed class Tap
    {
        public Tap(string name, TapHandler handler)
            => (Name, Handler) = (name, handler);

        public string Name { get; }

        public TapHandler Handler { get; }
    }
}

/// <summary>
/// Result returned by <see cref="WireTap{TPayload}"/>.
/// </summary>
public sealed class WireTapResult<TPayload>
{
    public WireTapResult(Message<TPayload> message, string tapName, IReadOnlyList<string> invokedTaps)
        => (Message, TapName, InvokedTaps) = (message, tapName, invokedTaps);

    /// <summary>The unchanged message that was observed.</summary>
    public Message<TPayload> Message { get; }

    /// <summary>The wire-tap name.</summary>
    public string TapName { get; }

    /// <summary>The ordered tap handlers invoked for the message.</summary>
    public IReadOnlyList<string> InvokedTaps { get; }
}
