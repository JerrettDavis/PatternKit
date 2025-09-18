using System.Runtime.CompilerServices;

namespace PatternKit.Examples.MediatorDemo;

// Sink used by demo handlers/behaviors to record execution
public interface IMediatorDemoSink { List<string> Log { get; } }
public sealed class MediatorDemoSink : IMediatorDemoSink { public List<string> Log { get; } = new(); }

// ----- Commands -----
public readonly record struct PingCmd(int Value) : ICommand<string>;
public sealed class PingHandler(IMediatorDemoSink sink) : ICommandHandler<PingCmd, string>
{
    public ValueTask<string> Handle(PingCmd request, CancellationToken ct)
        => new($"pong:{request.Value}");
}

public sealed record EchoCmd(string Text) : ICommand<string>;
public sealed class EchoHandler(IMediatorDemoSink sink) : ICommandHandler<EchoCmd, string>
{
    public ValueTask<string> Handle(EchoCmd request, CancellationToken ct) => new(request.Text);
}

public readonly record struct SumCmd(int A, int B) : ICommand<int>;
public sealed class SumHandler : ICommandHandler<SumCmd, int>
{
    public ValueTask<int> Handle(SumCmd request, CancellationToken ct) => new(request.A + request.B);
}

// ----- Notifications -----
public sealed record UserCreated(string Name) : INotification;
public sealed class WelcomeEmailHandler(IMediatorDemoSink sink) : INotificationHandler<UserCreated>
{
    public ValueTask Handle(UserCreated notification, CancellationToken ct)
    { sink.Log.Add($"email:welcome:{notification.Name}"); return default; }
}
public sealed class AuditLogHandler(IMediatorDemoSink sink) : INotificationHandler<UserCreated>
{
    public ValueTask Handle(UserCreated notification, CancellationToken ct)
    { sink.Log.Add($"audit:user-created:{notification.Name}"); return default; }
}

// ----- Streaming -----
public readonly record struct CountUpCmd(int From, int Count) : IStreamRequest<int>;
public sealed class CountUpHandler : IStreamRequestHandler<CountUpCmd, int>
{
    public async IAsyncEnumerable<int> Handle(CountUpCmd request, [EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 0; i < request.Count; i++) { await Task.Yield(); yield return request.From + i; }
    }
}

// ----- Pipeline behaviors -----
// Open-generic behavior: logs before/after for any ICommand<TResponse>
public sealed class LoggingBehavior<TRequest, TResponse>(IMediatorDemoSink sink) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    public async ValueTask<TResponse> Handle(TRequest request, CancellationToken ct, Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
    {
        sink.Log.Add($"before:{typeof(TRequest).Name}");
        var res = await next(request, ct).ConfigureAwait(false);
        sink.Log.Add($"after:{typeof(TRequest).Name}:{res}");
        return res;
    }
}

// Closed generic behavior targeting SumCmd specifically (in addition to LoggingBehavior)
public sealed class SumCmdBehavior(IMediatorDemoSink sink) : IPipelineBehavior<SumCmd, int>
{
    public async ValueTask<int> Handle(SumCmd request, CancellationToken ct, Func<SumCmd, CancellationToken, ValueTask<int>> next)
    {
        sink.Log.Add("sum:before");
        var res = await next(request, ct).ConfigureAwait(false);
        sink.Log.Add($"sum:after:{res}");
        return res;
    }
}
