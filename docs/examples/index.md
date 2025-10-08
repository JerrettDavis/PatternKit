# Examples & Demos

Welcome! This section collects small, focused demos that show **how to compose behaviors with PatternKit**—without sprawling frameworks, if/else ladders, or tangled control flow. Each demo is production-shaped, tiny, and easy to lift into your own code.

## What you'll see

* **First-match-wins strategies** for branching without `if` chains.
* **Branchless action chains** for rule packs (logging, pre-auth, discounts, tax).
* **Fluent decorators** for layering functionality (tax, discounts, rounding, logging) without inheritance.
* **Pipelines** built declaratively and tested end-to-end.
* **Config-driven composition** (DI + `IOptions`) so ops can re-order rules without redeploys.
* **Strategy-based coercion** for turning "whatever came in" into the types you actually want.
* **Ultra-minimal HTTP routing** to illustrate middleware vs. routes vs. negotiation.
* **Flyweight identity sharing** to eliminate duplicate immutable objects (glyphs, styles, tokens).

## Demos in this section

* **Composed, Preference-Aware Notification Strategy (Email/SMS/Push/IM)**
  Shows how to layer a user's channel preferences, failover, and throttling into a composable **Strategy** without `switch`es. Good template for "try X, else Y" flows (alerts, KYC, etc.).

* **Auth & Logging Chain**
  A tiny `ActionChain<HttpRequest>` showing **request ID logging**, an **auth short-circuit** for `/admin/*`, and the subtleties of **`.ThenContinue` vs `.ThenStop` vs `Finally`** (strict-stop semantics by default).

* **Strategy-Based Data Coercion**
  `Coercer<T>` compiles a tiny set of **TryStrategy** handlers once per target type to coerce JSON/primitives/strings at runtime—**first-match-wins**, culture-safe, allocation-light.

* **Mediated Transaction Pipeline**
  An in-code pipeline (no config) that demonstrates **ActionChain**, **Strategy**, and **TryStrategy** together: pre-auth checks, discounting, tax, rounding, tender handling, and finalization. Emphasizes clear logs and testable rules.

* **Configuration-Driven Transaction Pipeline**
  Same business shape as above, but wired via DI + `IOptions<PipelineOptions>`. Discounts/rounding/tenders are discovered and **ordered from config**, making the pipeline operationally tunable.

* **Minimal Web Request Router**
  A tiny "API gateway" that separates **first-match middleware** (side effects/logging/auth) from **first-match routes** and **content negotiation**. A crisp example of Strategy patterns in an HTTP-ish setting.

* **Payment Processor — Fluent Decorator Pattern for Point of Sale**
  Demonstrates the **Decorator** pattern for building flexible payment processors. Shows how to layer tax calculation, promotional discounts, loyalty programs, employee benefits, and rounding strategies on a base processor—**no inheritance hierarchies**. Includes five real-world processors (simple, retail, e-commerce, cash register, birthday special) with full test coverage. Perfect for understanding decorator execution order and composition patterns.

* **Flyweight Glyph Cache & Style Sharing**  
  Shows a high-volume text/glyph layout where each distinct glyph (and style) is allocated **once** and reused. Demonstrates intrinsic vs extrinsic state separation, preload of hot keys (spaces), custom key comparers (case-insensitive styles), and thread-safe lazy creation—mirroring classic Flyweight scenarios (rendering, AST token metadata, icon caches).

## How to run

From the repo root:

```bash
# Build everything
dotnet build PatternKit.slnx -c Release

# Run all tests (quick, cross-targeted)
dotnet test PatternKit.slnx -c Release
```

> Tip (Linux/macOS): our tests force `en-US` culture to make currency/text output stable across platforms.
> If you run outside the test host, ensure invariant globalization isn’t enabled:
>
> * `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` should be **unset** or **0**.
> * To see console output, execute demo entrypoints (e.g., `Demo.Run`) from your IDE or a small console host.

## Design highlights

* **First-match wins** everywhere (middleware, routes, rounding rules, tender routers).
* **Zero-`if` routing** via `BranchBuilder` and delegates (`Predicate` → `Handler`).
* **Branchless rule packs** via `ActionChain<T>` with `When/ThenContinue/ThenStop/Finally`.
* **Immutable, thread-safe artifacts** after `.Build()`; builders remain mutable.
* **Tiny domain types** you can replace or extend (requests, responses, tenders, items, rules).
* **Identity sharing** via Flyweight to lower allocation pressure in repetition-heavy domains.

## Where to look (quick map)

* **Auth & Logging Chain:** `AuthLoggingDemo` (+ `AuthLoggingDemoTests`) — strict-stop auth with logging.
* **Coercer:** `Coercer<T>` (+ `CoercerTests`) — strategy-based, culture-safe coercion.
* **Mini Router:** `MiniRouter` + `Demo.Run` — middleware/auth/negotiation in console output.
* **Mediated Pipeline:** `TransactionPipelineBuilder` + `MediatedTransactionPipelineDemo.Run`.
* **Config-Driven Pipeline:** `ConfigDrivenPipelineDemo.AddPaymentPipeline` + `PipelineOptions`.
* **Payment Processor Decorators:** `PaymentProcessor*` + related tests.
* **Flyweight Glyph Cache:** `FlyweightDemo` (+ `FlyweightDemoTests`) — glyph width layout & style sharing.
* **Flyweight Structural Tests:** `Structural/Flyweight/FlyweightTests.cs` — preload, concurrency, comparer, guards.
* **Tests:** `PatternKit.Examples.Tests/*` use TinyBDD scenarios that read like specs.

## Why these demos exist

They’re meant to be **copy-pasteable patterns**:

* Replace cascade `if/else` with **composed strategies**.
* Turn scattered rules into a **linear, testable chain**.
* Move “what runs & in what order” to **configuration**, when appropriate.
* Keep the primitives small so the system stays legible under change.
* Eliminate duplicate immutable instances (Flyweight) where repetition is high.

Jump in via the pages in the left-hand ToC and open the corresponding test files—the assertions double as executable documentation.