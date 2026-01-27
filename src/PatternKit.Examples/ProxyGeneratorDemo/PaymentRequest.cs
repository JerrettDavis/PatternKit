namespace PatternKit.Examples.ProxyGeneratorDemo;

/// <summary>
/// Represents a payment request with customer and transaction details.
/// </summary>
public sealed class PaymentRequest
{
    /// <summary>
    /// Gets or sets the unique customer identifier.
    /// </summary>
    public required string CustomerId { get; init; }

    /// <summary>
    /// Gets or sets the payment amount.
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Gets or sets the currency code (e.g., "USD", "EUR").
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Gets or sets the payment description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the authentication token for the requesting user.
    /// </summary>
    public string? AuthToken { get; init; }
}
