using PatternKit.Generators.Proxy;

namespace PatternKit.Examples.ProxyGeneratorDemo;

/// <summary>
/// Payment service interface with support for synchronous and asynchronous operations.
/// Configured to generate a proxy with pipeline interceptor support.
/// </summary>
[GenerateProxy(InterceptorMode = ProxyInterceptorMode.Pipeline)]
public partial interface IPaymentService
{
    /// <summary>
    /// Processes a payment request synchronously.
    /// </summary>
    /// <param name="request">The payment request details.</param>
    /// <returns>The result of the payment processing operation.</returns>
    PaymentResult ProcessPayment(PaymentRequest request);

    /// <summary>
    /// Processes a payment request asynchronously.
    /// </summary>
    /// <param name="request">The payment request details.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the payment result.</returns>
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the transaction history for a specific customer.
    /// </summary>
    /// <param name="customerId">The unique customer identifier.</param>
    /// <returns>A collection of historical transactions for the customer.</returns>
    IReadOnlyList<Transaction> GetTransactionHistory(string customerId);
}
