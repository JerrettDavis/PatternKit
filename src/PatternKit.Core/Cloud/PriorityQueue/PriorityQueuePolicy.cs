namespace PatternKit.Cloud.PriorityQueue;

public sealed class PriorityQueueEnqueueResult<TItem, TPriority>
{
    public PriorityQueueEnqueueResult(string queueName, TItem item, TPriority priority, int count)
        => (QueueName, Item, Priority, Count) = (queueName, item, priority, count);

    public string QueueName { get; }

    public TItem Item { get; }

    public TPriority Priority { get; }

    public int Count { get; }
}

public sealed class PriorityQueueDequeueResult<TItem, TPriority>
{
    private PriorityQueueDequeueResult(string queueName, TItem? item, TPriority? priority, bool hasItem, int remainingCount)
        => (QueueName, Item, Priority, HasItem, RemainingCount) = (queueName, item, priority, hasItem, remainingCount);

    public string QueueName { get; }

    public TItem? Item { get; }

    public TPriority? Priority { get; }

    public bool HasItem { get; }

    public int RemainingCount { get; }

    public static PriorityQueueDequeueResult<TItem, TPriority> Empty(string queueName)
        => new(queueName, default, default, false, 0);

    public static PriorityQueueDequeueResult<TItem, TPriority> ItemDequeued(string queueName, TItem item, TPriority priority, int remainingCount)
        => new(queueName, item, priority, true, remainingCount);
}

public sealed class PriorityQueuePolicy<TItem, TPriority>
{
    private readonly object _gate = new();
    private readonly List<Entry> _items = [];
    private readonly Func<TItem, TPriority> _prioritySelector;
    private readonly IComparer<TPriority> _comparer;
    private readonly bool _dequeueHighestPriorityFirst;
    private long _nextSequence;

    private PriorityQueuePolicy(
        string name,
        Func<TItem, TPriority> prioritySelector,
        IComparer<TPriority> comparer,
        bool dequeueHighestPriorityFirst)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Priority queue name is required.", nameof(name));

        Name = name;
        _prioritySelector = prioritySelector ?? throw new ArgumentNullException(nameof(prioritySelector));
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        _dequeueHighestPriorityFirst = dequeueHighestPriorityFirst;
    }

    public string Name { get; }

    public int Count
    {
        get
        {
            lock (_gate)
                return _items.Count;
        }
    }

    public PriorityQueueEnqueueResult<TItem, TPriority> Enqueue(TItem item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        var priority = _prioritySelector(item);
        lock (_gate)
        {
            _items.Add(new(item, priority, _nextSequence++));
            return new(Name, item, priority, _items.Count);
        }
    }

    public PriorityQueueDequeueResult<TItem, TPriority> Dequeue()
    {
        lock (_gate)
        {
            if (_items.Count == 0)
                return PriorityQueueDequeueResult<TItem, TPriority>.Empty(Name);

            var index = FindBestIndex();
            var entry = _items[index];
            _items.RemoveAt(index);
            return PriorityQueueDequeueResult<TItem, TPriority>.ItemDequeued(Name, entry.Item, entry.Priority, _items.Count);
        }
    }

    public PriorityQueueDequeueResult<TItem, TPriority> Peek()
    {
        lock (_gate)
        {
            if (_items.Count == 0)
                return PriorityQueueDequeueResult<TItem, TPriority>.Empty(Name);

            var entry = _items[FindBestIndex()];
            return PriorityQueueDequeueResult<TItem, TPriority>.ItemDequeued(Name, entry.Item, entry.Priority, _items.Count);
        }
    }

    public static Builder Create(string name = "priority-queue") => new(name);

    private int FindBestIndex()
    {
        var best = 0;
        for (var i = 1; i < _items.Count; i++)
        {
            var comparison = _comparer.Compare(_items[i].Priority, _items[best].Priority);
            if (_dequeueHighestPriorityFirst ? comparison > 0 : comparison < 0)
            {
                best = i;
                continue;
            }

            if (comparison == 0 && _items[i].Sequence < _items[best].Sequence)
                best = i;
        }

        return best;
    }

    public sealed class Builder
    {
        private readonly string _name;
        private Func<TItem, TPriority>? _prioritySelector;
        private IComparer<TPriority> _comparer = Comparer<TPriority>.Default;
        private bool _dequeueHighestPriorityFirst = true;

        internal Builder(string name) => _name = name;

        public Builder WithPrioritySelector(Func<TItem, TPriority> prioritySelector)
        {
            _prioritySelector = prioritySelector ?? throw new ArgumentNullException(nameof(prioritySelector));
            return this;
        }

        public Builder WithComparer(IComparer<TPriority> comparer)
        {
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            return this;
        }

        public Builder DequeueHighestPriorityFirst()
        {
            _dequeueHighestPriorityFirst = true;
            return this;
        }

        public Builder DequeueLowestPriorityFirst()
        {
            _dequeueHighestPriorityFirst = false;
            return this;
        }

        public PriorityQueuePolicy<TItem, TPriority> Build()
        {
            if (_prioritySelector is null)
                throw new InvalidOperationException("Priority queue requires a priority selector.");

            return new(_name, _prioritySelector, _comparer, _dequeueHighestPriorityFirst);
        }
    }

    private sealed class Entry
    {
        public Entry(TItem item, TPriority priority, long sequence)
            => (Item, Priority, Sequence) = (item, priority, sequence);

        public TItem Item { get; }

        public TPriority Priority { get; }

        public long Sequence { get; }
    }
}
