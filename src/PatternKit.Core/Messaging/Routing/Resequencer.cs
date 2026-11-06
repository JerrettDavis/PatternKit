namespace PatternKit.Messaging.Routing;

/// <summary>Buffers out-of-order messages and releases contiguous messages in sequence order.</summary>
public sealed class Resequencer<TPayload>
{
    public delegate long SequenceSelector(Message<TPayload> message, MessageContext context);

    private readonly object _gate = new();
    private readonly string _name;
    private readonly SequenceSelector _selector;
    private readonly SortedDictionary<long, Message<TPayload>> _buffer = new();
    private long _nextSequence;

    private Resequencer(string name, SequenceSelector selector, long startsAt)
        => (_name, _selector, _nextSequence) = (name, selector, startsAt);

    public ResequencerResult<TPayload> Accept(Message<TPayload> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var sequence = _selector(message, effectiveContext);
        lock (_gate)
        {
            if (sequence < _nextSequence)
                return ResequencerResult<TPayload>.Rejected(_name, sequence, "Message sequence has already been released.");
            if (_buffer.ContainsKey(sequence))
                return ResequencerResult<TPayload>.Rejected(_name, sequence, "Message sequence is already buffered.");

            _buffer.Add(sequence, message);
            var released = new List<SequencedMessage<TPayload>>();
            while (_buffer.TryGetValue(_nextSequence, out var next))
            {
                _buffer.Remove(_nextSequence);
                released.Add(new(_nextSequence, next));
                _nextSequence++;
            }

            return ResequencerResult<TPayload>.Success(_name, sequence, _nextSequence, released);
        }
    }

    public static Builder Create(string name = "resequencer") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private SequenceSelector? _selector;
        private long _startsAt = 1;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Resequencer name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder SelectSequence(SequenceSelector selector)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            return this;
        }

        public Builder StartsAt(long startsAt)
        {
            if (startsAt < 0)
                throw new ArgumentOutOfRangeException(nameof(startsAt), "Starting sequence cannot be negative.");

            _startsAt = startsAt;
            return this;
        }

        public Resequencer<TPayload> Build()
        {
            if (_selector is null)
                throw new InvalidOperationException("Resequencer requires a sequence selector.");

            return new(_name, _selector, _startsAt);
        }
    }
}

public sealed class ResequencerResult<TPayload>
{
    private ResequencerResult(string name, long acceptedSequence, long nextExpectedSequence, IReadOnlyList<SequencedMessage<TPayload>> released, bool accepted, string? rejectionReason)
        => (Name, AcceptedSequence, NextExpectedSequence, Released, Accepted, RejectionReason) = (name, acceptedSequence, nextExpectedSequence, released, accepted, rejectionReason);

    public string Name { get; }

    public long AcceptedSequence { get; }

    public long NextExpectedSequence { get; }

    public IReadOnlyList<SequencedMessage<TPayload>> Released { get; }

    public bool Accepted { get; }

    public string? RejectionReason { get; }

    internal static ResequencerResult<TPayload> Success(string name, long acceptedSequence, long nextExpectedSequence, IReadOnlyList<SequencedMessage<TPayload>> released)
        => new(name, acceptedSequence, nextExpectedSequence, released, true, null);

    internal static ResequencerResult<TPayload> Rejected(string name, long acceptedSequence, string rejectionReason)
        => new(name, acceptedSequence, acceptedSequence, Array.Empty<SequencedMessage<TPayload>>(), false, rejectionReason);
}

public sealed class SequencedMessage<TPayload>
{
    public SequencedMessage(long sequence, Message<TPayload> message)
        => (Sequence, Message) = (sequence, message ?? throw new ArgumentNullException(nameof(message)));

    public long Sequence { get; }

    public Message<TPayload> Message { get; }
}
