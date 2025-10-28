using PatternKit.Behavioral.Template;

namespace PatternKit.Examples.TemplateDemo;

// Async subclassing demo: fetch → transform → store with cancellation, logging, and synchronization
public sealed class AsyncDataPipeline : AsyncTemplateMethod<int, string>
{
    protected override bool Synchronized => false; // allow concurrency for independent requests

    protected override async ValueTask OnBeforeAsync(int requestId, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[BeforeAsync] Request {requestId}: starting...");
        await Task.Yield();
    }

    protected override async ValueTask<string> StepAsync(int requestId, CancellationToken cancellationToken)
    {
        // Simulate async fetch
        await Task.Delay(25, cancellationToken);
        var payload = $"payload:{requestId}";

        // Simulate async transform
        await Task.Delay(10, cancellationToken);
        var transformed = payload.ToUpperInvariant();

        // Simulate async store
        await Task.Delay(5, cancellationToken);
        return transformed;
    }

    protected override async ValueTask OnAfterAsync(int requestId, string result, CancellationToken cancellationToken)
    {
        await Task.Yield();
        Console.WriteLine($"[AfterAsync] Request {requestId}: result='{result}'");
    }
}

// Async fluent demo: same shape, with multiple hooks and error handling
public static class TemplateAsyncFluentDemo
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var tpl = AsyncTemplate<int, string>
            .Create(async (id, ct) =>
            {
                await Task.Delay(15, ct);
                return id < 0 ? throw new InvalidOperationException("invalid id") : $"VAL-{id}";
            })
            .Before((id, _) =>
            {
                Console.WriteLine($"[BeforeAsync] id={id}");
                return ValueTask.CompletedTask;
            })
            .After((id, res, _) =>
            {
                Console.WriteLine($"[AfterAsync] id={id}, res={res}");
                return ValueTask.CompletedTask;
            })
            .OnError((id, err, _) =>
            {
                Console.WriteLine($"[ErrorAsync] id={id}, err={err}");
                return ValueTask.CompletedTask;
            })
            .Synchronized(false) // demonstrate opt-out
            .Build();

        // Success
        var (ok1, res1, err1) = await tpl.TryExecuteAsync(42, cancellationToken);
        Console.WriteLine(ok1 ? $"OK: {res1}" : $"ERR: {err1}");

        // Failure
        var (ok2, res2, err2) = await tpl.TryExecuteAsync(-1, cancellationToken);
        Console.WriteLine(ok2 ? $"OK: {res2}" : $"ERR: {err2}");
    }
}