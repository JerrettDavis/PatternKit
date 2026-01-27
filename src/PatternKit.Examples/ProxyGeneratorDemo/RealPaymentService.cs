namespace PatternKit.Examples.ProxyGeneratorDemo;

/// <summary>
/// Concrete implementation of <see cref="IPaymentService"/> that simulates actual payment processing.
/// </summary>
public sealed class RealPaymentService : IPaymentService
{
    private readonly Dictionary<string, List<Transaction>> _transactionStore = new();
    private int _transactionCounter;

    /// <inheritdoc />
    public PaymentResult ProcessPayment(PaymentRequest request)
    {
        // Simulate payment processing logic
        var transactionId = $"TXN-{Interlocked.Increment(ref _transactionCounter):D6}";

        // Simulate processing delay
        Thread.Sleep(100);

        // Store transaction
        var transaction = new Transaction
        {
            TransactionId = transactionId,
            CustomerId = request.CustomerId,
            Amount = request.Amount,
            Currency = request.Currency,
            Status = "Completed",
            Timestamp = DateTime.UtcNow,
            Description = request.Description
        };

        lock (_transactionStore)
        {
            if (!_transactionStore.ContainsKey(request.CustomerId))
            {
                _transactionStore[request.CustomerId] = new List<Transaction>();
            }
            _transactionStore[request.CustomerId].Add(transaction);
        }

        return new PaymentResult
        {
            Success = true,
            TransactionId = transactionId,
            Message = $"Payment of {request.Amount} {request.Currency} processed successfully"
        };
    }

    /// <inheritdoc />
    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        // Simulate async payment processing
        var transactionId = $"TXN-{Interlocked.Increment(ref _transactionCounter):D6}";

        // Simulate async processing delay
        await Task.Delay(100, cancellationToken);

        // Store transaction
        var transaction = new Transaction
        {
            TransactionId = transactionId,
            CustomerId = request.CustomerId,
            Amount = request.Amount,
            Currency = request.Currency,
            Status = "Completed",
            Timestamp = DateTime.UtcNow,
            Description = request.Description
        };

        lock (_transactionStore)
        {
            if (!_transactionStore.ContainsKey(request.CustomerId))
            {
                _transactionStore[request.CustomerId] = new List<Transaction>();
            }
            _transactionStore[request.CustomerId].Add(transaction);
        }

        return new PaymentResult
        {
            Success = true,
            TransactionId = transactionId,
            Message = $"Payment of {request.Amount} {request.Currency} processed successfully (async)"
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<Transaction> GetTransactionHistory(string customerId)
    {
        // Simulate database lookup delay
        Thread.Sleep(50);

        lock (_transactionStore)
        {
            if (_transactionStore.TryGetValue(customerId, out var transactions))
            {
                return transactions.AsReadOnly();
            }
        }

        return Array.Empty<Transaction>();
    }
}
