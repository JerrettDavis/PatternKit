# Benchmarks

PatternKit keeps BenchmarkDotNet coverage in `benchmarks/PatternKit.Benchmarks`.

The benchmark suite is structured around fluent-vs-source-generated comparisons. Each pattern benchmark should report construction overhead separately from runtime execution when both costs matter. The shared BenchmarkDotNet configuration enables memory diagnostics and exports GitHub markdown, CSV, and JSON artifacts for CI publishing or local analysis.

Published scenario timing and full coverage-matrix results are kept in [Benchmark Results](benchmark-results.md).

Run all benchmarks:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --artifacts artifacts/benchmarks --join
```

Run a single pattern:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --filter *SchedulerAgentSupervisor* --artifacts artifacts/benchmarks --join
```

Benchmark output should be reviewed as part of pattern hardening work. When a pattern has both fluent and generated APIs, the benchmark must include both routes with categories for the pattern family, pattern name, route, and phase.

The benchmark suite also includes coverage matrix benchmarks for every pattern in the production-readiness catalog and every source generator under `src/PatternKit.Generators`. Those matrix benchmarks are validated by TinyBDD tests so a new pattern or generator cannot be added without appearing in BenchmarkDotNet output.

## Latest Snapshot

The following numbers were captured on Windows 11, Intel Core i9-14900K, .NET SDK 10.0.108, .NET 10.0.8, BenchmarkDotNet 0.15.8, using the `current-tfm` job. Treat them as directional route guidance; run the suite on deployment-class hardware when the difference matters.

| Pattern | Phase | Fluent mean | Fluent allocation | Generated mean | Generated allocation | Decision signal |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Ambassador | Construction | 55.42 ns | 448 B | 48.03 ns | 360 B | Generated reduced construction time and allocation in this microbenchmark. |
| Ambassador | Execution | 87.92 ns | 624 B | 93.72 ns | 624 B | Same allocation; fluent was slightly faster in this path. |
| Cache-Aside | Construction | 19.91 ns | 200 B | 19.85 ns | 200 B | Effectively equivalent for this microbenchmark. |
| Cache-Aside | Execution | 216.50 ns | 1,048 B | 208.60 ns | 1,048 B | Same allocation; generated was slightly faster for the miss-then-hit workflow. |
| Leader Election | Construction | 14.28 ns | 104 B | 15.91 ns | 104 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Leader Election | Execution | 43.62 ns | 360 B | 144.37 ns | 312 B | Generated allocated about 13% less memory, while fluent was faster in this path. |
| Message Routing | Construction | 23.42 ns | 224 B | 23.33 ns | 224 B | Effectively equivalent for this microbenchmark. |
| Message Routing | Execution | 707.34 ns | 4,744 B | 679.97 ns | 4,632 B | Generated reduced execution time and allocation for the route/split/aggregate workflow. |
| Message Translator | Construction | 39.49 ns | 424 B | 39.65 ns | 424 B | Effectively equivalent for this microbenchmark. |
| Message Translator | Execution | 365.30 ns | 2,528 B | 381.79 ns | 2,528 B | Same allocation; fluent was slightly faster in this path. |
| Reliability Pipeline | Construction | 34.90 ns | 392 B | 33.16 ns | 328 B | Generated reduced construction time and allocation in this microbenchmark. |
| Reliability Pipeline | Execution | 2.303 us | 3,992 B | 381.36 ns | 1,872 B | Generated was materially faster and allocated less for duplicate inbox processing plus outbox dispatch. |
| Retry | Construction | 25.36 ns | 208 B | 27.18 ns | 208 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Retry | Execution | 110.53 ns | 600 B | 109.52 ns | 600 B | Same allocation; generated was slightly faster for the transient retry workflow. |
| Scheduler Agent Supervisor | Construction | 47.29 ns | 400 B | 45.40 ns | 400 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Scheduler Agent Supervisor | Execution | 177.46 ns | 1,304 B | 180.14 ns | 1,304 B | Effectively equivalent for this scenario. |
| Service Activator | Construction | 4.825 ns | 32 B | 4.641 ns | 32 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Service Activator | Execution | 25.48 ns | 256 B | 26.49 ns | 256 B | Same allocation; fluent was slightly faster in this path. |

The coverage matrix is separate from the scenario timings. Matrix benchmarks prove every catalog pattern and every generator source file has a reportable BenchmarkDotNet route; pattern-specific scenario benchmarks provide the fluent-vs-generated construction and execution numbers shown above. See [Benchmark Results](benchmark-results.md) for the full pattern and generator matrix.

## Interpreting Results

Use construction benchmarks to decide whether source-generated setup meaningfully reduces startup or registration overhead. Use execution benchmarks for hot-path decisions. Allocation columns are often the stronger signal for throughput services because lower allocation reduces GC pressure even when mean time is close.

For source generators, also consider maintainability and deployment shape: generated routes remove runtime boilerplate and make registration explicit, while fluent routes are often faster to read and adjust during application composition.
