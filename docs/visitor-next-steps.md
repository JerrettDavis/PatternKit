# Visitor Pattern — Next Steps Plan (Updated)

## Objectives
- Achieve documentation and discoverability parity across Visitor variants (sync/async, result/action).
- Strengthen test coverage with concurrency and async scenarios beyond correctness.
- Provide DI/composition recipes for production usage and reuse.
- Establish baseline performance via microbenchmarks and identify optimizations.
- Explore developer ergonomics (source generators/analyzers) for large hierarchies.

## Current Status
- Completed
  - Sync `Visitor<TBase, TResult>` and `ActionVisitor<TBase>` APIs + XML docs.
  - Async `AsyncVisitor<TBase, TResult>` and `AsyncActionVisitor<TBase>` APIs + XML docs.
  - Core tests for sync/async visitors (dispatch, defaults, TryVisit, cancellation).
  - Real‑world POS example (`VisitorDemo`) and expanded docs (overview, async, guide, examples, FAQ, troubleshooting, alternatives).
  - TOC and index updated; Visitor listed as implemented.
- Gaps
  - No dedicated page for `AsyncActionVisitor<TBase>` (referenced but not first‑class page).
  - No concurrency stress tests (multi‑threaded/parallel visit).
  - No microbenchmarks (Visitor vs switch/pattern matching vs dictionary mapping).
  - Missing DI/composition recipes in docs with `IServiceCollection` examples.
  - No Visitor source generator/analyzers to optimize or lint usage patterns.

## Milestones & Deliverables

1) Documentation Parity & DI Recipes
- Add dedicated page: `docs/patterns/behavioral/visitor/asyncactionvisitor.md`
- Add “Composition & DI” sections to:
  - `docs/patterns/behavioral/visitor/visitor.md`
  - `docs/patterns/behavioral/visitor/asyncvisitor.md`
  - Show `IServiceCollection` registration and singleton reuse patterns.
- Update TOC: add AsyncActionVisitor page under Visitor.
- DoD: Docfx builds; pages link to tests and examples; discoverable from Visitor hub.

2) Concurrency Tests (Smoke + Deterministic)
- Files (proposed):
  - `test/PatternKit.Tests/Behavioral/VisitorConcurrencyTests.cs`
  - `test/PatternKit.Tests/Behavioral/AsyncVisitorConcurrencyTests.cs`
- Scenarios:
  - Parallel `Visit`/`VisitAsync` across mixed node arrays; counters validate counts; no exceptions.
  - Ensure default path participates correctly under concurrency.
- DoD: Tests pass deterministically on all TFMs.

3) Microbenchmarks (Baseline Performance)
- Project: `benchmarks/PatternKit.Benchmarks` (net8.0, net9.0)
- Scenarios (per N registrations; skewed/mixed/fallback hit patterns):
  - Visitor vs `switch`/pattern matching vs `Dictionary<Type, ...>` mapping.
  - Result and action variants; sync stubs; async stubs (completed ValueTasks).
- DoD: Benchmarks run locally; readme captures summary and guidance.

4) Async Example Enrichment
- Extend POS example with an async receipt enrichment (e.g., brand lookup or external call) using `AsyncVisitor<TBase, string>`.
- Add cancellation and timeout example; include in docs/examples.
- DoD: Example compiles, is referenced from async docs, and covered by a small test.

5) Developer Ergonomics (Optional but Valuable)
- Visitor Source Generator
  - New attribute: `GenerateVisitorAttribute` (name, base type, result/action kind).
  - Emit sealed visitor with optimized type fast‑paths and builder scaffolding.
- Analyzers (Roslyn)
  - Warn on missing `.Default(...)` when callers use `.Visit(...)` (risk of runtime throw).
  - Warn when a base type registration appears before a subtype (shadowing risk).
- DoD: Generators/analyzers ship under `PatternKit.Generators`; tests cover typical cases.

## Execution Notes
- Build/Test: `dotnet build PatternKit.slnx -c Release`, `dotnet test PatternKit.slnx -c Release`
- Docs: `docfx docs/docfx.json`
- Style: keep “first‑match‑wins” terminology; register specific types before base types; prefer idempotent actions.

## Proposed Order of Work
1) Docs parity + DI recipes (fast win, improves onboarding)
2) Concurrency tests (confidence in production usage)
3) Microbenchmarks (data to guide ordering/structure guidance)
4) Async example enrichment (better story for I/O‑bound real‑world cases)
5) Generators/analyzers (ergonomics and safety for larger teams)
