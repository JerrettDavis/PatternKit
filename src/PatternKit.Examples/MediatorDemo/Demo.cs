using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace PatternKit.Examples.MediatorDemo;

/// <summary>
/// Sink used by demo handlers / behaviors to record execution side-effects for tests and documentation.
/// </summary>
public interface IMediatorDemoSink
{
    List<string> Log { get; }
}

/// <summary>
/// In-memory implementation of <see cref="IMediatorDemoSink"/> collecting log entries.
/// </summary>
public sealed class MediatorDemoSink : IMediatorDemoSink
{
    public List<string> Log { get; } = [];
}

// ----- Commands -----

/// <summary>
/// Command that returns a formatted pong string including the provided integer value.
/// </summary>
/// <param name="Value">Integer to echo back inside the pong response.</param>
public readonly record struct PingCmd(int Value) : ICommand<string>;

/// <summary>
/// Handles <see cref="PingCmd"/> by returning a formatted pong string.
/// </summary>
public sealed class PingHandler : ICommandHandler<PingCmd, string>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(PingCmd request, CancellationToken ct)
        => new($"pong:{request.Value}");
}

/// <summary>
/// Command that echoes the provided text as its response.
/// </summary>
/// <param name="Text">Text to echo.</param>
public sealed record EchoCmd(string Text) : ICommand<string>;

/// <summary>
/// Handles <see cref="EchoCmd"/> by returning the original text.
/// </summary>
[UsedImplicitly]
public sealed class EchoHandler : ICommandHandler<EchoCmd, string>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(EchoCmd request, CancellationToken ct) => new(request.Text);
}

/// <summary>
/// Command that sums two integers and returns the arithmetic result.
/// </summary>
/// <param name="A">First addend.</param>
/// <param name="B">Second addend.</param>
public readonly record struct SumCmd(int A, int B) : ICommand<int>;

/// <summary>
/// Handles <see cref="SumCmd"/> by returning the sum of <see cref="SumCmd.A"/> and <see cref="SumCmd.B"/>.
/// </summary>
[UsedImplicitly]
public sealed class SumHandler : ICommandHandler<SumCmd, int>
{
    /// <inheritdoc />
    public ValueTask<int> Handle(SumCmd request, CancellationToken ct) => new(request.A + request.B);
}

// ----- Notifications -----

/// <summary>
/// Notification representing that a new user has been created.
/// </summary>
/// <param name="Name">User name.</param>
public sealed record UserCreated(string Name) : INotification;

/// <summary>
/// Sends a welcome email (simulated) for <see cref="UserCreated"/> notifications.
/// </summary>

public sealed class WelcomeEmailHandler(IMediatorDemoSink sink) : INotificationHandler<UserCreated>
{
    /// <inheritdoc />
    public ValueTask Handle(UserCreated notification, CancellationToken ct)
    {
        sink.Log.Add($"email:welcome:{notification.Name}");
        return default;
    }
}

/// <summary>
/// Writes an audit log entry (simulated) for <see cref="UserCreated"/> notifications.
/// </summary>
public sealed class AuditLogHandler(IMediatorDemoSink sink) : INotificationHandler<UserCreated>
{
    /// <inheritdoc />
    public ValueTask Handle(UserCreated notification, CancellationToken ct)
    {
        sink.Log.Add($"audit:user-created:{notification.Name}");
        return default;
    }
}

// ----- Streaming -----

/// <summary>
/// Streaming command that counts upward from <see cref="From"/> emitting <see cref="Count"/> consecutive integers.
/// </summary>
/// <param name="From">Starting value (inclusive).</param>
/// <param name="Count">Number of items to produce.</param>
public readonly record struct CountUpCmd(int From, int Count) : IStreamRequest<int>;

/// <summary>
/// Handles <see cref="CountUpCmd"/> by producing an async sequence of integers.
/// </summary>
public sealed class CountUpHandler : IStreamRequestHandler<CountUpCmd, int>
{
    /// <inheritdoc />
    public async IAsyncEnumerable<int> Handle(CountUpCmd request, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < request.Count; i++)
        {
            await Task.Yield();
            yield return request.From + i;
        }
    }
}

// ----- Pipeline behaviors -----

/// <summary>
/// Open-generic logging behavior that records before / after entries (including result) for any command.
/// </summary>
/// <typeparam name="TRequest">Command type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse>(IMediatorDemoSink sink) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(TRequest request, CancellationToken ct, Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
    {
        sink.Log.Add($"before:{typeof(TRequest).Name}");
        var res = await next(request, ct).ConfigureAwait(false);
        sink.Log.Add($"after:{typeof(TRequest).Name}:{res}");
        return res;
    }
}

/// <summary>
/// Closed generic behavior targeting <see cref="SumCmd"/> to demonstrate composing multiple behaviors.
/// </summary>
public sealed class SumCmdBehavior(IMediatorDemoSink sink) : IPipelineBehavior<SumCmd, int>
{
    /// <inheritdoc />
    public async ValueTask<int> Handle(SumCmd request, CancellationToken ct, Func<SumCmd, CancellationToken, ValueTask<int>> next)
    {
        sink.Log.Add("sum:before");
        var res = await next(request, ct).ConfigureAwait(false);
        sink.Log.Add($"sum:after:{res}");
        return res;
    }
}