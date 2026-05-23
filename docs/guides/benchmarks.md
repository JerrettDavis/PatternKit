# Benchmarks

PatternKit keeps BenchmarkDotNet coverage in `benchmarks/PatternKit.Benchmarks`.

The benchmark suite is structured around fluent-vs-source-generated comparisons. Each pattern benchmark should report construction overhead separately from runtime execution when both costs matter. The shared BenchmarkDotNet configuration enables memory diagnostics and exports GitHub markdown, CSV, and JSON artifacts for CI publishing or local analysis.

Run all benchmarks:

```powershell
dotnet run -c Release --project benchmarks/PatternKit.Benchmarks -- --artifacts artifacts/benchmarks
```

Run a single pattern:

```powershell
dotnet run -c Release --project benchmarks/PatternKit.Benchmarks -- --filter *SchedulerAgentSupervisor* --artifacts artifacts/benchmarks
```

Benchmark output should be reviewed as part of pattern hardening work. When a pattern has both fluent and generated APIs, the benchmark must include both routes with categories for the pattern family, pattern name, route, and phase.

The benchmark suite also includes coverage matrix benchmarks for every pattern in the production-readiness catalog and every source generator under `src/PatternKit.Generators`. Those matrix benchmarks are validated by TinyBDD tests so a new pattern or generator cannot be added without appearing in BenchmarkDotNet output.
