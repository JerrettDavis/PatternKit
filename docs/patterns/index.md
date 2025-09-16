# Patterns

Welcome! This section is the **reference home** for PatternKit’s core building blocks. Each page explains the *why*, the *shape* (APIs), and gives a tiny snippet so you can drop the pattern straight into your codebase.

If you’re looking for end-to-end, production-shaped demos, check the **Examples & Demos** section—those pages show these patterns working together (auth/logging chains, payment pipelines, router, coercer, etc.).

---

## How these fit together

* **Behavioral** patterns describe *what runs & when* (chains and strategies).
* **Creational** helpers build immutable, fast artifacts (routers, pipelines) from tiny delegates.
* Common themes:

    * **First-match wins** (predictable branching without `if` ladders).
    * **Branchless rule packs** (`ActionChain<T>` with `When/ThenContinue/ThenStop/Finally`).
    * **Immutable after `Build()`** (thread-safe, allocation-light hot paths).

---

## Behavioral

### Chain

* **[Behavioral.Chain.ActionChain](behavioral/chain/actionchain.md)**
  Compose linear rule packs with explicit continue/stop semantics and an always-runs `Finally`.

* **[Behavioral.Chain.ResultChain](behavioral/chain/resultchain.md)**
  Like `ActionChain`, but each step returns a result; first failure short-circuits.

### Strategy

* **[Behavioral.Strategy.Strategy](behavioral/strategy/strategy.md)**
  Simple strategy selection—pick exactly one handler.

* **[Behavioral.Strategy.TryStrategy](behavioral/strategy/trystrategy.md)**
  First-success wins: chain of `Try(in, out)` handlers; great for parsing/coercion.

* **[Behavioral.Strategy.ActionStrategy](behavioral/strategy/actionstrategy.md)**
  Fire one or more actions (no result value) based on predicates.

* **[Behavioral.Strategy.AsyncStrategy](behavioral/strategy/asyncstrategy.md)**
  Async sibling for strategies that await external work.

---

## Creational (Builder)

* **[Creational.Builder.BranchBuilder](creational/builder/branchbuilder.md)**
  Zero-`if` router: register `(predicate → step)` pairs; emits a tight first-match loop.

* **[Creational.Builder.ChainBuilder](creational/builder/chainbuilder.md)**
  Small helper to accumulate steps, then project into your own pipeline type.

* **[Creational.Builder.Composer](creational/builder/composer.md)**
  Compose multiple builders/artifacts into a single product.

* **[Creational.Builder.MutableBuilder](creational/builder/mutablebuilder.md)**
  A lightweight base for fluent, mutable configuration objects.

* **[Creational.Factory](creational/factory/factory.md)**
  Key → creator mapping with optional default; immutable and allocation-light.

* **[Creational.Prototype](creational/prototype/prototype.md)**
  Clone-and-tweak via fluent builders and keyed registries.

* **[Creational.Singleton](creational/singleton/singleton.md)**
  Fluent, thread-safe singleton with lazy/eager modes and init hooks.


## Structural

* **[Structural.Adapter.Adapter](structural/adapter/fluent-adapter.md)**
  Fluent in-place mapping with ordered validation for DTO projection.

* **[Structural.Bridge.Bridge](structural/bridge/bridge.md)**
  Abstraction/implementation split with fluent pre/post hooks and validations.

---

## Where to see them in action

* **Auth & Logging Chain** — request-ID logging + strict auth short-circuit using `ActionChain`.
* **Strategy-Based Coercion** — `TryStrategy` turns mixed inputs into typed values.
* **Mediated / Config-Driven Transaction Pipelines** — chains + strategies for totals, rounding, tender routing.
* **Minimal Web Request Router** — `BranchBuilder` for middleware and routes.

> Tip: every pattern page has a tiny example; the demos show realistic combinations with TinyBDD tests you can read like specs.
