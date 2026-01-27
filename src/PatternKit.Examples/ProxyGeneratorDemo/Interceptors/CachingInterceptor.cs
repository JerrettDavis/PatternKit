using System.Collections.Concurrent;

namespace PatternKit.Examples.ProxyGeneratorDemo.Interceptors;

/// <summary>
/// Interceptor that caches method results to improve performance.
/// Demonstrates conditional caching based on method type.
/// </summary>
public sealed class CachingInterceptor : IPaymentServiceInterceptor
{
    private readonly ConcurrentDictionary<string, (object Result, DateTime Expiry)> _cache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public void Before(MethodContext context)
    {
        // Check cache only for GetTransactionHistory
        if (context is GetTransactionHistoryMethodContext historyContext)
        {
            var cacheKey = $"history:{historyContext.CustomerId}";
            
            if (_cache.TryGetValue(cacheKey, out var cached) && cached.Expiry > DateTime.UtcNow)
            {
                Console.WriteLine($"[Cache] ✓ Cache hit for customer {historyContext.CustomerId}");
                // Note: Cannot set result from Before hook - caching would need generator support
            }
            else
            {
                Console.WriteLine($"[Cache] ✗ Cache miss for customer {historyContext.CustomerId}");
            }
        }
    }

    /// <inheritdoc />
    public void After(MethodContext context)
    {
        // Cache result for GetTransactionHistory
        if (context is GetTransactionHistoryMethodContext historyContext)
        {
            var cacheKey = $"history:{historyContext.CustomerId}";
            var result = historyContext.Result;
            
            if (result != null)
            {
                _cache[cacheKey] = (result, DateTime.UtcNow.Add(_cacheDuration));
                Console.WriteLine($"[Cache] → Cached result for customer {historyContext.CustomerId}");
            }
        }
    }

    /// <inheritdoc />
    public void OnException(MethodContext context, Exception exception)
    {
        // No caching on exception
    }

    /// <inheritdoc />
    public async ValueTask BeforeAsync(MethodContext context)
    {
        // Async methods don't use caching in this demo
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

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        Console.WriteLine("[Cache] Cache cleared");
    }

    /// <summary>
    /// Gets the current cache statistics.
    /// </summary>
    public (int Count, int Expired) GetStats()
    {
        var now = DateTime.UtcNow;
        var expired = _cache.Count(kvp => kvp.Value.Expiry <= now);
        return (_cache.Count, expired);
    }
}
