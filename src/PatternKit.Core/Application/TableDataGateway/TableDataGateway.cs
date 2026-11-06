namespace PatternKit.Application.TableDataGateway;

/// <summary>Row-oriented gateway for one table or table-like persistence boundary.</summary>
public interface ITableDataGateway<TRow, TKey>
    where TKey : notnull
{
    string TableName { get; }

    ValueTask<TableGatewayResult<TRow>> InsertAsync(TRow row, CancellationToken cancellationToken = default);

    ValueTask<TRow?> GetAsync(TKey key, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<TRow>> ListAsync(CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<TRow>> QueryAsync(Func<TRow, bool> predicate, CancellationToken cancellationToken = default);

    ValueTask<TableGatewayResult<TRow>> UpdateAsync(TRow row, CancellationToken cancellationToken = default);

    ValueTask<TableGatewayResult<TRow>> DeleteAsync(TKey key, CancellationToken cancellationToken = default);
}

/// <summary>In-memory Table Data Gateway for samples, tests, and embedded applications.</summary>
public sealed class InMemoryTableDataGateway<TRow, TKey> : ITableDataGateway<TRow, TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TRow> _rows;
    private readonly Func<TRow, TKey> _keySelector;

    private InMemoryTableDataGateway(string tableName, Func<TRow, TKey> keySelector, IEqualityComparer<TKey>? comparer)
    {
        TableName = tableName;
        _keySelector = keySelector;
        _rows = new Dictionary<TKey, TRow>(comparer);
    }

    public string TableName { get; }

    public static Builder Create(string tableName, Func<TRow, TKey> keySelector)
        => new(tableName, keySelector);

    public ValueTask<TableGatewayResult<TRow>> InsertAsync(TRow row, CancellationToken cancellationToken = default)
    {
        if (row is null)
            throw new ArgumentNullException(nameof(row));

        cancellationToken.ThrowIfCancellationRequested();
        var key = _keySelector(row);
        if (_rows.ContainsKey(key))
            return new(TableGatewayResult<TRow>.Conflict(row, $"Row with key '{key}' already exists in '{TableName}'."));

        _rows[key] = row;
        return new(TableGatewayResult<TRow>.Inserted(row));
    }

    public ValueTask<TRow?> GetAsync(TKey key, CancellationToken cancellationToken = default)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        cancellationToken.ThrowIfCancellationRequested();
        _rows.TryGetValue(key, out var row);
        return new(row);
    }

    public ValueTask<IReadOnlyList<TRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IReadOnlyList<TRow>)_rows.Values.ToArray());
    }

    public ValueTask<IReadOnlyList<TRow>> QueryAsync(Func<TRow, bool> predicate, CancellationToken cancellationToken = default)
    {
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        cancellationToken.ThrowIfCancellationRequested();
        return new((IReadOnlyList<TRow>)_rows.Values.Where(predicate).ToArray());
    }

    public ValueTask<TableGatewayResult<TRow>> UpdateAsync(TRow row, CancellationToken cancellationToken = default)
    {
        if (row is null)
            throw new ArgumentNullException(nameof(row));

        cancellationToken.ThrowIfCancellationRequested();
        var key = _keySelector(row);
        if (!_rows.ContainsKey(key))
            return new(TableGatewayResult<TRow>.Missing(row, $"Row with key '{key}' was not found in '{TableName}'."));

        _rows[key] = row;
        return new(TableGatewayResult<TRow>.Updated(row));
    }

    public ValueTask<TableGatewayResult<TRow>> DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        cancellationToken.ThrowIfCancellationRequested();
        if (!_rows.TryGetValue(key, out var row))
            return new(TableGatewayResult<TRow>.Missing(default, $"Row with key '{key}' was not found in '{TableName}'."));

        _rows.Remove(key);
        return new(TableGatewayResult<TRow>.Deleted(row));
    }

    public sealed class Builder
    {
        private readonly string _tableName;
        private readonly Func<TRow, TKey> _keySelector;
        private IEqualityComparer<TKey>? _comparer;

        internal Builder(string tableName, Func<TRow, TKey> keySelector)
        {
            _tableName = string.IsNullOrWhiteSpace(tableName)
                ? throw new ArgumentException("Table Data Gateway table name is required.", nameof(tableName))
                : tableName;
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        }

        public Builder UseComparer(IEqualityComparer<TKey> comparer)
        {
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            return this;
        }

        public InMemoryTableDataGateway<TRow, TKey> Build()
            => new(_tableName, _keySelector, _comparer);
    }
}

/// <summary>Result returned by Table Data Gateway mutation operations.</summary>
public sealed class TableGatewayResult<TRow>
{
    private TableGatewayResult(TRow? row, TableGatewayStatus status, string? reason)
    {
        Row = row;
        Status = status;
        Reason = reason;
    }

    public TRow? Row { get; }

    public TableGatewayStatus Status { get; }

    public string? Reason { get; }

    public bool Succeeded => Status is TableGatewayStatus.Inserted or TableGatewayStatus.Updated or TableGatewayStatus.Deleted;

    public static TableGatewayResult<TRow> Inserted(TRow row) => new(row, TableGatewayStatus.Inserted, null);

    public static TableGatewayResult<TRow> Updated(TRow row) => new(row, TableGatewayStatus.Updated, null);

    public static TableGatewayResult<TRow> Deleted(TRow row) => new(row, TableGatewayStatus.Deleted, null);

    public static TableGatewayResult<TRow> Conflict(TRow row, string reason) => new(row, TableGatewayStatus.Conflict, Validate(reason));

    public static TableGatewayResult<TRow> Missing(TRow? row, string reason) => new(row, TableGatewayStatus.Missing, Validate(reason));

    private static string Validate(string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("Table Data Gateway result reason is required.", nameof(reason))
            : reason;
}

/// <summary>Mutation status for Table Data Gateway operations.</summary>
public enum TableGatewayStatus
{
    Inserted,
    Updated,
    Deleted,
    Conflict,
    Missing
}
