# Behavioral.Iterator.Flow

`Flow<T>` is a fluent, **pull-based functional pipeline** that chains transformations (Map / Filter / FlatMap / Tee)
and can be upgraded into a replayable, **forkable** / **branchable** stream via `Share()`. It complements
`ReplayableSequence` (cursor-based random access) and `WindowSequence` (batch framing) by focusing on *functional
composition* and *selective multi-consumer fan‑out*.

Built to feel like a tiny synchronous analogue of an RxJS `pipe`—but intentionally minimal:
no subjects, scheduling, or async—just pure, lazy, allocation-light enumeration.

---
## TL;DR
```csharp
using PatternKit.Behavioral.Iterator;

var flow = Flow<int>.From(Enumerable.Range(1, 8))
    .Map(x => x * 2)          // 2,4,6,8,10,12,14,16
    .Filter(x => x % 4 == 0)  // 4,8,12,16
    .FlatMap(x => new[]{x, x})// 4,4,8,8,12,12,16,16
    .Tee(x => Console.WriteLine($"debug:{x}"));

var list = flow.ToList();
```

Fork & branch without re-enumerating upstream:
```csharp
var shared = Flow<int>.From(Enumerable.Range(1, 6)).Share();
var odds  = shared.Branch(x => x % 2 == 0).False; // 1,3,5
var evens = shared.Fork().Filter(x => x % 2 == 0); // 2,4,6
```

---
## Core Types
```csharp
public sealed class Flow<T> : IEnumerable<T>
{
    Flow<TOut> Map<TOut>(Func<T,TOut> f);
    Flow<T>    Filter(Func<T,bool> pred);
    Flow<TOut> FlatMap<TOut>(Func<T,IEnumerable<TOut>> f);
    Flow<T>    Tee(Action<T> sideEffect);
    SharedFlow<T> Share(); // enable replay / forks / branching
}

public sealed class SharedFlow<T>
{
    Flow<T> Fork();                 // new independent reader
    Flow<T>[] Fork(int count);      // multiple readers
    (Flow<T> True, Flow<T> False) Branch(Func<T,bool> predicate); // partition
    Flow<TOut> Map<TOut>(Func<T,TOut> f); // convenience (delegates to Fork)
    Flow<T> Filter(Func<T,bool> pred);
    Flow<T> AsFlow();
}

public static class FlowExtensions
{
    TAcc Fold<T,TAcc>(this Flow<T> flow, TAcc seed, Func<TAcc,T,TAcc> folder);
    Option<T> FirstOption<T>(this Flow<T> flow);
}
```

---
## Design Notes
| Concern | Approach |
|---------|----------|
| Laziness | All operators defer until enumeration. |
| Sharing | `Share()` converts pipeline to a `ReplayableSequence` so forks replay without re-running upstream. |
| Fork cost | Fork = cheap cursor snapshot + fresh `Flow` wrapper. |
| Branching | `Branch(predicate)` enumerates once and yields two filtered views. |
| Safety | Single-thread (no internal locking). |
| Allocation | Mostly delegates; shared mode buffers elements once. |

---
## Example: Two Independent Projections
```csharp
var shared = Flow<string>.From(new[]{"alpha","beta","gamma"}).Share();
var lengths = shared.Fork().Map(s => s.Length).ToList();   // [5,4,5]
var upper   = shared.Fork().Map(s => s.ToUpperInvariant()).ToList();
```

## Example: Partition & Aggregate
```csharp
var sf = Flow<int>.From(Enumerable.Range(1,10)).Share();
var (evenFlow, oddFlow) = sf.Branch(i => i % 2 == 0);
var evenSum = evenFlow.Fold(0, (a,x) => a + x); // 2+4+...+10 = 30
var oddMax  = oddFlow.Fold(0, Math.Max);        // 9
```

## Example: FirstOption & Early Exit
```csharp
var maybe = Flow<int>.From(Enumerable.Range(50, 5))
    .Filter(x => x > 52)
    .FirstOption();  // Some(53)
```

---
## When to Use Which Iterator?
| Need | Use |
|------|-----|
| Multi-pass cursors & lookahead | `ReplayableSequence` |
| Sliding / striding batch windows | `WindowSequence` |
| Functional chain + forks / branches | `Flow` |

They are complementary; `Flow.Share()` internally *uses* `ReplayableSequence` to ensure upstream is enumerated only once.

---
## Gotchas
| Gotcha | Mitigation |
|--------|-----------|
| Forgetting `Share()` before forking | Without `Share()`, re-enumerating the same `Flow` re-runs upstream. Call `Share()` if side-effects/expensive sources need single pass. |
| Mutation inside `Tee` | Keep side-effects idempotent / safe for possible replays (during debugging). |
| Large retained shared flows | Buffered elements remain in memory until GC; dispose references if done early. |

---
## Future Ideas
* AsyncFlow (Task/ValueTask aware operators)
* Parallelizing `FlatMap` merges (controlled degree of concurrency)
* Error handling / recovery operators (TryMap, Recover)

---
## See Also
* [ReplayableSequence](./replayablesequence.md)
* [WindowSequence](./windowsequence.md)
* Strategy / Chain patterns for higher-level branching or rule evaluation.

