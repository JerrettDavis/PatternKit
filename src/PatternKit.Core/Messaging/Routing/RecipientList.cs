namespace PatternKit.Messaging.Routing;

/// <summary>
/// Recipient List pattern that dispatches a message to every matching recipient.
/// </summary>
public sealed class RecipientList<TPayload>
{
    /// <summary>Predicate used to decide whether a recipient should receive a message.</summary>
    public delegate bool RecipientPredicate(Message<TPayload> message, MessageContext context);

    /// <summary>Recipient handler invoked for matching recipients.</summary>
    public delegate void RecipientHandler(Message<TPayload> message, MessageContext context);

    private readonly Recipient[] _recipients;

    private RecipientList(Recipient[] recipients) => _recipients = recipients;

    /// <summary>
    /// Dispatches <paramref name="message"/> to every matching recipient in registration order.
    /// </summary>
    public RecipientListResult Dispatch(Message<TPayload> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var delivered = new List<string>(_recipients.Length);
        foreach (var recipient in _recipients)
        {
            if (!recipient.Predicate(message, effectiveContext))
                continue;

            recipient.Handler(message, effectiveContext);
            delivered.Add(recipient.Name);
        }

        return new RecipientListResult(delivered);
    }

    /// <summary>Creates a new recipient list builder.</summary>
    public static Builder Create() => new();

    private sealed class Recipient
    {
        internal Recipient(string name, RecipientPredicate predicate, RecipientHandler handler)
            => (Name, Predicate, Handler) = (name, predicate, handler);

        internal string Name { get; }

        internal RecipientPredicate Predicate { get; }

        internal RecipientHandler Handler { get; }
    }

    /// <summary>Fluent builder for <see cref="RecipientList{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private readonly List<Recipient> _recipients = new(8);

        /// <summary>Adds a recipient that receives every message.</summary>
        public Builder To(string name, RecipientHandler handler)
            => When(name, static (_, _) => true).Then(handler);

        /// <summary>Adds a conditional recipient.</summary>
        public WhenBuilder When(string name, RecipientPredicate predicate)
        {
            ValidateName(name);
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return new WhenBuilder(this, name, predicate);
        }

        /// <summary>Builds an immutable recipient list.</summary>
        public RecipientList<TPayload> Build() => new(_recipients.ToArray());

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Recipient name cannot be null, empty, or whitespace.", nameof(name));
        }

        /// <summary>Fluent recipient continuation.</summary>
        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly string _name;
            private readonly RecipientPredicate _predicate;

            internal WhenBuilder(Builder owner, string name, RecipientPredicate predicate)
                => (_owner, _name, _predicate) = (owner, name, predicate);

            /// <summary>Adds the recipient handler.</summary>
            public Builder Then(RecipientHandler handler)
            {
                if (handler is null)
                    throw new ArgumentNullException(nameof(handler));

                _owner._recipients.Add(new Recipient(_name, _predicate, handler));
                return _owner;
            }
        }
    }
}
