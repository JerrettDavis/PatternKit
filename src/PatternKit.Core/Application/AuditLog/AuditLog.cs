namespace PatternKit.Application.AuditLog;

public interface IAuditLog<TEntry, TKey>
    where TKey : notnull
{
    string Name { get; }

    ValueTask<AuditLogAppendResult<TEntry>> AppendAsync(TEntry entry, CancellationToken cancellationToken = default);

    ValueTask<TEntry?> GetAsync(TKey key, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<TEntry>> QueryAsync(Func<TEntry, bool> predicate, CancellationToken cancellationToken = default);
}

public sealed class InMemoryAuditLog<TEntry, TKey> : IAuditLog<TEntry, TKey>
    where TKey : notnull
{
    private readonly object _gate = new();
    private readonly Func<TEntry, TKey> _keySelector;
    private readonly Dictionary<TKey, TEntry> _entries;
    private readonly List<TEntry> _orderedEntries = [];

    private InMemoryAuditLog(string name, Func<TEntry, TKey> keySelector, IEqualityComparer<TKey>? comparer)
    {
        Name = name;
        _keySelector = keySelector;
        _entries = new Dictionary<TKey, TEntry>(comparer);
    }

    public string Name { get; }

    public static Builder Create(string name, Func<TEntry, TKey> keySelector) => new(name, keySelector);

    public ValueTask<AuditLogAppendResult<TEntry>> AppendAsync(TEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry is null)
            throw new ArgumentNullException(nameof(entry));

        cancellationToken.ThrowIfCancellationRequested();
        var key = _keySelector(entry);
        if (key is null)
            throw new InvalidOperationException("Audit log key selector returned null.");

        lock (_gate)
        {
            if (_entries.ContainsKey(key))
                return new(AuditLogAppendResult<TEntry>.Duplicate(entry, $"Audit entry with key '{key}' already exists in '{Name}'."));

            _entries.Add(key, entry);
            _orderedEntries.Add(entry);
            return new(AuditLogAppendResult<TEntry>.AppendedEntry(entry));
        }
    }

    public ValueTask<TEntry?> GetAsync(TKey key, CancellationToken cancellationToken = default)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var result = _entries.TryGetValue(key, out var entry) ? entry : default;
            return new(result);
        }
    }

    public ValueTask<IReadOnlyList<TEntry>> QueryAsync(Func<TEntry, bool> predicate, CancellationToken cancellationToken = default)
    {
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
            return new(_orderedEntries.Where(predicate).ToArray());
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly Func<TEntry, TKey> _keySelector;
        private IEqualityComparer<TKey>? _comparer;

        internal Builder(string name, Func<TEntry, TKey> keySelector)
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Audit log name is required.", nameof(name))
                : name;
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        }

        public Builder UseComparer(IEqualityComparer<TKey> comparer)
        {
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            return this;
        }

        public InMemoryAuditLog<TEntry, TKey> Build() => new(_name, _keySelector, _comparer);
    }
}

public sealed class AuditLogAppendResult<TEntry>
{
    private AuditLogAppendResult(TEntry entry, AuditLogAppendStatus status, string? reason)
    {
        Entry = entry;
        Status = status;
        Reason = reason;
    }

    public TEntry Entry { get; }

    public AuditLogAppendStatus Status { get; }

    public string? Reason { get; }

    public bool Appended => Status == AuditLogAppendStatus.Appended;

    public static AuditLogAppendResult<TEntry> AppendedEntry(TEntry entry)
        => new(entry, AuditLogAppendStatus.Appended, null);

    public static AuditLogAppendResult<TEntry> Duplicate(TEntry entry, string reason)
        => new(entry, AuditLogAppendStatus.Duplicate, Validate(reason));

    private static string Validate(string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("Audit log append reason is required.", nameof(reason))
            : reason;
}

public enum AuditLogAppendStatus
{
    Appended,
    Duplicate
}
