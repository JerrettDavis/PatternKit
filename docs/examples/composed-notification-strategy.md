# Composed, Preference-Aware Notification Strategy (Email/SMS/Push/IM)

> **TL;DR**
> We compose a notification strategy that:
>
> 1. prepends **SMS** for critical messages,
> 2. otherwise uses the user’s **preferred channel order** (de-duped, first occurrence wins),
> 3. evaluates **per-channel gates** (identity/presence/rate),
> 4. executes the **first viable** channel, and
> 5. **falls back to Email** (by design) if nothing else is viable.

This example demonstrates how to build a production-friendly dispatcher with `<xref:PatternKit.Behavioral.Strategy.AsyncStrategy%602>` and a small set of explicit, testable components. It also shows how we validate behavior using **TinyBDD** scenarios with xUnit.

---

## Why a composed strategy (and not a giant `switch`)?

Branching logic for multi-channel notifications tends to sprawl:

* “If critical, always try SMS first…”
* “…but only if opted in + not rate-limited.”
* “Otherwise, follow user preferences…”
* “…making sure IM is online and Push isn’t DND.”
* “…and if none apply, still send *something*.”

A composed strategy lets us encode these as **named predicates and handlers**, then **assemble** them declaratively. The result is readable, testable, and easy to extend.

---

## Model types

* **Channels**: `<xref:PatternKit.Examples.Strategies.Composed.Channel>` — `Email`, `Sms`, `Push`, `Im`.
* **Input**: `<xref:PatternKit.Examples.Strategies.Composed.SendContext>` — `(UserId, Message, IsCritical, Locale?)`.
* **Output**: `<xref:PatternKit.Examples.Strategies.Composed.SendResult>` — `(Channel, Success, Info?)`.

Dependencies (`IIdentityService`, `IPresenceService`, `IRateLimiter`, `IPreferenceService`, and four sender interfaces) are small and focused. They can be synchronous or asynchronous (we use `ValueTask<bool>`/`ValueTask<SendResult>` everywhere).

---

## The core building block: `AsyncStrategy<TIn,TOut>`

We use `<xref:PatternKit.Behavioral.Strategy.AsyncStrategy%602>` to build branchy flows that look like:

```csharp
var strategy = AsyncStrategy<SendContext, SendResult>.Create()
    .When(IsCritical).Then(ExecuteCritical)
    .Default(ExecuteByPrefs)
    .Build();
```

* **When/Then** pairs add branches in order.
* **Default** is the fallback handler if no predicate matches.
* Each predicate/handler has async and sync adapters, so you can pass method groups without lambda allocations.

---

## Channel policies = Gate + Send

We keep channels self-contained with `<xref:PatternKit.Examples.Strategies.Composed.ChannelPolicy>`:

* `Gate : AsyncStrategy<SendContext,bool>` — **all** checks must pass.
* `Send : Handler` — the sender to invoke if the gate allows it.

`<xref:PatternKit.Examples.Strategies.Composed.ChannelPolicyFactory>` wires this up:

* **Push** gate: `HasPushToken && !DoNotDisturb && RateOkPush`
* **IM** gate: `OnlineIm && RateOkIm`
* **Email** gate: `HasVerifiedEmail && RateOkEmail`
* **SMS** gate: `HasSmsOptIn && RateOkSms`

> Gates short-circuit on first failure (sequential checks). They’re built once and reused.

**Why sequential?** It preserves intent and makes short-circuit behavior observable in tests (e.g., “no token → don’t even check DND or rate”).

---

## Preference-aware composition

`<xref:PatternKit.Examples.Strategies.Composed.ComposedStrategies.BuildPreferenceAware*>` builds the top-level strategy:

1. **Critical path**
   If `<xref:PatternKit.Examples.Strategies.Composed.SendContext.IsCritical>` is true, we **prepend `Sms`** to the order (if it isn’t already first) and evaluate gates in that order.

2. **Preferred path**
   Otherwise we ask `<xref:PatternKit.Examples.Strategies.Composed.IPreferenceService>` for the user’s order, **de-dupe while preserving first occurrence**, then build a per-request strategy that tries each policy’s gate in that order.

3. **Fallback**
   If nothing matches, we call **Email’s send handler** as a final attempt **even if Email’s gate would fail**. That is by design to guarantee a last-ditch delivery.

A trimmed version of the ordered execution:

```csharp
var distinct = order.Distinct().ToList(); // preserve first occurrence

var builder = AsyncStrategy<SendContext, SendResult>.Create();

builder = distinct
  .Select(ch => policies[ch])
  .Aggregate(builder, (b, p) => b.When(p.Gate.ExecuteAsync).Then(p.Send));

var strat = builder.Default(policies[Channel.Email].Send).Build();
return await strat.ExecuteAsync(ctx, ct);
```

---

## Gates cheat-sheet

| Channel   | Checks (all must pass)                        |
| --------- | --------------------------------------------- |
| **Push**  | `HasPushToken`, `!DoNotDisturb`, `RateOkPush` |
| **IM**    | `OnlineIm`, `RateOkIm`                        |
| **Email** | `HasVerifiedEmail`, `RateOkEmail`             |
| **SMS**   | `HasSmsOptIn`, `RateOkSms`                    |

