# ActionStrategy\<TIn>

A tiny, **first-match-wins** strategy where each branch runs a **side-effecting action** (no return value). Think of it as the “actions-only” sibling of `Strategy<TIn,TOut>` / `TryStrategy<TIn,TOut>`.

* **Input:** `in TIn`
* **Match:** first predicate that returns `true`
* **Action:** runs once (no fallthrough)
* **Fallback:** optional default action
* **APIs:** `Execute(in TIn)` (throws if nothing matches and no default), `TryExecute(in TIn)` (never throws; returns `true/false`)

---

## When to use

Use `ActionStrategy<TIn>` when you need exactly one of several **procedures** to run for a given input—logging, routing to an action that doesn’t produce a value, selecting a handler, etc. If you need to **compute a result**, use `Strategy<TIn,TOut>` / `TryStrategy<TIn,TOut>` instead.

---

## Quick start

```csharp
using PatternKit.Behavioral.Strategy;

var log = new List<string>();

var s = ActionStrategy<int>.Create()
    .When(static (in i) => i > 0).Then(static (in i) => log.Add($"+{i}"))
    .When(static (in i) => i < 0).Then(static (in i) => log.Add($"{i}"))
    .Default(static (in _) => log.Add("zero"))
    .Build();

s.Execute(5);   // logs "+5"
s.Execute(-3);  // logs "-3"
s.Execute(0);   // logs "zero"
```

### No default vs default

```csharp
var noDefault = ActionStrategy<int>.Create()
    .When(static (in i) => i % 2 == 0).Then(static (in i) => Console.WriteLine($"even:{i}"))
    .Build();

noDefault.TryExecute(3); // false, nothing ran
// noDefault.Execute(3); // throws InvalidOperationException (no match, no default)

var withDefault = ActionStrategy<int>.Create()
    .Default(static (in _) => Console.WriteLine("fallback"))
    .Build();

withDefault.TryExecute(3); // true, wrote "fallback"
withDefault.Execute(3);    // also writes "fallback"
```

---

## Builder API (at a glance)

```csharp
var s = ActionStrategy<TIn>.Create()
    .When(Predicate).Then(ActionHandler) // add as many branches as you want
    .Default(ActionHandler)              // optional
    .Build();
```

* `When(Predicate)`: start a branch (predicate signature: `bool(in TIn)`).
* `Then(ActionHandler)`: action to run when the branch matches (`void(in TIn)`).
* `Default(ActionHandler)`: action to run when nothing matched.
* `Build()`: composes an immutable, thread-safe strategy.

**Execution semantics**

* Branches are evaluated in **registration order**.
* The **first** matching `When(...).Then(...)` runs; no later branches are considered.
* If nothing matched:

    * `Execute` runs **default** if present; otherwise throws.
    * `TryExecute` returns **true** if default ran; otherwise **false**.

---

## Ordering guarantees

Registration order is preserved. Only the **first** matching action runs:

```csharp
var log = new List<string>();
var s = ActionStrategy<int>.Create()
    .When(static (in i) => i % 2 == 0).Then(static (in _) => log.Add("first"))
    .When(static (in i) => i >= 0).Then(static (in _) => log.Add("second"))
    .Default(static (in _) => log.Add("default"))
    .Build();

s.Execute(2);
Console.WriteLine(string.Join("|", log)); // "first"
```

---

## Error handling

* `Execute(in TIn)` throws `InvalidOperationException` when **no** predicate matches **and** there is **no** default.
* `TryExecute(in TIn)` never throws due to “no match”; it simply returns `false`.

---

## Performance & thread-safety

* The builder compiles arrays of predicates/actions once at `Build()` time.
* The built strategy is **immutable** and **thread-safe** (you can cache and reuse).
* Delegates use `in` parameters to avoid defensive copies for structs.

---

## Testing tips (TinyBDD snippet)

Your unit tests can read like specs:

```csharp
using TinyBDD;
using TinyBDD.Xunit;
using PatternKit.Behavioral.Strategy;

public class ActionStrategySpec : TinyBddXunitBase
{
    [Fact]
    public async Task first_match_and_default()
    {
        var log = new List<string>();
        var s = ActionStrategy<int>.Create()
            .When(static (in i) => i > 0).Then((in i) => log.Add($"+{i}"))
            .When(static (in i) => i < 0).Then((in i) => log.Add($"{i}"))
            .Default(static (in _) => log.Add("zero"))
            .Build();

        await Given("a composed action strategy", () => s)
            .When("executing 5", _ => { s.Execute(5); return 0; })
            .And("executing -3", _ => { s.Execute(-3); return 0; })
            .And("executing 0", _ => { s.Execute(0); return 0; })
            .Then("logs +5|-3|zero", _ => string.Join("|", log) == "+5|-3|zero")
            .AssertPassed();
    }
}
```

---

## Related patterns

* **Produces a value?** Use \[`Strategy<TIn,TOut>`] or \[`TryStrategy<TIn,TOut>`].
* **Needs side effects with short-circuiting middleware style?** See \[`ActionChain<TCtx>`].

> All of these follow the same **first-match-wins** philosophy so you can compose small units without `if/else` tangles.
