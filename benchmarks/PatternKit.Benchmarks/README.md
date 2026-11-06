# PatternKit Benchmarks

This project contains reportable BenchmarkDotNet comparisons for PatternKit fluent builders and source-generated factories.

Run the full benchmark suite from the repository root:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --artifacts artifacts/benchmarks --join
```

Run one pattern family:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --filter *LeaderElection* --artifacts artifacts/benchmarks --join
```

Every pattern-specific scenario benchmark class must:

- Compare fluent and source-generated routes in the same benchmark class.
- Split construction/setup overhead from execution overhead where the pattern has meaningful runtime work.
- Keep realistic domain names and payloads so benchmark results map back to production examples.
- Emit memory diagnostics and markdown, CSV, and JSON reports through the shared benchmark config.
- Use BenchmarkDotNet categories for the pattern family, pattern name, route, and benchmark phase.

Coverage matrix benchmarks under `Coverage/` include every pattern from `PatternKitPatternCatalog` and every `*Generator.cs`
source file from `src/PatternKit.Generators`. The TinyBDD production-readiness tests fail when a catalog pattern or generator
is missing from that matrix. Pattern-specific benchmarks can then add deeper scenario timing while the matrix keeps top-to-bottom
coverage complete.

Published benchmark snapshots live in `docs/guides/benchmarks.md` so users can compare fluent and source-generated timing,
overhead, and allocation without running the suite first.
