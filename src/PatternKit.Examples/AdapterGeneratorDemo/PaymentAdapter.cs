using PatternKit.Generators.Adapter;

namespace PatternKit.Examples.AdapterGeneratorDemo;

// =============================================================================
// Scenario: Adapting multiple payment gateways to a unified interface
// =============================================================================

/// <summary>
/// Unified payment gateway interface for the application.
/// Allows easy swapping of payment providers.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Charges a credit card.</summary>
    Task<PaymentResult> ChargeAsync(string cardToken, decimal amount, string currency, CancellationToken ct = default);

    /// <summary>Refunds a previous charge.</summary>
    Task<RefundResult> RefundAsync(string transactionId, decimal amount, CancellationToken ct = default);

    /// <summary>Gets the gateway name for logging.</summary>
    string GatewayName { get; }
}

/// <summary>Result of a payment charge operation.</summary>
public record PaymentResult(bool Success, string TransactionId, string? ErrorMessage = null);

/// <summary>Result of a refund operation.</summary>
public record RefundResult(bool Success, string RefundId, string? ErrorMessage = null);

// -----------------------------------------------------------------------------
// Legacy Stripe-like SDK with different API shape
// -----------------------------------------------------------------------------

/// <summary>
/// Simulates a third-party Stripe-like payment SDK.
/// </summary>
public sealed class StripePaymentClient
{
    public string ProviderName => "Stripe";

    public async Task<StripeChargeResponse> CreateChargeAsync(
        StripeChargeRequest request,
        CancellationToken cancellation = default)
    {
        await Task.Delay(10, cancellation); // Simulate network call
        return new StripeChargeResponse
        {
            Succeeded = true,
            ChargeId = $"ch_{Guid.NewGuid():N}",
            Error = null
        };
    }

    public async Task<StripeRefundResponse> CreateRefundAsync(
        string chargeId,
        long amountInCents,
        CancellationToken cancellation = default)
    {
        await Task.Delay(10, cancellation);
        return new StripeRefundResponse
        {
            Succeeded = true,
            RefundId = $"re_{Guid.NewGuid():N}",
            Error = null
        };
    }
}

public class StripeChargeRequest
{
    public string Source { get; set; } = "";
    public long AmountInCents { get; set; }
    public string Currency { get; set; } = "usd";
}

public class StripeChargeResponse
{
    public bool Succeeded { get; set; }
    public string ChargeId { get; set; } = "";
    public string? Error { get; set; }
}

public class StripeRefundResponse
{
    public bool Succeeded { get; set; }
    public string RefundId { get; set; } = "";
    public string? Error { get; set; }
}

/// <summary>
/// Adapter mappings that bridge StripePaymentClient to IPaymentGateway.
/// </summary>
[GenerateAdapter(
    Target = typeof(IPaymentGateway),
    Adaptee = typeof(StripePaymentClient),
    AdapterTypeName = "StripePaymentAdapter")]
public static partial class StripeAdapterMappings
{
    [AdapterMap(TargetMember = nameof(IPaymentGateway.GatewayName))]
    public static string MapGatewayName(StripePaymentClient adaptee)
        => adaptee.ProviderName;

    [AdapterMap(TargetMember = nameof(IPaymentGateway.ChargeAsync))]
    public static async Task<PaymentResult> MapChargeAsync(
        StripePaymentClient adaptee,
        string cardToken,
        decimal amount,
        string currency,
        CancellationToken ct)
    {
        var request = new StripeChargeRequest
        {
            Source = cardToken,
            AmountInCents = (long)(amount * 100),
            Currency = currency.ToLowerInvariant()
        };

        var response = await adaptee.CreateChargeAsync(request, ct);
        return new PaymentResult(response.Succeeded, response.ChargeId, response.Error);
    }

    [AdapterMap(TargetMember = nameof(IPaymentGateway.RefundAsync))]
    public static async Task<RefundResult> MapRefundAsync(
        StripePaymentClient adaptee,
        string transactionId,
        decimal amount,
        CancellationToken ct)
    {
        var response = await adaptee.CreateRefundAsync(transactionId, (long)(amount * 100), ct);
        return new RefundResult(response.Succeeded, response.RefundId, response.Error);
    }
}

// -----------------------------------------------------------------------------
// Legacy PayPal-like SDK with yet another API shape
// -----------------------------------------------------------------------------

/// <summary>
/// Simulates a third-party PayPal-like payment SDK.
/// </summary>
public sealed class PayPalPaymentService
{
    public string ServiceIdentifier => "PayPal";

    public async Task<PayPalTransaction> ExecutePaymentAsync(
        string tokenId,
        PayPalAmount paymentAmount,
        CancellationToken token = default)
    {
        await Task.Delay(15, token);
        return new PayPalTransaction
        {
            State = "approved",
            Id = $"PAY-{Guid.NewGuid():N}"
        };
    }

    public async Task<PayPalRefund> ProcessRefundAsync(
        string paymentId,
        PayPalAmount refundAmount,
        CancellationToken token = default)
    {
        await Task.Delay(15, token);
        return new PayPalRefund
        {
            State = "completed",
            Id = $"REF-{Guid.NewGuid():N}"
        };
    }
}

public class PayPalAmount
{
    public string Total { get; set; } = "0.00";
    public string Currency { get; set; } = "USD";
}

public class PayPalTransaction
{
    public string State { get; set; } = "";
    public string Id { get; set; } = "";
}

public class PayPalRefund
{
    public string State { get; set; } = "";
    public string Id { get; set; } = "";
}

/// <summary>
/// Adapter mappings that bridge PayPalPaymentService to IPaymentGateway.
/// </summary>
[GenerateAdapter(
    Target = typeof(IPaymentGateway),
    Adaptee = typeof(PayPalPaymentService),
    AdapterTypeName = "PayPalPaymentAdapter")]
public static partial class PayPalAdapterMappings
{
    [AdapterMap(TargetMember = nameof(IPaymentGateway.GatewayName))]
    public static string MapGatewayName(PayPalPaymentService adaptee)
        => adaptee.ServiceIdentifier;

    [AdapterMap(TargetMember = nameof(IPaymentGateway.ChargeAsync))]
    public static async Task<PaymentResult> MapChargeAsync(
        PayPalPaymentService adaptee,
        string cardToken,
        decimal amount,
        string currency,
        CancellationToken ct)
    {
        var paypalAmount = new PayPalAmount
        {
            Total = amount.ToString("F2"),
            Currency = currency.ToUpperInvariant()
        };

        var transaction = await adaptee.ExecutePaymentAsync(cardToken, paypalAmount, ct);
        var success = transaction.State == "approved";
        return new PaymentResult(success, transaction.Id, success ? null : transaction.State);
    }

    [AdapterMap(TargetMember = nameof(IPaymentGateway.RefundAsync))]
    public static async Task<RefundResult> MapRefundAsync(
        PayPalPaymentService adaptee,
        string transactionId,
        decimal amount,
        CancellationToken ct)
    {
        var refundAmount = new PayPalAmount
        {
            Total = amount.ToString("F2"),
            Currency = "USD"
        };

        var refund = await adaptee.ProcessRefundAsync(transactionId, refundAmount, ct);
        var success = refund.State == "completed";
        return new RefundResult(success, refund.Id, success ? null : refund.State);
    }
}
