using System.Diagnostics;
using System.Collections.Concurrent;

namespace PatternKit.Examples.ProxyGeneratorDemo.Interceptors;

/// <summary>
/// Interceptor that measures and records method execution time.
/// Demonstrates performance monitoring as a cross-cutting concern.
/// </summary>
public sealed class TimingInterceptor : IPaymentServiceInterceptor
{
    private readonly Dictionary<string, long> _timings = new();
    private readonly ConcurrentDictionary<MethodContext, Stopwatch> _activeTimers = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public void Before(MethodContext context)
    {
        var sw = Stopwatch.StartNew();
        _activeTimers[context] = sw;
    }

    /// <inheritdoc />
    public void After(MethodContext context)
    {
        if (_activeTimers.TryRemove(context, out var sw))
        {
            sw.Stop();
            RecordTiming(context.MethodName, sw.ElapsedMilliseconds);
            Console.WriteLine($"[Timing] {context.MethodName} took {sw.ElapsedMilliseconds}ms");
        }
    }

    /// <inheritdoc />
    public void OnException(MethodContext context, Exception exception)
    {
        if (_activeTimers.TryRemove(context, out var sw))
        {
            sw.Stop();
            Console.WriteLine($"[Timing] {context.MethodName} failed after {sw.ElapsedMilliseconds}ms");
        }
    }

    /// <inheritdoc />
    public async ValueTask BeforeAsync(MethodContext context)
    {
        var sw = Stopwatch.StartNew();
        _activeTimers[context] = sw;
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask AfterAsync(MethodContext context)
    {
        if (_activeTimers.TryRemove(context, out var sw))
        {
            sw.Stop();
            RecordTiming(context.MethodName, sw.ElapsedMilliseconds);
            Console.WriteLine($"[Timing] {context.MethodName} took {sw.ElapsedMilliseconds}ms (async)");
        }
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask OnExceptionAsync(MethodContext context, Exception exception)
    {
        if (_activeTimers.TryRemove(context, out var sw))
        {
            sw.Stop();
            Console.WriteLine($"[Timing] {context.MethodName} failed after {sw.ElapsedMilliseconds}ms (async)");
        }
        await ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the recorded timings for all methods.
    /// </summary>
    public IReadOnlyDictionary<string, long> GetTimings()
    {
        lock (_lock)
        {
            return new Dictionary<string, long>(_timings);
        }
    }

    /// <summary>
    /// Prints a summary of all recorded timings.
    /// </summary>
    public void PrintSummary()
    {
        Console.WriteLine("\n[Timing] Performance Summary:");
        lock (_lock)
        {
            foreach (var (method, time) in _timings.OrderByDescending(kvp => kvp.Value))
            {
                Console.WriteLine($"  {method}: {time}ms");
            }
        }
    }

    private void RecordTiming(string methodName, long milliseconds)
    {
        lock (_lock)
        {
            if (_timings.ContainsKey(methodName))
            {
                _timings[methodName] += milliseconds;
            }
            else
            {
                _timings[methodName] = milliseconds;
            }
        }
    }
}