> We invert DND using a tiny zero-alloc combinator:
>
> ```csharp
> internal static ValueTask<bool> Continue(this ValueTask<bool> t, Func<bool,bool> f) =>
>     t.IsCompletedSuccessfully ? new(f(t.Result)) : Awaited(t, f);
> ```

---

## Extending with a new channel

1. Add to `enum Channel`.
2. Implement sender interface.
3. Add identity/presence/rate checks (if any).
4. Add a gate via `ChannelPolicyFactory.Gate([...])`.
5. Add `[newChannel] = new(gate, SendNewChannel)` to `CreateAll()`.
6. Update tests (see below).

---

## Testing with TinyBDD

We use **TinyBDD + xUnit** to express readable, executable scenarios. Each test builds a small **harness** of fakes/spies and asserts outcomes.

### Main scenarios

1. **Preference order: first viable → Push**

```csharp
await Given("Push is first & all push guards pass", () =>
        CreateHarness(h => {
            h.Prefs.Set([Channel.Push, Channel.Im, Channel.Email]);
            h.Id.PushToken = true;
            h.Presence.DoNotDisturb = false;
            h.Rate.Set(Channel.Push, true);
        }))
    .When("executing the strategy", Run)
    .Then("result channel should be Push", x => x.R.Channel == Channel.Push)
    .And("push called exactly once", x => x.H.Push.Calls == 1)
    .And("no other senders called", x => x.H.Im.Calls == 0 && x.H.Email.Calls == 0 && x.H.Sms.Calls == 0)
    .AssertPassed();
```

2. **Skip non-viable Push → IM**, **Critical → SMS first**, **Empty prefs → Email**,
   **De-dupe preserves first occurrence** and **evaluates each gate once**,
   **Email gate respected when first in order → next viable (SMS)**,
   **IM requires Online + Rate**, **Rate limiter is per-channel**,
   **Default Email fallback ignores Email gate**.

### Extended scenarios (behavioral edges)

1. **Push short-circuits when no token**
   We prove that **DND and rate aren’t touched** when the first check fails:

```csharp
Then("push token checked once", x => x.t.id.HasPushTokenCalls == 1)
And("DND not checked", x => x.t.presence.DndCalls == 0)
And("push rate not checked", x => x.t.rate.PushCalls == 0)
```

2. **IM sender failure does not fall through**
   If IM is chosen and the sender returns `Success=false`, we **do not** try Email afterward. The result is IM/false.

3. **Fallback Email throws → cancellation propagates**
   If preferences force fallback to Email and the Email sender throws (e.g., a cancelled token), we surface the exception (`TaskCanceledException` in the sample).

4. **Tie-breakers follow declared order**
   When **all gates pass**, selection follows the declared preference order (e.g., `[Sms, Push, Email, Im]` picks `Sms`).

---

## Test harness fakes & spies

We use minimal test doubles:

* **Fakes** (`Fake*`) to collect call counts and capture `SendContext`.
* **Spies** (`Spy*`) to verify **short-circuiting** (e.g., how many times `HasPushTokenAsync` was called).
* **Throwing/Failing senders** to exercise cancellation and “no fall-through” behavior.

This keeps assertions crisp and maps 1:1 to the production code’s intent.

---

## Performance and reliability notes

* **`ValueTask` everywhere**: avoid allocations on sync-fast paths.
* **Method groups over lambdas**: fewer allocations, clearer intent.
* **No per-call closures** inside the strategy builder: policies are created once and reused.
* **Short-circuit gates**: only the minimum number of checks is executed.
* **Thread-safe composition**: the built strategy is immutable; ensure your dependencies are thread-safe.

---

## Troubleshooting

* **“Argument 2: cannot convert from ‘method group’…”**
  Ensure the method group **matches the delegate** exactly. For example, `When(policy.Gate.ExecuteAsync)` expects `Func<SendContext, CancellationToken, ValueTask<bool>>`. If your method has default parameters or a different signature, **wrap** it:

  ```csharp
  b.When((ctx, ct) => policy.Gate.ExecuteAsync(ctx, ct));
  ```

* **Email fallback ignores Email gate**
  That’s **intentional**: we guarantee a last-ditch attempt to send something.

---

## Quick start

```csharp
// Wire your real services here
var strategy = ComposedStrategies.BuildPreferenceAware(
    id, presence, rate, prefs, email, sms, push, im);

var result = await strategy.ExecuteAsync(
    new SendContext(userId, "Hello from PatternKit!", isCritical: false),
    CancellationToken.None);

// result.Channel: which channel was executed
// result.Success: whether the sender reported success
```

---

## Cross-references

* `<xref:PatternKit.Behavioral.Strategy.AsyncStrategy%602>`
* `<xref:PatternKit.Examples.Strategies.Composed.Channel>`
* `<xref:PatternKit.Examples.Strategies.Composed.SendContext>`
* `<xref:PatternKit.Examples.Strategies.Composed.SendResult>`
* `<xref:PatternKit.Examples.Strategies.Composed.ChannelPolicy>`
* `<xref:PatternKit.Examples.Strategies.Composed.ChannelPolicyFactory>`
* `<xref:PatternKit.Examples.Strategies.Composed.ComposedStrategies>`

