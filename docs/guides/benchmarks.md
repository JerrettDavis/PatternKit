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
| Backends For Frontends | Construction | 58.00 ns | 512 B | 42.48 ns | 296 B | Generated reduced construction time and allocation in this microbenchmark. |
| Backends For Frontends | Execution | 25.40 ns | 216 B | 29.77 ns | 216 B | Same allocation; fluent was faster for the web summary shaping workflow. |
| Bulkhead | Construction | 20.56 ns | 216 B | 20.48 ns | 216 B | Effectively equivalent for this microbenchmark. |
| Bulkhead | Execution | 102.70 ns | 592 B | 106.11 ns | 592 B | Same allocation; fluent was slightly faster for the shipping allocation workflow. |
| Cache-Aside | Construction | 19.91 ns | 200 B | 19.85 ns | 200 B | Effectively equivalent for this microbenchmark. |
| Cache-Aside | Execution | 216.50 ns | 1,048 B | 208.60 ns | 1,048 B | Same allocation; generated was slightly faster for the miss-then-hit workflow. |
| Channel Adapter | Construction | 38.469 ns | 384 B | 38.681 ns | 384 B | Effectively equivalent for this microbenchmark. |
| Channel Adapter | Execution | 204.907 ns | 888 B | 198.374 ns | 888 B | Same allocation; generated was slightly faster for the ERP order document round-trip workflow. |
| Claim Check | Construction | 111.92 ns | 1,664 B | 110.71 ns | 1,664 B | Effectively equivalent for this microbenchmark. |
| Claim Check | Execution | 355.24 ns | 2,976 B | 342.12 ns | 2,976 B | Same allocation; generated was slightly faster for the large-document restore workflow. |
| Competing Consumers | Construction | 35.66 ns | 328 B | 35.40 ns | 328 B | Effectively equivalent for this microbenchmark. |
| Competing Consumers | Execution | 75.34 ns | 416 B | 75.23 ns | 416 B | Effectively equivalent for the fulfillment dispatch workflow. |
| Circuit Breaker | Construction | 14.33 ns | 128 B | 13.73 ns | 128 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Circuit Breaker | Execution | 85.34 ns | 488 B | 85.19 ns | 488 B | Effectively equivalent for the accepted fulfillment workflow. |
| Control Bus | Construction | 115.64 ns | 880 B | 79.88 ns | 624 B | Generated reduced construction time and allocation in this microbenchmark. |
| Control Bus | Execution | 290.44 ns | 1,688 B | 232.48 ns | 1,432 B | Generated reduced execution time and allocation for operational command dispatch. |
| Data Mapper | Construction | 40.56 ns | 288 B | 12.87 ns | 112 B | Generated reduced construction time and allocation in this microbenchmark. |
| Data Mapper | Execution | 188.09 ns | 1,104 B | 97.71 ns | 672 B | Generated reduced execution time and allocation for the map-store-load workflow. |
| Dead Letter Channel | Construction | 12.35 ns | 120 B | 12.50 ns | 120 B | Effectively equivalent for this microbenchmark. |
| Dead Letter Channel | Execution | 999.95 ns | 7,056 B | 1.023 us | 7,024 B | Generated allocated slightly less, while fluent was slightly faster for capture-and-replay preparation. |
| Domain Event | Construction | 199.5 ns | 1.34 KB | 157.6 ns | 1.04 KB | Generated reduced construction time and allocation in this microbenchmark. |
| Domain Event | Execution | 367.2 ns | 1.77 KB | 346.4 ns | 1.55 KB | Generated reduced execution time and allocation for the order-placed dispatch workflow. |
| Event-Carried State Transfer | Construction | 7.552 ns | 48 B | 6.751 ns | 48 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Event-Carried State Transfer | Execution | 58.508 ns | 448 B | 59.071 ns | 448 B | Effectively equivalent for the inventory projection workflow. |
| Event Notification | Construction | 30.920 ns | 232 B | 31.926 ns | 232 B | Effectively equivalent for this microbenchmark. |
| Event Notification | Execution | 93.209 ns | 704 B | 106.973 ns | 704 B | Same allocation; fluent was faster for order notification publishing. |
| Event-Driven Consumer | Construction | 41.394 ns | 336 B | 25.216 ns | 192 B | Generated reduced construction time and allocation in this microbenchmark. |
| Event-Driven Consumer | Execution | 135.584 ns | 888 B | 122.305 ns | 688 B | Generated reduced execution time and allocation for the order-accepted event workflow. |
| External Configuration Store | Construction | 42.02 ns | 392 B | 41.06 ns | 328 B | Generated reduced construction time and allocation in this microbenchmark. |
| External Configuration Store | Execution | 34.94 ns | 48 B | 35.78 ns | 48 B | Same allocation; fluent was slightly faster for the cached tenant settings workflow. |
| Gateway Aggregation | Construction | 104.21 ns | 856 B | 64.99 ns | 560 B | Generated reduced construction time and allocation in this microbenchmark. |
| Gateway Aggregation | Execution | 109.55 ns | 632 B | 112.95 ns | 632 B | Same allocation; fluent was slightly faster for the dashboard aggregation workflow. |
| Gateway Routing | Construction | 56.20 ns | 464 B | 50.72 ns | 336 B | Generated reduced construction time and allocation in this microbenchmark. |
| Gateway Routing | Execution | 23.63 ns | 200 B | 20.13 ns | 168 B | Generated reduced execution time and allocation for the inventory routing workflow. |
| Health Endpoint Monitoring | Construction | 35.68 ns | 264 B | 35.25 ns | 264 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Health Endpoint Monitoring | Execution | 38.50 ns | 248 B | 31.67 ns | 248 B | Same allocation; generated was faster for the fulfillment health evaluation workflow. |
| Leader Election | Construction | 14.28 ns | 104 B | 15.91 ns | 104 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Leader Election | Execution | 43.62 ns | 360 B | 144.37 ns | 312 B | Generated allocated about 13% less memory, while fluent was faster in this path. |
| Materialized View | Construction | 140.9 ns | 1.05 KB | 147.4 ns | 1.05 KB | Same allocation; fluent was slightly faster in this microbenchmark. |
| Materialized View | Execution | 389.5 ns | 2.02 KB | 386.0 ns | 2.02 KB | Effectively equivalent for this scenario. |
| Message Channel | Construction | 10.282 ns | 120 B | 10.148 ns | 120 B | Effectively equivalent for this microbenchmark. |
| Message Channel | Execution | 72.921 ns | 512 B | 71.924 ns | 512 B | Same allocation; generated was slightly faster for the inventory adjustment workflow. |
| Message Envelope | Construction | 248.580 ns | 1,688 B | 228.019 ns | 1,688 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Message Envelope | Execution | 455.486 ns | 2,752 B | 427.664 ns | 2,752 B | Same allocation; generated was slightly faster for message context enrichment. |
| Message Filter | Construction | 25.431 ns | 232 B | 25.626 ns | 232 B | Effectively equivalent for this microbenchmark. |
| Message Filter | Execution | 44.637 ns | 424 B | 45.826 ns | 424 B | Same allocation; fluent was slightly faster for order fraud screening. |
| Message Routing | Construction | 23.42 ns | 224 B | 23.33 ns | 224 B | Effectively equivalent for this microbenchmark. |
| Message Routing | Execution | 707.34 ns | 4,744 B | 679.97 ns | 4,632 B | Generated reduced execution time and allocation for the route/split/aggregate workflow. |
| Message Store | Construction | 18.824 ns | 216 B | 18.721 ns | 216 B | Effectively equivalent for this microbenchmark. |
| Message Store | Execution | 274.799 ns | 1,576 B | 265.470 ns | 1,576 B | Same allocation; generated was slightly faster for record-and-replay lookup. |
| Message Translator | Construction | 39.49 ns | 424 B | 39.65 ns | 424 B | Effectively equivalent for this microbenchmark. |
| Message Translator | Execution | 365.30 ns | 2,528 B | 381.79 ns | 2,528 B | Same allocation; fluent was slightly faster in this path. |
| Messaging Gateway | Construction | 14.094 ns | 160 B | 14.167 ns | 160 B | Effectively equivalent for this microbenchmark. |
| Messaging Gateway | Execution | 66.597 ns | 560 B | 67.558 ns | 560 B | Same allocation; fluent was slightly faster for payment authorization. |
| Pipes and Filters | Construction | 32.99 ns | 264 B | 32.98 ns | 264 B | Effectively equivalent for this microbenchmark. |
| Pipes and Filters | Execution | 138.66 ns | 800 B | 137.18 ns | 800 B | Same allocation; generated was slightly faster for the fulfillment pipeline workflow. |
| Polling Consumer | Construction | 35.577 ns | 328 B | 4.367 ns | 32 B | Generated materially reduced construction time and allocation in this microbenchmark. |
| Polling Consumer | Execution | 52.658 ns | 384 B | 15.783 ns | 96 B | Generated reduced execution time and allocation for the replenishment polling workflow. |
| Reliability Pipeline | Construction | 34.90 ns | 392 B | 33.16 ns | 328 B | Generated reduced construction time and allocation in this microbenchmark. |
| Reliability Pipeline | Execution | 2.303 us | 3,992 B | 381.36 ns | 1,872 B | Generated was materially faster and allocated less for duplicate inbox processing plus outbox dispatch. |
| Repository | Construction | 9.793 ns | 112 B | 9.239 ns | 112 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Repository | Execution | 146.37 ns | 888 B | 143.27 ns | 888 B | Same allocation; generated was slightly faster for the seed-and-query workflow. |
| Resequencer | Construction | 16.89 ns | 192 B | 17.27 ns | 192 B | Effectively equivalent for this microbenchmark. |
| Resequencer | Execution | 311.90 ns | 2,456 B | 303.94 ns | 2,456 B | Same allocation; generated was slightly faster for the three-event shipment resequencing workflow. |
| Priority Queue | Construction | 14.67 ns | 128 B | 14.33 ns | 128 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Priority Queue | Execution | 95.63 ns | 536 B | 93.42 ns | 536 B | Same allocation; generated was slightly faster for the fulfillment scheduling workflow. |
| Queue-Based Load Leveling | Construction | 17.64 ns | 176 B | 17.54 ns | 176 B | Effectively equivalent for this microbenchmark. |
| Queue-Based Load Leveling | Execution | 94.49 ns | 480 B | 94.42 ns | 480 B | Effectively equivalent for the fulfillment enqueue workflow. |
| Retry | Construction | 25.36 ns | 208 B | 27.18 ns | 208 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Retry | Execution | 110.53 ns | 600 B | 109.52 ns | 600 B | Same allocation; generated was slightly faster for the transient retry workflow. |
| Rate Limiting | Construction | 19.16 ns | 168 B | 18.19 ns | 168 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Rate Limiting | Execution | 247.62 ns | 1,200 B | 245.56 ns | 1,200 B | Same allocation; generated was slightly faster for the tenant rejection workflow. |
| Scatter Gather | Construction | 59.78 ns | 408 B | 62.41 ns | 408 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Scatter Gather | Execution | 327.74 ns | 1,704 B | 388.12 ns | 2,064 B | Fluent was faster and allocated less for the supplier quote fan-out workflow. |
| Scheduler Agent Supervisor | Construction | 47.29 ns | 400 B | 45.40 ns | 400 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Scheduler Agent Supervisor | Execution | 177.46 ns | 1,304 B | 180.14 ns | 1,304 B | Effectively equivalent for this scenario. |
| Sidecar | Construction | 59.96 ns | 488 B | 52.09 ns | 400 B | Generated reduced construction time and allocation in this microbenchmark. |
| Sidecar | Execution | 99.61 ns | 640 B | 100.51 ns | 640 B | Same allocation; fluent was slightly faster for the order sidecar submission workflow. |
| Service Activator | Construction | 4.825 ns | 32 B | 4.641 ns | 32 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Service Activator | Execution | 25.48 ns | 256 B | 26.49 ns | 256 B | Same allocation; fluent was slightly faster in this path. |
| Service Layer | Construction | 56.33 ns | 496 B | 41.36 ns | 296 B | Generated reduced construction time and allocation in this microbenchmark. |
| Service Layer | Execution | 151.32 ns | 960 B | 148.10 ns | 872 B | Generated slightly reduced execution time and allocation for the register-customer workflow. |
| Specification | Construction | 196.03 ns | 1,704 B | 136.87 ns | 1,008 B | Generated reduced construction time and allocation in this microbenchmark. |
| Specification | Execution | 111.25 ns | 344 B | 93.30 ns | 344 B | Same allocation; generated was faster for loan-application evaluation. |
| Strangler Fig | Construction | 53.71 ns | 416 B | 42.35 ns | 288 B | Generated reduced construction time and allocation in this microbenchmark. |
| Strangler Fig | Execution | 24.64 ns | 200 B | 20.50 ns | 168 B | Generated reduced execution time and allocation for the enterprise checkout routing workflow. |
| Table Data Gateway | Construction | 9.740 ns | 120 B | 9.698 ns | 120 B | Effectively equivalent for this microbenchmark. |
| Table Data Gateway | Execution | 90.51 ns | 600 B | 96.35 ns | 600 B | Same allocation; fluent was slightly faster for the insert-update-query workflow. |
| Transaction Script | Construction | 20.634 ns | 240 B | 5.839 ns | 40 B | Generated materially reduced construction time and allocation in this microbenchmark. |
| Transaction Script | Execution | 184.93 ns | 1,136 B | 98.28 ns | 600 B | Generated reduced execution time and allocation for the submit-order workflow. |
| Unit Of Work | Construction | 49.50 ns | 304 B | 46.91 ns | 304 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Unit Of Work | Execution | 121.03 ns | 824 B | 96.91 ns | 520 B | Generated reduced execution time and allocation for the checkout commit workflow. |
| Wire Tap | Construction | 47.13 ns | 496 B | 40.99 ns | 336 B | Generated reduced construction time and allocation in this microbenchmark. |
| Wire Tap | Execution | 214.72 ns | 1,232 B | 191.45 ns | 1,064 B | Generated reduced execution time and allocation for the order observability workflow. |

The coverage matrix is separate from the scenario timings. Matrix benchmarks prove every catalog pattern and every generator source file has a reportable BenchmarkDotNet route; pattern-specific scenario benchmarks provide the fluent-vs-generated construction and execution numbers shown above. See [Benchmark Results](benchmark-results.md) for the full pattern and generator matrix.

## Interpreting Results

Use construction benchmarks to decide whether source-generated setup meaningfully reduces startup or registration overhead. Use execution benchmarks for hot-path decisions. Allocation columns are often the stronger signal for throughput services because lower allocation reduces GC pressure even when mean time is close.

For source generators, also consider maintainability and deployment shape: generated routes remove runtime boilerplate and make registration explicit, while fluent routes are often faster to read and adjust during application composition.
