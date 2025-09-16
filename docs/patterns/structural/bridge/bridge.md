# Bridge — Bridge<TIn,TOut,TImpl>

The Bridge pattern decouples an Abstraction (what you want to do) from its Implementation (how it’s done) so both can vary independently.
This fluent Bridge gives you a tiny, explicit way to compose that split:

- Provider: obtain an implementation (static or dependent on input)
- Operation: core work that uses the implementation
- Before/After: ordered hooks around the operation
- Require/RequireResult: pre- and post-conditions (first failure wins)
- Immutable after Build(), allocation-light, AOT-friendly delegates

---

## Mental model (how it runs)

Call flow for Execute(in TIn input):

1) Provider → impl
2) Require (pre) → fail fast? throw/false
3) Before hooks (zero or more)
4) Operation → result
5) After hooks (zero or more) can transform the result
6) RequireResult (post) → fail fast? throw/false
7) Return final result

TryExecute mirrors Execute but never throws for validation failures; it returns false with an error message. Unexpected exceptions from hooks/operation are caught and surfaced in error as well.

---

## How this maps to GoF Bridge

- Abstraction = your composed Bridge (validation + hooks + operation)
- Implementor = the TImpl you obtain from Provider/ProviderFrom
- Refined Abstraction = different Bridge instances for different policies
- Concrete Implementors = different TImpls (e.g., PdfRenderer, HtmlRenderer)

Instead of subclassing, you use delegates to keep it explicit and fast.

---

## TL;DR

```csharp
using PatternKit.Structural.Bridge;

public sealed record Job(string Data, string? Format = null);
public sealed class Renderer { public string Name; public Renderer(string name) => Name = name; }

var bridge = Bridge<Job, string, Renderer>
    .Create(static (in Job j) => new Renderer(j.Format ?? "default"))  // ProviderFrom
    .Require(static (in Job j, Renderer _) => string.IsNullOrWhiteSpace(j.Data) ? "data required" : null)
    .Before(static (in Job _, Renderer r) => Console.WriteLine($"pre:{r.Name}"))
    .Operation(static (in Job j, Renderer r) => $"{r.Name}:{j.Data}")
    .After(static (in Job _, Renderer __, string s) => $"[{s}]")
    .RequireResult(static (in Job _, Renderer __, in string s) => s.Length > 128 ? "too long" : null)
    .Build();

var ok = bridge.Execute(new Job("hello", Format: "pdf"));   // "[pdf:hello]"

if (!bridge.TryExecute(new Job(""), out var _, out var err)) // false, "data required"
{
    // handle error without exceptions
}
```

---

## API (at a glance)

```csharp
public sealed class Bridge<TIn, TOut, TImpl>
{
    public delegate TImpl Provider();
    public delegate TImpl ProviderFrom(in TIn input);
    public delegate TOut Operation(in TIn input, TImpl impl);
    public delegate void Pre(in TIn input, TImpl impl);
    public delegate TOut Post(in TIn input, TImpl impl, TOut result);
    public delegate string? Validate(in TIn input, TImpl impl);
    public delegate string? ValidateResult(in TIn input, TImpl impl, in TOut result);

    public static Builder Create(Provider provider);
    public static Builder Create(ProviderFrom providerFrom);

    public TOut Execute(in TIn input);
    public bool TryExecute(in TIn input, out TOut output, out string? error);

    public sealed class Builder
    {
        public Builder Operation(Operation op);
        public Builder Before(Pre hook);
        public Builder After(Post hook);
        public Builder Require(Validate v);
        public Builder RequireResult(ValidateResult v);
        public Bridge<TIn, TOut, TImpl> Build();
    }
}
```

### Semantics

- Provider vs ProviderFrom: choose one; used to acquire the implementation per Execute call.
- Require validators run before Before/Operation; first non-empty message fails.
- Before hooks run in registration order; can prepare/prime the impl.
- Operation is required; it produces the result using the impl.
- After hooks can transform the result; order preserved.
- RequireResult validators run last; first failing message aborts.
- TryExecute returns false with error and never throws on validation failures.

---

## Why (and when not) to use Bridge

Use it when:
- You need to separate policy from mechanics (e.g., “render this job” vs “which renderer”).
- Implementation can vary by input (tenant, file type, locale) without branching in the hot path.
- You want explicit pre/post conditions and light cross-cutting hooks around the operation.

Avoid it when:
- You only have one implementation and no need for pre/post/validation; a plain function suffices.
- You need dynamic discovery/DI routing by key → consider Factory<TKey, …>.
- You need first-match branching among many operations → consider Strategy or BranchBuilder.

