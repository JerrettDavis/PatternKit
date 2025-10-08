# Flyweight Pattern — Share Heavy, Repeated, Immutable State

> **TL;DR**: Flyweight splits objects into **intrinsic state** (shared & immutable) and **extrinsic state** (supplied at use time) so you don’t allocate duplicate heavy objects. PatternKit provides a **fluent, allocation‑light, thread‑safe** implementation with predictable performance.

---
## 1. Motivation
You often discover large clusters of *logically identical* objects: glyph metadata for every character on screen, identical style/theme objects, token descriptors in parsed code, or repeated sprite descriptors in a scene. Allocating each one separately wastes memory, pollutes caches, increases GC pressure, and complicates identity checks.

**Flyweight** solves this by centralizing the immutable “essence” of objects (intrinsic) and deferring per‑use variability (extrinsic) to call sites.

| Without Flyweight | With Flyweight |
|-------------------|----------------|
| 120,000 glyph objects for a long document | 85 unique glyph shape objects + 120k cheap (char / position) values |
| Duplicated style instances per element | 1 shared style per logical name (key → style) |
| Repeated parse token metadata | 1 shared token info per token kind |
| Identical brush/pen/texture objects | One shared per configuration |

---
## 2. Mental Model

| Term | Meaning | Example |
|------|---------|---------|
| Intrinsic | Shared, immutable, identity‑stable state | Glyph width table, token kind info, style definition |
| Extrinsic | Context‑specific state supplied *when used* | X/Y position, runtime color tint, AST span, render layer |

**Intrinsic objects must be safely shareable.** Prefer records / immutable classes / readonly structs. If you must wrap a mutable structure, freeze it before caching.

---
## 3. Quick Start
```csharp
using PatternKit.Structural.Flyweight;

// Share glyph metadata keyed by char
var glyphs = Flyweight<char, Glyph>.Create()
    .Preload(' ', Glyph.Space)                 // hot key
    .WithFactory(c => c == ' ' ? Glyph.Space : new Glyph(c, InferWidth(c)))
    .WithCapacity(96)                          // hint (ASCII subset)
    .Build();

var gA1 = glyphs.Get('A');
var gA2 = glyphs.Get('A');
Debug.Assert(ReferenceEquals(gA1, gA2)); // same shared object

if (!glyphs.TryGetExisting('Z', out _))
{
    // 'Z' not yet materialized — Get('Z') would create it
}

// Snapshot for diagnostics (safe copy)
var snapshot = glyphs.Snapshot();
```

---
## 4. Fluent Builder API
| Method | Required | Purpose |
|--------|----------|---------|
| `WithFactory(Factory)` | ✅ | Defines how to build a value the first time a key is requested. Must not return null. |
| `Preload(key, value)` | ⛔ | Eagerly seed hot entries (removes first‑call lock). Last duplicate wins. |
| `Preload((key,value)[])` | ⛔ | Batch preload. Auto‑sizes if capacity not set. |
| `WithCapacity(int)` | ⛔ | Reduces rehashing for known key cardinality. |
| `WithComparer(IEqualityComparer<TKey>)` | ⛔ | Case‑insensitive / culture / custom structural key equality. |
| `Build()` | ✅ | Produces immutable, thread‑safe flyweight instance. |
| `Get(in TKey key)` | Runtime | Retrieves or lazily creates value for key. |
| `TryGetExisting(in TKey key, out TValue value)` | Runtime | Query without creating. Returns false if absent. |
| `Count` | Runtime | Distinct intrinsic items cached. |
| `Snapshot()` | Runtime | Point‑in‑time copy for diagnostics / iteration. |

> Builders are **not** thread‑safe. The built flyweight **is** (single lock on first creation per key; lock‑free read path after).

---
## 5. Example Scenarios
### 5.1 Glyph Layout (Rendering)
```csharp
var layout = new List<(Glyph g, int x)>();
var x = 0;
foreach (var ch in text)
{
    var g = glyphs.Get(ch);   // intrinsic
    layout.Add((g, x));       // extrinsic position
    x += g.Width;
}
```

### 5.2 Case‑Insensitive Styles
```csharp
var styles = Flyweight<string, Style>.Create()
    .WithComparer(StringComparer.OrdinalIgnoreCase)
    .WithFactory(name => new Style(name.ToUpperInvariant()))
    .Build();

var a = styles.Get("header");
var b = styles.Get("HEADER");
Debug.Assert(ReferenceEquals(a, b));
```

### 5.3 AST Token Kinds
```csharp
var tokens = Flyweight<string, TokenInfo>.Create()
    .Preload(("if", TokenInfo.Keyword("if")), ("while", TokenInfo.Keyword("while")))
    .WithFactory(id => TokenInfo.Identifier(id))
    .Build();
```

### 5.4 UI Icon Cache (Theming)
Pair with **Decorator** to layer color transforms at use time instead of storing many tinted variants.

---
## 6. Thread Safety & Memory
**Creation Path** (miss): dictionary read → lock → double‑check → factory → store → return.  
**Hot Path** (hit): single dictionary lookup (no lock).

| Aspect | Behavior |
|--------|----------|
| Concurrency | Exactly one factory invocation per key (double‑checked inside lock). |
| Visibility | After 
store, value is visible to subsequent readers (dictionary guarantees). |
| Memory Growth | Monotonic; no eviction built‑in. |
| GC Pressure | One allocation per distinct key + dictionary growth. |

If you need **eviction / TTL / LRU**, layer a Proxy or create a specialized caching decorator rather than complicating the core flyweight.

