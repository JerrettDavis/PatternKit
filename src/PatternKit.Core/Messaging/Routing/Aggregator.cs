using System.Collections.ObjectModel;

namespace PatternKit.Messaging.Routing;

/// <summary>
/// In-process aggregator that collects related messages until a completion policy is satisfied.
/// </summary>
public sealed class Aggregator<TKey, TItem, TResult>
    where TKey : notnull
{
    /// <summary>Selects the aggregation key for a message.</summary>
    public delegate TKey KeySelector(Message<TItem> message, MessageContext context);

    /// <summary>Determines whether a group is complete.</summary>
    public delegate bool CompletionPolicy(TKey key, IReadOnlyList<Message<TItem>> messages, MessageContext context);

    /// <summary>Creates the result for a completed group.</summary>
    public delegate TResult ResultFactory(TKey key, IReadOnlyList<Message<TItem>> messages, MessageContext context);

    private readonly object _gate = new();
    private readonly Dictionary<TKey, Group> _groups = new();
    private readonly KeySelector _keySelector;
    private readonly CompletionPolicy _completionPolicy;
    private readonly ResultFactory _resultFactory;
    private readonly DuplicateMessagePolicy _duplicatePolicy;

    private Aggregator(
        KeySelector keySelector,
        CompletionPolicy completionPolicy,
        ResultFactory resultFactory,
        DuplicateMessagePolicy duplicatePolicy)
        => (_keySelector, _completionPolicy, _resultFactory, _duplicatePolicy) =
            (keySelector, completionPolicy, resultFactory, duplicatePolicy);

    /// <summary>
    /// Adds a message to its group and returns a completed result when the completion policy is satisfied.
    /// </summary>
    public AggregationResult<TKey, TResult> Add(Message<TItem> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var key = _keySelector(message, effectiveContext);
        IReadOnlyList<Message<TItem>>? completedSnapshot = null;
        bool accepted;
        lock (_gate)
        {
            if (!_groups.TryGetValue(key, out var group))
            {
                group = new Group();
                _groups.Add(key, group);
            }

            accepted = group.Add(message, _duplicatePolicy);
            var snapshot = group.Snapshot();
            if (!_completionPolicy(key, snapshot, effectiveContext))
                return AggregationResult<TKey, TResult>.Pending(key, snapshot.Count, accepted);

            _groups.Remove(key);
            completedSnapshot = snapshot;
        }

        return AggregationResult<TKey, TResult>.Complete(
            key,
            completedSnapshot.Count,
            accepted,
            _resultFactory(key, completedSnapshot, effectiveContext));
    }

    /// <summary>Returns the number of currently open groups.</summary>
    public int OpenGroupCount
    {
        get
        {
            lock (_gate)
                return _groups.Count;
        }
    }

    /// <summary>Creates a new aggregator builder.</summary>
    public static Builder Create() => new();

    private sealed class Group
    {
        private readonly List<Message<TItem>> _messages = new();
        private readonly HashSet<string> _messageIds = new(StringComparer.Ordinal);

        internal bool Add(Message<TItem> message, DuplicateMessagePolicy duplicatePolicy)
        {
            var messageId = message.Headers.MessageId;
            if (messageId is null)
            {
                _messages.Add(message);
                return true;
            }

            if (_messageIds.Add(messageId))
            {
                _messages.Add(message);
                return true;
            }

            if (duplicatePolicy == DuplicateMessagePolicy.Include)
            {
                _messages.Add(message);
                return true;
            }

            if (duplicatePolicy == DuplicateMessagePolicy.Replace)
            {
                for (var i = 0; i < _messages.Count; i++)
                {
                    if (_messages[i].Headers.MessageId == messageId)
                    {
                        _messages[i] = message;
                        return true;
                    }
                }
            }

            return false;
        }

        internal IReadOnlyList<Message<TItem>> Snapshot()
            => new ReadOnlyCollection<Message<TItem>>(_messages.ToArray());
    }

    /// <summary>Fluent builder for <see cref="Aggregator{TKey,TItem,TResult}"/>.</summary>
    public sealed class Builder
    {
        private KeySelector? _keySelector;
        private CompletionPolicy? _completionPolicy;
        private ResultFactory? _resultFactory;
        private DuplicateMessagePolicy _duplicatePolicy = DuplicateMessagePolicy.Ignore;

        /// <summary>Sets the aggregation key selector.</summary>
        public Builder KeyBy(KeySelector keySelector)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            return this;
        }

        /// <summary>Sets the completion policy.</summary>
        public Builder CompleteWhen(CompletionPolicy completionPolicy)
        {
            _completionPolicy = completionPolicy ?? throw new ArgumentNullException(nameof(completionPolicy));
            return this;
        }

        /// <summary>Sets the result factory.</summary>
        public Builder Project(ResultFactory resultFactory)
        {
            _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
            return this;
        }

        /// <summary>Sets duplicate handling for messages with the same message id.</summary>
        public Builder Duplicates(DuplicateMessagePolicy duplicatePolicy)
        {
            _duplicatePolicy = duplicatePolicy;
            return this;
        }

        /// <summary>Builds an immutable aggregator.</summary>
        public Aggregator<TKey, TItem, TResult> Build()
            => new(
                _keySelector ?? throw new InvalidOperationException("An aggregation key selector is required."),
                _completionPolicy ?? throw new InvalidOperationException("An aggregation completion policy is required."),
                _resultFactory ?? throw new InvalidOperationException("An aggregation result factory is required."),
                _duplicatePolicy);
    }
}
