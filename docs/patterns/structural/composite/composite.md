# Composite — Composite<TIn,TOut>

The Composite pattern lets you treat a single thing (a leaf) and a group of things (a composite of leaves/other composites) the same way.
In PatternKit, you build a tiny tree of work where each node returns a value; a composite node folds its children’s values in order.

- Leaf: a single operation TIn → TOut
- Composite node: a Seed and a Combine function that fold child results in order
- Immutable after Build(), allocation-light, AOT-friendly delegates

---

## The idea in plain English (no prior knowledge required)

Think of a folder on your computer:
- A file has a size — it’s a leaf; asking a file for its size returns a number.
- A folder is a composite — it starts at 0, then adds each child’s size; asking a folder for its size returns the total.

That’s the Composite pattern: leaves compute directly; composites start from a seed and fold children in order.

---

## Mental model (how a node executes)

For a leaf:
- Return LeafOp(input)

For a composite:
- acc = Seed(input)
- For each child in order: acc = Combine(input, acc, child.Execute(input))
- Return acc

This is just an ordered fold over child results.

---

## How this maps to GoF Composite

- Component = Composite<TIn,TOut> (uniform interface: Execute(in TIn) → TOut)
- Leaf = Leaf(op) → Build()
- Composite = Node(seed, combine).AddChildren(...).Build()
- Client code uses Execute regardless of node kind

---

## Five‑minute tutorial (step by step)

Goal: compute a cart summary string like "<items: Apple|Milk|Bananas>" using a mix of leaves and composites.

1) Define three leaves (produce strings):

```csharp
var apple  = Composite<int, string>.Leaf(static (in int _) => "Apple");
var milk   = Composite<int, string>.Leaf(static (in int _) => "Milk");
var banana = Composite<int, string>.Leaf(static (in int _) => "Bananas");
```

2) Make a composite that joins child results with a prefix "<items: " and no separator logic (we’ll just append pipes):

```csharp
var items = Composite<int, string>
    .Node(static (in int _) => "<items: ", static (in int _, string acc, string r) => acc + r)
    .AddChildren(apple, milk, banana)
    .Build();
```

3) Wrap it with a right-side adornment using another composite (demonstrates nesting):

```csharp
var wrapper = Composite<int, string>
    .Node(static (in int _) => string.Empty, static (in int _, string acc, string r) => acc + r)
    .AddChildren(
        items,
        Composite<int, string>.Leaf(static (in int _) => ">")
    )
    .Build();
```

4) Execute:

```csharp
var s = wrapper.Execute(0); // "<items: AppleMilkBananas>"
```

5) Want separators? Insert leafs that return "|" between items, or make Combine add separators when acc isn’t empty.

Key takeaways:
- Leaves are just functions input → value.
- Composites are folds: Seed then Combine over children.
- Nest freely; everything exposes the same Execute(in TIn) → TOut.

---

## TL;DR

```csharp
using PatternKit.Structural.Composite;

// Sum of two leaves: f(x) = x, g(x) = 2 => seed=0, combine=+
var calc = Composite<int, int>
    .Node(static (in int _) => 0, static (in int _, int acc, int r) => acc + r)
    .AddChildren(
        Composite<int, int>.Leaf(static (in int x) => x),
        Composite<int, int>.Leaf(static (in int _) => 2))
    .Build();

calc.Execute(5); // 7
```

Nested:

```csharp
var nested = Composite<int, string>
    .Node(static (in int _) => "<", static (in int _, string a, string r) => a + r)
    .AddChildren(
        Composite<int, string>
            .Node(static (in int _) => "L:", static (in int _, string a, string r) => a + r)
            .AddChildren(
                Composite<int, string>.Leaf(static (in int _) => "a"),
                Composite<int, string>.Leaf(static (in int _) => "b")),
        Composite<int, string>.Leaf(static (in int _) => "|c"))
    .Build();

nested.Execute(0); // "<L:ab|c"
```

---

## API (at a glance)

```csharp
public sealed class Composite<TIn, TOut>
{
    public delegate TOut LeafOp(in TIn input);
    public delegate TOut Seed(in TIn input);
    public delegate TOut Combine(in TIn input, TOut acc, TOut childResult);

    public static Builder Leaf(LeafOp op);
    public static Builder Node(Seed seed, Combine combine);

    public TOut Execute(in TIn input);

    public sealed class Builder
    {
        public Builder AddChild(Builder child);         // no-op on leaves
        public Builder AddChildren(params Builder[] cs); // no-op on leaves
        public Composite<TIn, TOut> Build();            // immutable snapshot
    }
}
```

### Semantics

- Leaf vs Composite is decided when you start the builder (Leaf(...) vs Node(...)).
- Child order is preserved; Combine runs in registration order.
- Empty composite returns Seed(input).
- Leaves ignore any attempted AddChild/AddChildren (remain leaves).
- Built trees are immutable and safe for concurrent use.

