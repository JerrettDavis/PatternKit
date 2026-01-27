namespace PatternKit.Examples.ProxyGeneratorDemo.Interceptors;

/// <summary>
/// Interceptor that demonstrates exception handling and logging.
/// In a full implementation, this could implement retry logic with custom exception handling.
/// </summary>
public sealed class RetryInterceptor : IPaymentServiceInterceptor
{
    private readonly int _maxRetries;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryInterceptor"/> class.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (for demonstration).</param>
    public RetryInterceptor(int maxRetries = 3)
    {
        _maxRetries = maxRetries;
    }

    /// <inheritdoc />
    public void Before(MethodContext context)
    {
        // Could initialize retry state here if needed
    }

    /// <inheritdoc />
    public void After(MethodContext context)
    {
        // Success - no retry needed
    }

    /// <inheritdoc />
    public void OnException(MethodContext context, Exception exception)
    {
        if (IsRetriable(exception))
        {
            Console.WriteLine($"[Retry] ✗ Retriable exception in {context.MethodName}: {exception.Message}");
            Console.WriteLine($"[Retry] ℹ In a full implementation, would retry up to {_maxRetries} times");
        }
        else
        {
            Console.WriteLine($"[Retry] ✗ Non-retriable exception in {context.MethodName}: {exception.GetType().Name}");
        }
    }

    /// <inheritdoc />
    public async ValueTask BeforeAsync(MethodContext context)
    {
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask AfterAsync(MethodContext context)
    {
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask OnExceptionAsync(MethodContext context, Exception exception)
    {
        if (IsRetriable(exception))
        {
            Console.WriteLine($"[Retry] ✗ Retriable exception in {context.MethodName}: {exception.Message} (async)");
            Console.WriteLine($"[Retry] ℹ In a full implementation, would retry up to {_maxRetries} times");
        }
        else
        {
            Console.WriteLine($"[Retry] ✗ Non-retriable exception in {context.MethodName}: {exception.GetType().Name} (async)");
        }
        await ValueTask.CompletedTask;
    }

    private static bool IsRetriable(Exception exception)
    {
        // Retry transient failures, but not permanent ones like UnauthorizedAccessException
        return exception is not UnauthorizedAccessException and not ArgumentException;
    }
}
