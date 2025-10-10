# In-Process Event Hub with Observer<TEvent>

This demo shows how to build a minimal, typed event hub using PatternKit’s Observer.

What it demonstrates
- Typed publish/subscribe: `Observer<TEvent>`
- Predicate-based subscriptions (per-subscriber filters)
- Error policies (aggregate, throw-first, swallow) + error sink
- Unsubscribe via `IDisposable`

Where to look
- Code: `src/PatternKit.Examples/ObserverDemo/`
  - `SimpleEventHub.cs`: small wrapper and a sample `UserEvent` record
- Tests: `test/PatternKit.Tests/Behavioral/Observer/ObserverTests.cs`

Quick start
```csharp
using PatternKit.Behavioral.Observer;

// Build an observer hub for string events
var hub = Observer<string>.Create()
    .OnError(static (ex, in msg) => Console.Error.WriteLine($"handler failed: {ex.Message} on '{msg}'"))
    .ThrowAggregate() // default policy
    .Build();

// Subscribe all messages
var subAll = hub.Subscribe(static (in string msg) => Console.WriteLine($"ALL: {msg}"));

// Subscribe only warnings
var subWarn = hub.Subscribe(static (in string msg) => msg.StartsWith("warn:", StringComparison.Ordinal),
                            static (in string msg) => Console.WriteLine($"WARN: {msg}"));

hub.Publish("hello");
hub.Publish("warn: low-disk");

subWarn.Dispose(); // unsubscribe
```

Error handling policies
- ThrowAggregate (default): run everyone; collect exceptions and throw a single `AggregateException` at the end.
- ThrowFirstError: stop at the first exception (later handlers don’t run).
- SwallowErrors: never throw; failures go only to the error sink.

Example: swallow errors so publishing never throws
```csharp
var hub = Observer<int>.Create()
    .OnError(static (ex, in n) => Console.Error.WriteLine($"fail({n}): {ex.Message}"))
    .SwallowErrors()
    .Build();

hub.Subscribe(static (in int _) => throw new InvalidOperationException("boom"));
hub.Subscribe(static (in int n) => Console.WriteLine($"ok:{n}"));

hub.Publish(42); // prints: ok:42, no exception to caller
```

Notes
- Subscriptions are copy-on-write; publishing iterates a snapshot without locks.
- Registration order is preserved.
- Subscribing/unsubscribing during a publish affects the next publish, not the current one.
- Delegates use `in T` for zero-copy struct pass-through.

Run the tests
```bash
# From the repo root
dotnet build PatternKit.slnx -c Debug
dotnet test PatternKit.slnx -c Debug
```

