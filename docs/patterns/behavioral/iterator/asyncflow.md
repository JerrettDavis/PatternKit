# Behavioral.Iterator.AsyncFlow

`AsyncFlow<T>` is the asynchronous counterpart to [`Flow<T>`](./flow.md): a **lazy, pull-based functional pipeline** over
`IAsyncEnumerable<T>` with replay & branching via `Share()`. It lets you compose async transformations without manual
plumbing, and then optionally create multiple consumers (forks) that each read the same buffered stream exactly once.

---
## When to choose AsyncFlow
| Use | Instead of | Why |
|-----|------------|-----|
| Composing async pure transformations | Ad-hoc `await foreach` chains | Fluent, reusable, testable pipelines |
| Multiple independent async readers | Re-enumerating a cold async source | `Share()` buffers once (single upstream pass) |
| Partitioning an async stream | Building two separate filters (double work) | `Branch()` partitions during a single replay |

If you need synchronous cursors / lookahead: use [`ReplayableSequence`](./replayablesequence.md). If you need sliding
windows: see [`WindowSequence`](./windowsequence.md). For synchronous functional chains: [`Flow`](./flow.md).

---
## TL;DR
```csharp
using PatternKit.Behavioral.Iterator;

var numbers = AsyncFlow<int>.From(ProduceAsync());

var processed = numbers
    .Map(x => x * 3)
    .Filter(x => x % 2 == 0)
    .FlatMap(x => RepeatAsync(x, count: 2))
    .Tee(x => Console.WriteLine($"debug:{x}"));

await foreach (var v in processed)
    /* use v */;

// Share for multi-consumer fan-out (single upstream pass)
var shared = processed.Share();
var doubled = shared.Fork().Map(x => x * 2);   // independent consumer
var evens   = shared.Fork().Filter(x => x % 2 == 0);

var sum = await doubled.FoldAsync(0, (a, v) => a + v);
var firstEven = await evens.FirstOptionAsync();
```

---
## Core API
```csharp
public sealed class AsyncFlow<T> : IAsyncEnumerable<T>
{
    AsyncFlow<TOut> Map<TOut>(Func<T,TOut> f);
    AsyncFlow<T>    Filter(Func<T,bool> pred);
    AsyncFlow<TOut> FlatMap<TOut>(Func<T,IAsyncEnumerable<TOut>> f);
    AsyncFlow<T>    Tee(Action<T> sideEffect);
    SharedAsyncFlow<T> Share();
}

public sealed class SharedAsyncFlow<T>
{
    AsyncFlow<T> Fork();
    (AsyncFlow<T> True, AsyncFlow<T> False) Branch(Func<T,bool> predicate);
    // (Map / Filter still available via Fork() chaining)
}

public static class AsyncFlowExtensions
{
    ValueTask<TAcc> FoldAsync<T,TAcc>(this AsyncFlow<T> flow, TAcc seed, Func<TAcc,T,TAcc> folder, CancellationToken = default);
    ValueTask<Option<T>> FirstOptionAsync<T>(this AsyncFlow<T> flow, CancellationToken = default);
}
```

### Differences from `Flow<T>`
| Aspect | Flow | AsyncFlow |
|--------|------|-----------|
| Source | `IEnumerable<T>` | `IAsyncEnumerable<T>` |
| FlatMap | `Func<T,IEnumerable<U>>` | `Func<T,IAsyncEnumerable<U>>` |
| Replay Engine | `ReplayableSequence<T>` | Custom `AsyncReplayBuffer<T>` |
| Terminal ops | LINQ or Fold/FirstOption | `FoldAsync` / `FirstOptionAsync` |
| Thread-safety | Lock around buffer fill | Lock + waiter coordination (TCS) |

---
## Replay & Forking Semantics
Calling `Share()` wraps the upstream async iterator in an *async replay buffer*:
* First fork enumerates and buffers as needed.
* Subsequent forks read already-buffered items up to the furthest consumer position.
* The upstream `MoveNextAsync()` is called **at most once per element** across all forks.

This enables fan-out patterns without re-triggering expensive I/O or side effects.

---
## Branching
```csharp
var shared = AsyncFlow<int>.From(RangeAsync(1, 10)).Share();
var (evenFlow, oddFlow) = shared.Branch(i => i % 2 == 0);
var evenSum = await evenFlow.FoldAsync(0, (a,v) => a + v); // 2+4+6+8+10
var firstOdd = await oddFlow.FirstOptionAsync();           // Some(1)
```
`Branch` performs a *single* pass: partition logic runs while elements are replayed, not by re-enumerating.

---
## Cancellation & Backpressure
AsyncFlow respects cancellation tokens in all internal loops (`WithCancellation`). There is *no built-in backpressure*
mechanism—if downstream is slower than upstream, items accumulate in the replay buffer until consumed.

For high-volume streams consider customizing the replay buffer with a bounded policy or spill strategy.

---
## Error Propagation
If upstream throws:
* The replay buffer captures the exception.
* All current and future forks observe the same exception at the appropriate index.
* No partial / inconsistent state is exposed (buffer is append-only).

---
## Example: Time-Stamped Processing
```csharp
var processed = AsyncFlow<int>.From(RangeAsync(1, 5))
    .Map(x => (Value: x, Stamp: DateTimeOffset.UtcNow))
    .Filter(x => x.Value != 3)
    .FlatMap(tuple => EmitWithDelayAsync(tuple, TimeSpan.FromMilliseconds(25)))
    .Share();

var (high, low) = processed.Branch(t => t.Value >= 4);
var highCount = await high.FoldAsync(0, (a, _) => a + 1); // 2
```

---
## Performance Notes
| Factor | Notes |
|--------|-------|
| Memory | Buffer grows with produced elements until completion (no eviction). |
| Locking | Single lock around mutation + waiter coordination; fine for moderate concurrency. |
| Allocation | Delegates + per awaited item; no per-item Task creation (yield state machine only). |
| Fairness | No scheduling policy; forks progress as they pull. |

---
## Limitations / Future Ideas
* No async-aware `MapAsync(Func<T,Task<U>>)` operator yet (can wrap in FlatMap).
* No built-in throttling or rate limiting; pair with external flow control.
* Potential enhancement: Bounded / windowed replay buffer.

---
## Gotchas
| Gotcha | Explanation | Mitigation |
|--------|-------------|------------|
| Large unbounded streams | Full retention in buffer | Introduce custom chunking or GC-friendly segmentation |
| Very slow consumer | Memory grows until consumer catches up | Split pipeline earlier or add batching |
| Side effects in Tee | Will not repeat for forks (they happen once upstream) | Place side-effects before `Share()` if you want single execution |

---
## Interop & Mixing
* Convert synchronous pipelines: `AsyncFlow.From(synchronousSequence.ToAsync())` (write a small extension).
* Feed into `Flow<T>` by materializing (`await flow.ToListAsync()`) then `Flow.From(list)`.
* Combine with `ReplayableSequence.AsAsyncEnumerable()` for uniform async surface (when targeting modern TFMs).

---
## See Also
* [Flow](./flow.md) – synchronous functional pipeline.
* [ReplayableSequence](./replayablesequence.md) – multi-pass / lookahead.
* [WindowSequence](./windowsequence.md) – sliding & striding windows.

