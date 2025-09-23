# Behavioral.Iterator.ReplayableSequence

`ReplayableSequence<T>` is a fluent, allocation-light helper that lets you treat any forward `IEnumerable<T>` as a
multi-pass, lookahead, forkable stream – **without** pre-materializing everything into an array or repeatedly
re-enumerating the original source.

It augments the classic Iterator pattern by giving you *struct cursors* that:

* Advance independently (fork at any position)
* Support `Peek()` and arbitrary positive `Lookahead(offset)`
* Can be turned into (lazy) `IEnumerable<T>` sequences at any time
* Cooperatively fill a shared on-demand buffer (each underlying element is pulled at most once)
* Interop naturally with LINQ (and add a couple of extra fluent helpers like `Batch`)

---
## TL;DR

```csharp
using PatternKit.Behavioral.Iterator;

var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 5));
var c1 = seq.GetCursor();

// Read two values
c1.TryNext(out var a, out c1); // a = 1
c1.TryNext(out var b, out c1); // b = 2

// Fork (branch) without consuming more of c1
var c2 = c1.Fork();

// c2 can scan ahead independently
var la = c2.Lookahead(0).OrDefault(); // 3
var lb = c2.Lookahead(1).OrDefault(); // 4

// Enumerate remaining from c2 (3,4,5) with LINQ
var evens = c2.Where(x => x % 2 == 0).ToList(); // [4]

// c1 is still parked at element 3
c1.TryNext(out var third, out c1); // 3
```

---
## Why not just use `IEnumerable<T>` directly?

Typical options when you need speculative / multi-pass logic:

| Need | Common Approach | Downsides |
|------|-----------------|-----------|
| Lookahead | `Queue<T>` + manual buffering | Manual complexity, error-prone indices |
| Backtracking / fork | Re-enumerate source multiple times | Re-runs expensive producers / I/O |
| Multiple cursors | Materialize to `List<T>` | Upfront cost + full allocation |

`ReplayableSequence<T>` gives *pay-as-you-go* buffering: only what you actually touch is stored. Perfect for:

* Tokenizers / lightweight parsers
* Rule engines scanning the same prefix in different ways
* DSL interpreters
* Streaming transforms where limited rewind is handy
* Batch framing or chunked processing with optional lookahead

---
## Core API

```csharp
public sealed class ReplayableSequence<T>
{
    public static ReplayableSequence<T> From(IEnumerable<T> source);
    public Cursor GetCursor();
    public IEnumerable<T> AsEnumerable();

    public readonly struct Cursor
    {
        int Position { get; }
        Cursor Fork();
        bool TryNext(out T value, out Cursor next); // immutable advance
        bool Peek(out T value);                     // no advance
        Option<T> Lookahead(int offset);            // offset >= 0
        IEnumerable<T> AsEnumerable();              // enumerate from current position (cursor itself not moved)
    }
}

public static class ReplayableSequenceExtensions
{
    IEnumerable<TOut> Select<T,TOut>(Cursor c, Func<T,TOut> f);
    IEnumerable<T> Where<T>(Cursor c, Func<T,bool> pred);
    IEnumerable<IReadOnlyList<T>> Batch<T>(Cursor c, int size);
    IEnumerable<T> AsEnumerable<T>(ReplayableSequence<T> seq); // convenience
}
```

### Design Notes
* Cursor is a readonly struct → copying / forking is cheap.
* `TryNext` returns a *new* advanced cursor (functional style) to avoid hidden mutation.
* All cursors share a single underlying buffer – thread confinement is assumed (not thread-safe).
* `Lookahead(n)` ensures the buffer contains index `Position + n` (if possible) and returns an `Option<T>`.
* After the source is fully drained the buffer becomes a random-access immutable snapshot for all cursors.

---
## Examples

### 1. Token-style lookahead
```csharp
var letters = ReplayableSequence<char>.From("abcde".ToCharArray());
var cur = letters.GetCursor();

// Need 2-char decision?
if (cur.Lookahead(0).OrDefault() == 'a' && cur.Lookahead(1).OrDefault() == 'b')
{
    cur.TryNext(out _, out cur); // consume 'a'
    cur.TryNext(out _, out cur); // consume 'b'
    // ... parse AB token
}
```

### 2. Fork speculative parse branch
```csharp
var seq = ReplayableSequence<int>.From(new[]{1,2,3,9,9});
var p = seq.GetCursor();
var attempt = p.Fork();

// Try read three numbers summing to 6
int sum = 0; int read = 0;
while (read < 3 && attempt.TryNext(out var v, out attempt)) { sum += v; read++; }

if (sum == 6) // success → commit (just adopt attempt cursor)
    p = attempt; // original p now advanced logically
// else: discard attempt (p unchanged)
```

### 3. Batch processing (streaming window framing)
```csharp
var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 10));
var c = seq.GetCursor();
foreach (var batch in c.Batch(4))
{
    Console.WriteLine(string.Join(',', batch));
}
// 1,2,3,4
// 5,6,7,8
// 9,10
```

### 4. Mixed LINQ + cursor ops
```csharp
var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 8));
var c = seq.GetCursor();

// Peek without moving
c.Peek(out var first); // 1

// Use Where on a cursor (does not move the original beyond enumeration copy)
var odds = c.Where(x => x % 2 == 1).Take(3).ToList(); // [1,3,5]

// c still at position 0 (functional enumeration)
```

---
## Testing Invariants
| Invariant | Meaning |
|-----------|---------|
| Single production | Underlying source element is produced (MoveNext true) at most once. |
| Idempotent forks | Forking does not mutate either cursor. |
| Safe lookahead | `Lookahead(k)` never advances state. |
| Lazy buffering | No elements buffered until requested. |

---
## Gotchas & Tips
* Negative offsets → `ArgumentOutOfRangeException` (failing fast clarifies bugs).
* Avoid very large unbounded lookahead if your source is huge (each requested index must be buffered).
* Not thread-safe: confine a sequence + its cursors to one logical consumer (or add external synchronization).
* `Batch` yields arrays per chunk (copy for immutability); if you need pooled buffers, add a specialized variant.

---
## Comparison
| Approach | Pros | Cons |
|----------|------|------|
| Plain re-enumeration | Simple | Re-runs side effects / I/O, duplicate work |
| Materialize `List<T>` | Random access | Upfront full allocation |
| ReplayableSequence | On-demand, multi-cursor, lookahead | Buffer growth unbounded if you read far |

---
## See also
* Standard Iterator pattern (this is an *enriched* variant)
* Chain / Strategy patterns when composing behaviors over streamed elements
* `Option<T>` (used for `Lookahead`) for fluent presence/absence handling