---
## 7. Performance Guidelines
| Operation | Complexity | Notes |
|-----------|-----------|-------|
| `Get` hit | ~O(1) | Dictionary lookup; inlining friendly. |
| `Get` miss | ~O(1) + factory | One lock, only first call per key. |
| `TryGetExisting` | ~O(1) | Same cost as hit; no factory risk. |
| `Snapshot` | O(n) | Allocates new dictionary; use sparingly. |

**Tips**:
1. Preload hot keys (space, punctuation) to avoid first‑frame stalls in render loops.
2. Use `WithCapacity` if you can estimate cardinality (e.g., 128 glyphs, 64 styles). 
3. Keep factory pure & fast; heavy I/O belongs elsewhere (wrap factory in a lazy Proxy if needed). 
4. Avoid large mutable arrays inside values; prefer readonly spans or value objects.

---
## 8. Composition With Other Patterns
| Pattern | Combination | Benefit |
|---------|-------------|---------|
| Proxy | Wrap flyweight `Get` with metrics / logging / soft limits | Observability & governance |
| Decorator | Apply runtime transforms (e.g., style overlays) | Avoid intrinsic bloat |
| Strategy | Select among multiple flyweights (locale, theme) | Dynamic source selection |
| Facade | Expose grouped flyweights (glyphs, styles, brushes) | Simplified API surface |
| Chain / Command | Access or warm flyweight as a pipeline step | Deterministic warmup |

---
## 9. Pitfalls & Anti‑Patterns
| Pitfall | Why It Hurts | Remedy |
|---------|--------------|--------|
| Mutable intrinsic values | Shared mutation = race conditions & spooky action | Make immutable; copy‑on‑write if needed |
| High cardinality keys ~= total uses | No reuse → overhead vs direct create | Skip flyweight / subdivide domain |
| Factory with side effects | Retries on failure may duplicate side effects | Isolate side effects; cache result only |
| Embedding extrinsic fields into intrinsic value | Defeats separation; reduces reuse | Pass extrinsic at call time |
| Using snapshot per frame | O(n) copy churn | Cache snapshot; iterate underlying structure carefully |

---
## 10. Testing Strategy (PatternKit Coverage)
PatternKit structural tests assert:
- Single factory invocation per key under concurrency.
- `Preload` items are returned without re‑creation.
- Comparer merges (case‑insensitive). 
- Guard clauses (missing factory, null return) throw `InvalidOperationException`.
- `TryGetExisting` doesn’t create.
- Snapshot contains stable copy.

**Your additions**: add domain‑specific validations (e.g., width sums, token classification) using the same BDD style.

---
## 11. Micro Benchmark (Illustrative)
_(You can adapt this with BenchmarkDotNet)_
```csharp
[MemoryDiagnoser]
public class FlyweightBench
{
    private readonly Flyweight<int, Box> _fly = Flyweight<int, Box>.Create()
        .WithCapacity(1000)
        .WithFactory(i => new Box(i))
        .Build();

    [Benchmark]
    public int Get_Flyweight() => _fly.Get(42).Value;

    [Benchmark]
    public int Get_NewEachTime() => new Box(42).Value;

    public sealed record Box(int Value);
}
```
Expect lower allocation count for flyweight path (1 during warmup vs 1 per call).

---
## 12. FAQ
**Q: Can I remove entries?**  Not in this core implementation. Add a layer (Proxy + custom dictionary) if you need eviction.

**Q: Can factories be async?**  No. Materialize synchronously; if async I/O is required, pre‑fetch externally and feed results as intrinsic values.

**Q: Can I store null?** No. A null factory result throws to surface programming errors early.

**Q: Is `Get` safe inside the factory (recursive)?** Discouraged. It can deadlock or cause partial states.

**Q: How big is too big for intrinsic objects?** Keep them reasonably small (metadata). Large binary blobs (images) may belong in a dedicated resource cache with streaming semantics.

---
## 13. Migration Checklist
| Step | Action |
|------|--------|
| 1 | Identify high repetition objects (profiling / memory dump) |
| 2 | Extract immutable core state to a record/class |
| 3 | Define key (minimal identity; avoid composite if derivable) |
| 4 | Implement pure factory (no side effects) |
| 5 | Preload critical hot keys |
| 6 | Replace allocations with `Get(key)` calls |
| 7 | Add tests: reuse, concurrency, guard conditions |
| 8 | (Optional) add metrics via Proxy wrapper |

---
## 14. Full Example
```csharp
public sealed record Glyph(char Char, int Width);

static int Measure(char c) => c switch
{
    'W' or 'M' => 9,
    'I' or 'l' => 4,
    _ => 6
};

var glyphs = Flyweight<char, Glyph>.Create()
    .Preload(' ', new Glyph(' ', 3))
    .WithFactory(c => new Glyph(c, Measure(c)))
    .Build();

int XAdvance(string text)
{
    var x = 0;
    foreach (var ch in text)
        x += glyphs.Get(ch).Width;
    return x;
}

Console.WriteLine(XAdvance("HELLO WORLD"));
```

---
## 15. Summary
Flyweight gives predictable identity sharing + reduced allocation for repetition‑heavy domains. The PatternKit implementation favors **clarity, immutability, and low overhead** over feature breadth. Compose it with other structural patterns (Proxy, Decorator, Facade) for richer behaviors without inflating the intrinsic core.

> Need eviction, async hydration, or metrics? Wrap the flyweight—you get to keep the simple, robust core.

---
**See also**: [Proxy](../proxy/index.md), [Decorator](../decorator/index.md), [Strategy](../../behavioral/strategy/strategy.md)

