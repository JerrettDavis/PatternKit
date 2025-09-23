# Behavioral.Iterator.WindowSequence

`WindowSequence.Windows(...)` is a fluent, allocation-light helper that produces **sliding or striding windows** over any `IEnumerable<T>`.
It demonstrates how you can extend the classic Iterator pattern with richer semantics (window size, stride, partial trailing
windows, reusable buffers) while still exposing a standard, lazy, LINQ-friendly API.

---
## TL;DR
```csharp
using PatternKit.Behavioral.Iterator;

var windows = Enumerable.Range(1, 7)
    .Windows(size: 3, stride: 1)   // slide 1 each time
    .Select(w => string.Join(',', w.ToArray()));
// -> ["1,2,3", "2,3,4", "3,4,5", "4,5,6", "5,6,7"]
```

Stride 2 (skip elements between starts):
```csharp
var stepped = Enumerable.Range(1, 9)
    .Windows(size: 4, stride: 2)
    .Select(w => string.Join('-', w.ToArray()));
// -> ["1-2-3-4", "3-4-5-6", "5-6-7-8"]
```

Include trailing partial window:
```csharp
var partials = new[]{1,2,3,4,5}
    .Windows(size: 3, stride: 3, includePartial: true)
    .Select(w => $"[{string.Join(',', w.ToArray())}] (partial={w.IsPartial})");
// -> ["[1,2,3] (partial=False)", "[4,5] (partial=True)"]
```

Reuse a buffer (zero alloc per full window, but you MUST copy if you retain data):
```csharp
var reused = Enumerable.Range(1, 6)
    .Windows(size: 3, reuseBuffer: true)
    .Select(w => w.ToArray())     // force snapshot copy each time
    .ToList();
// windows: [1,2,3], [2,3,4], [3,4,5], [4,5,6]
```

---
## API Shape
```csharp
public static class WindowSequence
{
    public static IEnumerable<Window<T>> Windows<T>(
        this IEnumerable<T> source,
        int size,
        int stride = 1,
        bool includePartial = false,
        bool reuseBuffer = false);

    public readonly struct Window<T>
    {
        public int Count { get; }
        public bool IsPartial { get; }
        public bool IsBufferReused { get; }
        public T this[int index] { get; }
        public T[] ToArray();              // always copies
        public IEnumerator<T> GetEnumerator();
    }
}
```

### Parameters
* `size` – required window length (> 0).
* `stride` – elements to advance between successive window starts (default 1).
* `includePartial` – include trailing window with `Count < size`.
* `reuseBuffer` – reuse a single backing array for *full* windows (partial windows still copy). Call `ToArray()` to snapshot.

---
## Semantics & Guarantees
| Aspect | Behavior |
|--------|----------|
| Enumeration | Single pass over source; deferred execution. |
| Overlap | Controlled by `stride` (1 = full overlap sliding). |
| Partial | Disabled by default; enable via `includePartial`. |
| Reuse | When `reuseBuffer=true`, full windows share the same array (treat returned `Window` as ephemeral unless copied). |
| Safety | `ToArray()` always returns an independent copy. |

---
## Use Cases
* Batch / micro-batch analytics (moving averages, rolling sums)
* Stream framing (fixed length records with overlap)
* Feature extraction windows (ML preprocessing)
* Temporal rule evaluation (N previous events)

---
## Example: Rolling Average
```csharp
double RollingAverage(IEnumerable<int> src, int windowSize)
    => src.Windows(windowSize)
          .Select(w => w.ToArray().Average())
          .LastOrDefault();
```

## Example: Find First Increasing Triple
```csharp
var triple = nums.Windows(size:3)
    .Select(w => w.ToArray())
    .FirstOrDefault(arr => arr[0] < arr[1] && arr[1] < arr[2]);
```

---
## Performance Notes
* Uses a `Queue<T>` internally for clarity – O(size) per full window snapshot when copying.
* For extremely hot paths, a ring buffer variant would reduce copy cost; this implementation prioritizes readability.
* Set `reuseBuffer:true` to avoid per-window allocations (copy yourself if persistent storage is required).

---
## Gotchas
| Gotcha | Explanation |
|--------|-------------|
| Buffer reuse surprises | Mutating or retaining reused buffer contents without copying will show later window values. Always call `ToArray()` if you persist. |
| Large stride + partial disabled | You may silently drop tail elements—enable `includePartial` if you need them. |
| size or stride <= 0 | Immediate `ArgumentOutOfRangeException`. |

---
## See Also
* [ReplayableSequence](./replayablesequence.md) – multi-cursor, lookahead iteration.
* LINQ standard operators for reference semantics.

