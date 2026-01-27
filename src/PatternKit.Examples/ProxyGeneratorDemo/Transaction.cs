namespace PatternKit.Examples.ProxyGeneratorDemo;

/// <summary>
/// Represents a historical transaction record.
/// </summary>
public sealed class Transaction
{
    /// <summary>
    /// Gets or sets the transaction identifier.
    /// </summary>
    public required string TransactionId { get; init; }

    /// <summary>
    /// Gets or sets the customer identifier.
    /// </summary>
    public required string CustomerId { get; init; }

    /// <summary>
    /// Gets or sets the transaction amount.
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Gets or sets the currency code.
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Gets or sets the transaction status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the transaction timestamp.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the transaction description.
    /// </summary>
    public string? Description { get; init; }
}