---

## Practical recipes

1) Per-input implementation selection

```csharp
var render = Bridge<Job, byte[], IRenderer>
    .Create(static (in Job j) => j.Format switch
    {
        "pdf"  => RendererPool.Get("pdf"),
        "html" => RendererPool.Get("html"),
        _       => RendererPool.Get("txt")
    })
    .Operation(static (in Job j, IRenderer r) => r.Render(j.Data))
    .Build();
```

2) Inject a singleton provider (shared impl)

```csharp
var shared = new Renderer("pdf"); // assume thread-safe
var bridge = Bridge<Job, string, Renderer>
    .Create(static () => shared) // same instance each time
    .Operation(static (in Job j, Renderer r) => r.Name + ":" + j.Data)
    .Build();
```

3) Pre/post hooks for observability and wrapping

```csharp
var b = Bridge<Job, string, Renderer>
    .Create(static () => new Renderer("impl"))
    .Before(static (in Job j, Renderer r) => Metrics.Incr($"render.start.{r.Name}"))
    .Operation(static (in Job j, Renderer r) => r.Name + ":" + j.Data)
    .After(static (in Job _, Renderer r, string s) => s + " (ok)")
    .RequireResult(static (in Job _, Renderer r, in string s) => s.Length < 256 ? null : "too long")
    .Build();
```

4) Guard rails with pre/post conditions

```csharp
var guarded = Bridge<Job, string, Renderer>
    .Create(static () => new Renderer("impl"))
    .Require(static (in Job j, Renderer _) => j.Data.Length == 0 ? "data required" : null)
    .Operation(static (in Job j, Renderer r) => r.Name + ":" + j.Data)
    .RequireResult(static (in Job _, Renderer __, in string s) => s.Contains(':') ? null : "malformed")
    .Build();
```

---

## Threading & performance notes

- Built bridges are immutable and safe to share across threads.
- Provider may create or return a shared impl; if shared, ensure TImpl itself is thread-safe.
- Delegates use in parameters to avoid struct copies; prefer static lambdas/method groups to avoid captures.
- Execution is array iteration over precompiled hooks/validators; no LINQ or reflection in the hot path.

---

## TinyBDD testing style (spec-like)

```csharp
using PatternKit.Structural.Bridge;
using TinyBDD;
using TinyBDD.Xunit;

public sealed record Job(string Data);
public sealed class Impl { public int Calls; }

[Feature("Bridge basics")]
public sealed class BridgeSpec : TinyBddXunitBase
{
    [Scenario("pre/operation/post order and validation")]
    [Fact]
    public Task Spec()
        => Given("a bridge", () =>
                Bridge<Job, string, Impl>
                    .Create(static () => new Impl())
                    .Require(static (in Job j, Impl _) => string.IsNullOrWhiteSpace(j.Data) ? "required" : null)
                    .Before(static (in Job _, Impl i) => i.Calls++)
                    .Operation(static (in Job j, Impl i) => $"{i.Calls}:{j.Data}")
                    .After(static (in Job _, Impl __, string s) => $"[{s}]")
                    .Build())
            .When("executing", b => b.Execute(new Job("hi")))
            .Then("result wrapped and counted", s => s.StartsWith("[") && s.Contains(":hi]"))
            .AssertPassed();
}
```

---

## FAQs

- How do I reuse a heavy implementation across calls?
  - Use Create(static () => sharedImpl). Ensure the impl is thread-safe or add a lock inside Before/Operation if needed.

- Can a post-hook change the result type?
  - Post returns TOut, so it can transform the value, not the type. If you need a different type, wrap it in a discriminated union or project later.

- Where should logging go?
  - Prefer Before/After for observational logging; use Require/RequireResult to surface validation messages.

- What happens on exception?
  - Execute bubbles exceptions. TryExecute catches and returns false with error = ex.Message.

---

## Troubleshooting

- “Operation(...) must be configured.” on Build()
  - You forgot Operation(). It’s required.

- TryExecute returns false with error but Execute worked before
  - A Require or RequireResult started failing (input/impl changed). Inspect hooks and recent edits.

- Concurrency surprises
  - If Provider returns a shared impl, make sure the impl is thread-safe. Otherwise return a fresh instance per call.

---

## Related patterns

- Strategy — when you need to pick one of many operations by predicate (first-match wins).
- Factory — when you route by key to a constructor rather than by input predicate.
- Adapter — when you need in-place mapping and validation from one shape to another.
