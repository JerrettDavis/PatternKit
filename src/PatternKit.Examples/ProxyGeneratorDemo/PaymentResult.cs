namespace PatternKit.Examples.ProxyGeneratorDemo;

/// <summary>
/// Represents the result of a payment processing operation.
/// </summary>
public sealed class PaymentResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the payment was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets or sets the unique transaction identifier.
    /// </summary>
    public required string TransactionId { get; init; }

    /// <summary>
    /// Gets or sets the result message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets or sets the timestamp of the transaction.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