---

## Practical recipes

1) Sum, min, max aggregations

```csharp
var sum = Composite<int, int>
    .Node(static (in int _) => 0, static (in int _, int a, int r) => a + r)
    .AddChildren(
        Composite<int, int>.Leaf(static (in int x) => x),
        Composite<int, int>.Leaf(static (in int _) => 10))
    .Build();

var min = Composite<int, int>
    .Node(static (in int x) => x, static (in int _, int a, int r) => Math.Min(a, r))
    .AddChildren(Composite<int, int>.Leaf(static (in int _) => 7))
    .Build();
```

2) Transform-and-join strings

```csharp
var join = Composite<string, string>
    .Node(static (in string _) => "[", static (in string _, string a, string r) => a + r)
    .AddChildren(
        Composite<string, string>.Leaf(static (in string s) => s.ToUpperInvariant()),
        Composite<string, string>.Leaf(static (in string s) => ":" + s.Length),
        Composite<string, string>.Leaf(static (in string _) => "]"))
    .Build();

join.Execute("hi"); // "[HI:2]"
```

3) Conditional leaves inside children

Use Strategy or simple predicates to decide which leaf builder to pass into AddChild at composition time.

4) Weighted average (folding tuples)

```csharp
public readonly record struct Stat(double Sum, double Weight);
var avg = Composite<double, Stat>
    .Node(static (in double _) => new Stat(0, 0), static (in double x, Stat a, Stat r) => new(a.Sum + r.Sum, a.Weight + r.Weight))
    .AddChildren(
        Composite<double, Stat>.Leaf(static (in double w) => new Stat(10 * w, w)),
        Composite<double, Stat>.Leaf(static (in double w) => new Stat(20 * w, 2*w)))
    .Build();
var s = avg.Execute(1.0); // Stat(Sum=50, Weight=3) → average = 16.66
```

---

## Threading & performance notes

- Compose once; Execute is a tight loop over pre-built arrays (no LINQ or reflection).
- Delegates use in parameters to avoid struct copies; prefer static lambdas/method groups to avoid captures.
- Trees are immutable after Build() and safe to share across threads.

---

## TinyBDD spec example

```csharp
using PatternKit.Structural.Composite;
using TinyBDD;
using TinyBDD.Xunit;

[Feature("Composite basics")]
public sealed class CompositeSpec : TinyBddXunitBase
{
    [Scenario("sum leaves and nested order")]
    [Fact]
    public Task Spec()
        => Given("a nested composite", () =>
                Composite<int, string>
                    .Node(static (in int _) => "<", static (in int _, string a, string r) => a + r)
                    .AddChildren(
                        Composite<int, string>.Leaf(static (in int _) => "a"),
                        Composite<int, string>.Leaf(static (in int _) => "b"))
                    .Build())
            .When("executing", c => c.Execute(0))
            .Then("accumulation order holds", s => s == "<ab")
            .AssertPassed();
}
```

---

## When to use (and when not)

Use Composite when:
- You need a uniform way to run one thing or many things and combine their results.
- Child order matters and you want explicit folding semantics.
- You want an immutable, allocation-light tree you can reuse safely.

Avoid it when:
- You don’t need a tree — a single function is enough.
- You need dynamic first-match branching — use Strategy/BranchBuilder instead.
- You need side-effecting middleware with stop/continue control — use ActionChain/ResultChain.

---

## Pitfalls & troubleshooting

- Empty composite returns Seed(input) — this is by design; set a meaningful seed.
- Exceptions bubble up from your leaf/combiner — wrap your delegates if you need guard rails.
- Deep trees: recursion depth follows your tree; if you suspect stack depth issues, keep trees shallow or split them.
- Debugging: wrap your leaf ops to log their outputs, or log within Combine to see the fold.

Example debug wrapper:

```csharp
static Composite<int, int>.Builder LogLeaf(string name, Func<int,int> f)
    => Composite<int, int>.Leaf((in int x) => { var r = f(x); Console.WriteLine($"{name}={r}"); return r; });
```

---

## FAQs

- Can composite nodes be dynamic at runtime?
  - Build the shape you need up front; if you must change children, rebuild from a builder or keep a small factory that assembles trees.

- How do I short-circuit combining?
  - Make Combine carry a sentinel in acc so it can skip or stop folding (e.g., store a flag and early-return inside Combine).

- Can I mix different TOuts across children?
  - No; by design TOut is uniform to keep execution allocation-light. Project later if you need heterogeneous results.

---

## Related patterns

- Strategy — choose leaves to add based on input or configuration
- Builder — compose trees via fluent builders lazily, then freeze
- Bridge — add Before/After validations around a leaf operation if needed
