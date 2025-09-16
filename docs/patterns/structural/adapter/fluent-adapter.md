# Adapter — Adapter<TIn,TOut>

A tiny, fluent adapter that maps an input TIn to an output TOut via ordered mapping steps and optional validations. Build once, then Adapt or TryAdapt. No reflection, no magic—just arrays of delegates composed at Build().

- Map steps mutate an output instance in registration order
- Seed the destination from a parameterless factory or from the input
- Validators run after mapping; the first non-null message fails the adaptation
- Immutable and thread-safe after Build()

---

## TL;DR

```csharp
using PatternKit.Structural.Adapter;

public sealed record Source(string First, string Last, int Age);
public sealed class Dest { public string? FullName; public int Age; public bool Adult; }

var adapter = Adapter<Source, Dest>
    .Create(static () => new Dest())                   // or Create(static (in Source s) => new Dest { Age = s.Age })
    .Map(static (in Source s, Dest d) => d.FullName = $"{s.First} {s.Last}")
    .Map(static (in Source s, Dest d) => d.Age = s.Age)
    .Map(static (in Source s, Dest d) => d.Adult = s.Age >= 18)
    .Require(static (in Source _, Dest d) => string.IsNullOrWhiteSpace(d.FullName) ? "name required" : null)
    .Require(static (in Source _, Dest d) => d.Age is < 0 or > 130 ? $"age out of range: {d.Age}" : null)
    .Build();

var dto = adapter.Adapt(new Source("Ada", "Lovelace", 30));  // fills Dest, throws if invalid

if (!adapter.TryAdapt(new Source("", "", 10), out var bad, out var error))
{
    // error == "name required"; bad is null
}
```

---

## API (at a glance)

```csharp
public sealed class Adapter<TIn, TOut>
{
    public delegate TOut Seed();
    public delegate TOut SeedFrom(in TIn input);
    public delegate void MapStep(in TIn input, TOut output);
    public delegate string? Validator(in TIn input, TOut output);

    public static Builder Create(Seed seed);
    public static Builder Create(SeedFrom seedFrom);

    public TOut Adapt(in TIn input);                               // throws on first failing validator
    public bool TryAdapt(in TIn input, out TOut output, out string? error); // returns false + error

    public sealed class Builder
    {
        public Builder Map(MapStep step);       // append mutation
        public Builder Require(Validator rule);  // append validator
        public Adapter<TIn, TOut> Build();
    }
}
```

### Semantics

- Destination seed: either a parameterless factory (Create(Seed)) or dependent on the input (Create(SeedFrom)).
- Mapping order preserved: steps run in the order added.
- Validation order preserved: validators run after all mapping steps; the first non-null/empty message fails the adaptation.
- TryAdapt returns false with the error instead of throwing and sets output to default.
- Built adapters are immutable and safe for concurrent use.

---

## Patterns & usage

- DTO projection without AutoMapper—keep it explicit and allocation-light.
- Safe model normalization where validations depend on the adapted result.
- Adapters that need input-aware seeding (e.g., copy an ID or timestamp to the destination before mapping).

```csharp
// Input-aware seed (copy Age), then mapping
var a = Adapter<Source, Dest>
    .Create(static (in Source s) => new Dest { Age = s.Age })
    .Map(static (in Source s, Dest d) => d.FullName = s.First)
    .Build();

var d = a.Adapt(new Source("X", "Y", 21)); // d.Age=21, d.FullName="X"
```

---

## Tips

- Prefer static lambdas/method groups to avoid captures.
- Keep validators idempotent and fast; they run once per Adapt/TryAdapt.
- Use TryAdapt for probe/validate flows (CLI, API inputs) to avoid exception overhead.

---

## Tests

See PatternKit.Tests/Structural/Adapter/AdapterTests.cs for TinyBDD specs covering:

- Mapping order and success
- First validator failure wins (Adapt throws, TryAdapt returns false)
- SeedFrom semantics
- Adapter reuse producing distinct outputs
