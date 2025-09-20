# Replacing MediatR with PatternKit.Mediator — DI Scanning + Behaviors (PoC)

This demo shows how to wire PatternKit’s allocation-light Mediator into a typical .NET app with DI scanning, request/notification/stream handlers, and pipeline behaviors — near-parity with MediatR.

What it demonstrates
- DI scanning for handlers and pipeline behaviors via a single extension
- Send (request/response), Publish (fan-out notifications), and Stream (IAsyncEnumerable)
- Open-generic pipeline behaviors (e.g., LoggingBehavior<TRequest,TResponse>)
- Zero-alloc fast path via ValueTask and in parameters

Where to look
- Code: src/PatternKit.Examples/MediatorDemo/
  - Abstractions.cs: MediatR-like abstractions and IServiceCollection.AddPatternKitMediator
  - Demo.cs: sample commands, notifications, stream requests, handlers, and a logging behavior
- Tests: test/PatternKit.Examples.Tests/MediatorDemo/MediatorDemoTests.cs (TinyBDD scenarios)

Quick start
```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.MediatorDemo;

var services = new ServiceCollection();
services.AddSingleton<IMediatorDemoSink, MediatorDemoSink>();
// Scan current assembly (or provide others)
services.AddPatternKitMediator(typeof(PingHandler).Assembly);
var sp = services.BuildServiceProvider();

var mediator = sp.GetRequiredService<IAppMediator>();

// Send (request/response)
var pong = await mediator.Send(new PingCmd(7));   // "pong:7"
var sum  = await mediator.Send(new SumCmd(2, 3)); // 5

// Publish (fan-out notifications)
await mediator.Publish(new UserCreated("Ada")); // both handlers run

// Stream (IAsyncEnumerable), netstandard2.1+/netcoreapp3.0+
await foreach (var i in mediator.Stream(new CountUpCmd(3, 4)))
    Console.WriteLine(i); // 3, 4, 5, 6
```

Handlers
```csharp
public readonly record struct PingCmd(int Value) : ICommand<string>;
public sealed class PingHandler : ICommandHandler<PingCmd, string>
{
    public ValueTask<string> Handle(PingCmd r, CancellationToken ct) => new($"pong:{r.Value}");
}

public sealed record UserCreated(string Name) : INotification;
public sealed class AuditLogHandler : INotificationHandler<UserCreated>
{
    public ValueTask Handle(UserCreated n, CancellationToken ct) { /* log */ return default; }
}

public readonly record struct CountUpCmd(int From, int Count) : IStreamRequest<int>;
public sealed class CountUpHandler : IStreamRequestHandler<CountUpCmd, int>
{
    public async IAsyncEnumerable<int> Handle(CountUpCmd r, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    { for (int i = 0; i < r.Count; i++) { await Task.Yield(); yield return r.From + i; } }
}
```

Pipeline behaviors
```csharp
public sealed class LoggingBehavior<TRequest,TResponse>(IMediatorDemoSink sink) : IPipelineBehavior<TRequest,TResponse>
    where TRequest : ICommand<TResponse>
{
    public async ValueTask<TResponse> Handle(TRequest req, CancellationToken ct, Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
    {
        sink.Log.Add($"before:{typeof(TRequest).Name}");
        var res = await next(req, ct);
        sink.Log.Add($"after:{typeof(TRequest).Name}:{res}");
        return res;
    }
}
```

DI scanning
- Call services.AddPatternKitMediator(assemblies) to scan types implementing:
  - ICommandHandler<TRequest,TResponse>
  - INotificationHandler<TNotification>
  - IStreamRequestHandler<TRequest,TItem>
  - IPipelineBehavior<TRequest,TResponse> (open generic supported)
- Handlers/behaviors are registered transient; IMediator facade (IAppMediator) is scoped.

Near-parity with MediatR
- Send/Publish/Stream APIs via IAppMediator
- Pipeline behaviors (open-generic) with before/after orchestration
- DI scanning for automatic wire-up

Differences
- Uses PatternKit.Behavioral.Mediator under the hood (ValueTask everywhere, in parameters, minimal allocations)
- The IAppMediator is an example facade; you can adapt names/signatures to match your project.

Run the demo tests
```bash
# From the repo root
dotnet build PatternKit.slnx -c Debug
dotnet test PatternKit.slnx -c Debug
```
Tests include send/publish/stream and behavior ordering assertions.

