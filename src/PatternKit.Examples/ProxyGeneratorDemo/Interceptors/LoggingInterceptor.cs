namespace PatternKit.Examples.ProxyGeneratorDemo.Interceptors;

/// <summary>
/// Interceptor that logs method calls, results, and exceptions.
/// Demonstrates basic cross-cutting concern implementation.
/// </summary>
public sealed class LoggingInterceptor : IPaymentServiceInterceptor
{
    private readonly string _name;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingInterceptor"/> class.
    /// </summary>
    /// <param name="name">Optional name to identify this interceptor instance in logs.</param>
    public LoggingInterceptor(string? name = null)
    {
        _name = name ?? "Logging";
    }

    /// <inheritdoc />
    public void Before(MethodContext context)
    {
        Console.WriteLine($"[{_name}] → {context.MethodName}");
    }

    /// <inheritdoc />
    public void After(MethodContext context)
    {
        var resultInfo = FormatResult(context);
        Console.WriteLine($"[{_name}] ← {context.MethodName}{resultInfo}");
    }

    /// <inheritdoc />
    public void OnException(MethodContext context, Exception exception)
    {
        Console.WriteLine($"[{_name}] ✗ {context.MethodName} threw {exception.GetType().Name}: {exception.Message}");
    }

    /// <inheritdoc />
    public async ValueTask BeforeAsync(MethodContext context)
    {
        Console.WriteLine($"[{_name}] → {context.MethodName} (async)");
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask AfterAsync(MethodContext context)
    {
        var resultInfo = FormatResult(context);
        Console.WriteLine($"[{_name}] ← {context.MethodName}{resultInfo} (async)");
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask OnExceptionAsync(MethodContext context, Exception exception)
    {
        Console.WriteLine($"[{_name}] ✗ {context.MethodName} threw {exception.GetType().Name}: {exception.Message} (async)");
        await ValueTask.CompletedTask;
    }

    private static string FormatResult(MethodContext context)
    {
        return context switch
        {
            ProcessPaymentMethodContext pc => $" => PaymentResult(Success={pc.Result.Success}, TxnId={pc.Result.TransactionId})",
            ProcessPaymentAsyncMethodContext pac => $" => PaymentResult(Success={pac.Result.Result.Success}, TxnId={pac.Result.Result.TransactionId})",
            GetTransactionHistoryMethodContext gh => $" => {gh.Result.Count} transaction(s)",
            _ => ""
        };
    }
}
