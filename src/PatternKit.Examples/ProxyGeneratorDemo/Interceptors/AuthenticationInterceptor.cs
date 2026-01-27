namespace PatternKit.Examples.ProxyGeneratorDemo.Interceptors;

/// <summary>
/// Interceptor that validates user authentication before method execution.
/// Demonstrates security as a cross-cutting concern.
/// </summary>
public sealed class AuthenticationInterceptor : IPaymentServiceInterceptor
{
    private readonly HashSet<string> _validTokens = new()
    {
        "valid-token-123",
        "admin-token-456"
    };

    /// <inheritdoc />
    public void Before(MethodContext context)
    {
        ValidateAuthentication(context);
    }

    /// <inheritdoc />
    public void After(MethodContext context)
    {
        // No action needed after method execution
    }

    /// <inheritdoc />
    public void OnException(MethodContext context, Exception exception)
    {
        // No action needed on exception
    }

    /// <inheritdoc />
    public async ValueTask BeforeAsync(MethodContext context)
    {
        ValidateAuthentication(context);
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
        await ValueTask.CompletedTask;
    }

    private void ValidateAuthentication(MethodContext context)
    {
        // Extract auth token from context based on method
        string? authToken = context switch
        {
            ProcessPaymentMethodContext pc => pc.Request.AuthToken,
            ProcessPaymentAsyncMethodContext pac => pac.Request.AuthToken,
            _ => null
        };

        if (authToken == null)
        {
            Console.WriteLine("[Authentication] ⚠ No auth token provided");
            throw new UnauthorizedAccessException("Authentication token is required");
        }

        if (!_validTokens.Contains(authToken))
        {
            Console.WriteLine($"[Authentication] ✗ Invalid token: {authToken}");
            throw new UnauthorizedAccessException("Invalid authentication token");
        }

        Console.WriteLine($"[Authentication] ✓ Token validated: {authToken}");
    }
}
