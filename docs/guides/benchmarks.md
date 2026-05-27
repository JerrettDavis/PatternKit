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
| Abstract Factory | Construction | 715.345 ns | 5,992 B | 720.811 ns | 5,992 B | Effectively equivalent for tenant widget factory composition. |
| Abstract Factory | Execution | 750.189 ns | 6,200 B | 735.733 ns | 6,200 B | Same allocation; generated was slightly faster for login widget creation. |
| Adapter | Construction | 34.668 ns | 320 B | 3.607 ns | 24 B | Generated adapter construction was materially faster and allocated less. |
| Adapter | Execution | 59.084 ns | 416 B | 20.479 ns | 80 B | Generated adapter execution was faster and allocated less for shipment adaptation. |
| Activity Tracker | Construction | 13.09 ns | 152 B | 12.98 ns | 152 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Activity Tracker | Execution | 446.88 ns | 1,656 B | 452.36 ns | 1,656 B | Same allocation; fluent was slightly faster for dashboard loading gates. |
| Aggregator | Construction | 14.562 ns | 168 B | 15.235 ns | 168 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Aggregator | Execution | 188.000 ns | 1,088 B | 200.564 ns | 1,088 B | Same allocation; fluent was faster for order line aggregation. |
| Ambassador | Construction | 55.42 ns | 448 B | 48.03 ns | 360 B | Generated reduced construction time and allocation in this microbenchmark. |
| Ambassador | Execution | 87.92 ns | 624 B | 93.72 ns | 624 B | Same allocation; fluent was slightly faster in this path. |
| Anti-Corruption Layer | Construction | 59.62 ns | 440 B | 59.77 ns | 440 B | Effectively equivalent for this microbenchmark. |
| Anti-Corruption Layer | Execution | 109.13 ns | 656 B | 107.42 ns | 656 B | Same allocation; generated was slightly faster for legacy order import. |
| Audit Log | Construction | 19.70 ns | 192 B | 18.62 ns | 192 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Audit Log | Execution | 265.81 ns | 968 B | 273.42 ns | 968 B | Same allocation; fluent was slightly faster for submit-and-approve audit logging. |
| Backends For Frontends | Construction | 58.00 ns | 512 B | 42.48 ns | 296 B | Generated reduced construction time and allocation in this microbenchmark. |
| Backends For Frontends | Execution | 25.40 ns | 216 B | 29.77 ns | 216 B | Same allocation; fluent was faster for the web summary shaping workflow. |
| Builder | Construction | 48.24 ns | 320 B | 160.455 us | 129,236 B | Fluent builder construction is much lighter; generated measures full HostApplicationBuilder composition. |
| Builder | Execution | 65.14 ns | 352 B | 500.676 us | 279,923 B | Fluent option building is much lighter; generated exercises the full host-backed application build. |
| Bridge | Construction | 53.115 ns | 504 B | 3.821 ns | 24 B | Generated bridge construction was materially faster and allocated less. |
| Bridge | Execution | 91.848 ns | 664 B | 30.004 ns | 160 B | Generated bridge forwarding was faster and allocated less for notice rendering. |
| Bulkhead | Construction | 20.56 ns | 216 B | 20.48 ns | 216 B | Effectively equivalent for this microbenchmark. |
| Bulkhead | Execution | 102.70 ns | 592 B | 106.11 ns | 592 B | Same allocation; fluent was slightly faster for the shipping allocation workflow. |
| Cache-Aside | Construction | 19.91 ns | 200 B | 19.85 ns | 200 B | Effectively equivalent for this microbenchmark. |
| Cache-Aside | Execution | 216.50 ns | 1,048 B | 208.60 ns | 1,048 B | Same allocation; generated was slightly faster for the miss-then-hit workflow. |
| Canonical Data Model | Construction | 75.482 ns | 632 B | 59.947 ns | 496 B | Generated reduced construction time and allocation in this microbenchmark. |
| Canonical Data Model | Execution | 116.680 ns | 832 B | 92.082 ns | 696 B | Generated reduced execution time and allocation for order normalization. |
| Channel Adapter | Construction | 38.469 ns | 384 B | 38.681 ns | 384 B | Effectively equivalent for this microbenchmark. |
| Channel Adapter | Execution | 204.907 ns | 888 B | 198.374 ns | 888 B | Same allocation; generated was slightly faster for the ERP order document round-trip workflow. |
| Channel Purger | Construction | 28.72 ns | 288 B | 16.66 ns | 168 B | Generated reduced construction time and allocation for the maintenance purger factory. |
| Channel Purger | Execution | 402.57 ns | 2,456 B | 269.76 ns | 1,680 B | Generated reduced execution time and allocation for the inventory backlog purge workflow. |
| Invalid Message Channel | Construction | 20.15 ns | 176 B | 19.76 ns | 176 B | Fluent and generated builder construction were effectively equivalent. |
| Invalid Message Channel | Execution | 428.21 ns | 2,680 B | 441.83 ns | 2,680 B | Fluent and generated invalid-message routing had equivalent allocation with minor timing variance. |
| Claim Check | Construction | 111.92 ns | 1,664 B | 110.71 ns | 1,664 B | Effectively equivalent for this microbenchmark. |
| Claim Check | Execution | 355.24 ns | 2,976 B | 342.12 ns | 2,976 B | Same allocation; generated was slightly faster for the large-document restore workflow. |
| Competing Consumers | Construction | 35.66 ns | 328 B | 35.40 ns | 328 B | Effectively equivalent for this microbenchmark. |
| Competing Consumers | Execution | 75.34 ns | 416 B | 75.23 ns | 416 B | Effectively equivalent for the fulfillment dispatch workflow. |
| Composite | Construction | 82.460 ns | 848 B | 30.685 ns | 216 B | Generated composite construction was faster and allocated less for the storage tree. |
| Composite | Execution | 90.010 ns | 848 B | 36.760 ns | 216 B | Generated composite traversal was faster and allocated less for size calculation. |
| Circuit Breaker | Construction | 14.33 ns | 128 B | 13.73 ns | 128 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Circuit Breaker | Execution | 85.34 ns | 488 B | 85.19 ns | 488 B | Effectively equivalent for the accepted fulfillment workflow. |
| Content-Based Router | Construction | 42.913 ns | 400 B | 44.118 ns | 400 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Content-Based Router | Execution | 52.789 ns | 520 B | 60.816 ns | 576 B | Fluent was faster and allocated less for wholesale order routing. |
| Control Bus | Construction | 115.64 ns | 880 B | 79.88 ns | 624 B | Generated reduced construction time and allocation in this microbenchmark. |
| Control Bus | Execution | 290.44 ns | 1,688 B | 232.48 ns | 1,432 B | Generated reduced execution time and allocation for operational command dispatch. |
| CQRS | Construction | 199.3 ns | 2.09 KB | 88.001 us | 309.24 KB | Fluent mediator construction is much lighter; generated measurement includes full IServiceCollection dispatcher composition. |
| CQRS | Execution | 479.9 ns | 3.40 KB | 96.453 us | 324.83 KB | Fluent route is much lighter in this microbenchmark; generated route exercises the full DI-integrated dispatcher workflow. |
| Data Mapper | Construction | 40.56 ns | 288 B | 12.87 ns | 112 B | Generated reduced construction time and allocation in this microbenchmark. |
| Data Mapper | Execution | 188.09 ns | 1,104 B | 97.71 ns | 672 B | Generated reduced execution time and allocation for the map-store-load workflow. |
| Dead Letter Channel | Construction | 12.35 ns | 120 B | 12.50 ns | 120 B | Effectively equivalent for this microbenchmark. |
| Dead Letter Channel | Execution | 999.95 ns | 7,056 B | 1.023 us | 7,024 B | Generated allocated slightly less, while fluent was slightly faster for capture-and-replay preparation. |
| Durable Subscriber | Construction | 98.55 ns | 760 B | 81.45 ns | 616 B | Generated reduced construction time and allocation for checkpointed replay subscriber setup. |
| Durable Subscriber | Execution | 711.81 ns | 3,912 B | 605.65 ns | 3,128 B | Generated reduced catch-up projection replay time and allocation in this microbenchmark. |
| Dynamic Router | Construction | 213.4 ns | 1.29 KB | 214.0 ns | 1.29 KB | Fluent and generated route-table construction were effectively equivalent. |
| Dynamic Router | Execution | 406.7 ns | 2.83 KB | 415.2 ns | 2.83 KB | Same allocation; fluent was slightly faster for fulfillment route-table dispatch. |
| Messaging Bridge | Construction | 184.0 ns | 1,328 B | 185.5 ns | 1,328 B | Same allocation; fluent and generated bridge construction were effectively equivalent. |
| Messaging Bridge | Execution | 666.8 ns | 3,912 B | 670.8 ns | 3,912 B | Same allocation; fluent was slightly faster for partner order imports. |
| Decorator | Construction | 34.293 ns | 264 B | 17.669 ns | 168 B | Generated decorator composition was faster and allocated less. |
| Decorator | Execution | 60.765 ns | 384 B | 35.551 ns | 304 B | Generated decorator execution was faster and allocated less for decorated storage reads. |
| Domain Event | Construction | 199.5 ns | 1.34 KB | 157.6 ns | 1.04 KB | Generated reduced construction time and allocation in this microbenchmark. |
| Domain Event | Execution | 367.2 ns | 1.77 KB | 346.4 ns | 1.55 KB | Generated reduced execution time and allocation for the order-placed dispatch workflow. |
| Event-Carried State Transfer | Construction | 7.552 ns | 48 B | 6.751 ns | 48 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Event-Carried State Transfer | Execution | 58.508 ns | 448 B | 59.071 ns | 448 B | Effectively equivalent for the inventory projection workflow. |
| Event Notification | Construction | 30.920 ns | 232 B | 31.926 ns | 232 B | Effectively equivalent for this microbenchmark. |
| Event Notification | Execution | 93.209 ns | 704 B | 106.973 ns | 704 B | Same allocation; fluent was faster for order notification publishing. |
| Event Sourcing | Construction | 12.92 ns | 144 B | 13.00 ns | 144 B | Effectively equivalent for this microbenchmark. |
| Event Sourcing | Execution | 239.97 ns | 1,168 B | 245.13 ns | 1,168 B | Same allocation; fluent was slightly faster for place-and-pay event replay. |
| Event-Driven Consumer | Construction | 41.394 ns | 336 B | 25.216 ns | 192 B | Generated reduced construction time and allocation in this microbenchmark. |
| Event-Driven Consumer | Execution | 135.584 ns | 888 B | 122.305 ns | 688 B | Generated reduced execution time and allocation for the order-accepted event workflow. |
| External Configuration Store | Construction | 42.02 ns | 392 B | 41.06 ns | 328 B | Generated reduced construction time and allocation in this microbenchmark. |
| External Configuration Store | Execution | 34.94 ns | 48 B | 35.78 ns | 48 B | Same allocation; fluent was slightly faster for the cached tenant settings workflow. |
| Factory Method | Construction | 72.397 ns | 512 B | 0.355 ns | 0 B | Generated static factory surface resolution avoids runtime builder allocation. |
| Factory Method | Execution | 1.792 us | 10,568 B | 1.681 us | 10,056 B | Generated was faster and allocated less for service-module registration. |
| Facade | Construction | 59.132 ns | 624 B | 12.567 ns | 112 B | Generated facade construction was faster and allocated less for the shipping facade. |
| Facade | Execution | 100.182 ns | 664 B | 68.503 ns | 208 B | Generated facade execution was faster and allocated less for shipping quote calculation. |
| Feature Toggle | Construction | 84.97 ns | 496 B | 82.73 ns | 496 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Feature Toggle | Execution | 151.85 ns | 1,120 B | 150.87 ns | 1,120 B | Effectively equivalent for checkout feature evaluation. |
| Flyweight | Construction | 20.595 ns | 192 B | 23.842 ns | 272 B | Fluent construction was lighter; generated route includes LRU cache infrastructure. |
| Flyweight | Execution | 58.502 ns | 400 B | 109.987 ns | 704 B | Fluent was faster and allocated less in this LRU-vs-unbounded cache comparison. |
| Gateway Aggregation | Construction | 104.21 ns | 856 B | 64.99 ns | 560 B | Generated reduced construction time and allocation in this microbenchmark. |
| Gateway Aggregation | Execution | 109.55 ns | 632 B | 112.95 ns | 632 B | Same allocation; fluent was slightly faster for the dashboard aggregation workflow. |
| Gateway Routing | Construction | 56.20 ns | 464 B | 50.72 ns | 336 B | Generated reduced construction time and allocation in this microbenchmark. |
| Gateway Routing | Execution | 23.63 ns | 200 B | 20.13 ns | 168 B | Generated reduced execution time and allocation for the inventory routing workflow. |
| Health Endpoint Monitoring | Construction | 35.68 ns | 264 B | 35.25 ns | 264 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Health Endpoint Monitoring | Execution | 38.50 ns | 248 B | 31.67 ns | 248 B | Same allocation; generated was faster for the fulfillment health evaluation workflow. |
| Idempotent Receiver | Construction | 17.022 ns | 184 B | 17.021 ns | 184 B | Effectively equivalent for this microbenchmark. |
| Idempotent Receiver | Execution | 99.419 ns | 608 B | 99.051 ns | 608 B | Effectively equivalent for idempotent command handling. |
| Inbox | Construction | 22.259 ns | 208 B | 21.675 ns | 208 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Inbox | Execution | 113.126 ns | 632 B | 110.770 ns | 608 B | Generated reduced execution time and allocation for inbox command processing. |
| Identity Map | Construction | 10.62 ns | 112 B | 10.68 ns | 112 B | Effectively equivalent for this microbenchmark. |
| Identity Map | Execution | 108.91 ns | 968 B | 94.83 ns | 968 B | Same allocation; generated was faster for scoped identity-map reuse. |
| Leader Election | Construction | 14.28 ns | 104 B | 15.91 ns | 104 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Leader Election | Execution | 43.62 ns | 360 B | 144.37 ns | 312 B | Generated allocated about 13% less memory, while fluent was faster in this path. |
| Materialized View | Construction | 140.9 ns | 1.05 KB | 147.4 ns | 1.05 KB | Same allocation; fluent was slightly faster in this microbenchmark. |
| Materialized View | Execution | 389.5 ns | 2.02 KB | 386.0 ns | 2.02 KB | Effectively equivalent for this scenario. |
| Mailbox | Construction | 17.030 ns | 216 B | 29.867 ns | 360 B | Fluent was faster and allocated less for disposable mailbox construction. |
| Mailbox | Execution | 1.856 us | 2,620 B | 1.956 us | 2,474 B | Generated allocated less, while fluent was faster for serialized mailbox processing. |
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
| Outbox | Construction | 9.544 ns | 88 B | 9.672 ns | 88 B | Effectively equivalent for this microbenchmark. |
| Outbox | Execution | 118.307 ns | 424 B | 122.972 ns | 424 B | Same allocation; fluent was slightly faster for enqueue-and-dispatch processing. |
| Pipes and Filters | Construction | 32.99 ns | 264 B | 32.98 ns | 264 B | Effectively equivalent for this microbenchmark. |
| Pipes and Filters | Execution | 138.66 ns | 800 B | 137.18 ns | 800 B | Same allocation; generated was slightly faster for the fulfillment pipeline workflow. |
| Polling Consumer | Construction | 35.577 ns | 328 B | 4.367 ns | 32 B | Generated materially reduced construction time and allocation in this microbenchmark. |
| Polling Consumer | Execution | 52.658 ns | 384 B | 15.783 ns | 96 B | Generated reduced execution time and allocation for the replenishment polling workflow. |
| Prototype | Construction | 886.538 ns | 6,888 B | 20.985 ns | 152 B | Generated clone source construction is much lighter than populating the fluent registry. |
| Prototype | Execution | 1.193 us | 7,832 B | 21.717 ns | 152 B | Generated cloning was materially faster and allocated less for the character prototype. |
| Proxy | Construction | 9.099 ns | 88 B | 6.037 ns | 48 B | Generated proxy construction was faster and allocated less. |
| Proxy | Execution | 20.148 ns | 120 B | 3.116 ns | 0 B | Generated proxy execution avoided allocation and was materially faster for price calculation. |
| Publish-Subscribe | Construction | 156.89 ns | 1,616 B | 153.49 ns | 1,616 B | Same allocation; generated was slightly faster for topology composition. |
| Publish-Subscribe | Execution | 6.380 us | 10,386 B | 6.275 us | 10,417 B | Generated was slightly faster; fluent allocated slightly less for two-subscriber dispatch. |
| Reliability Pipeline | Construction | 34.90 ns | 392 B | 33.16 ns | 328 B | Generated reduced construction time and allocation in this microbenchmark. |
| Reliability Pipeline | Execution | 2.303 us | 3,992 B | 381.36 ns | 1,872 B | Generated was materially faster and allocated less for duplicate inbox processing plus outbox dispatch. |
| Recipient List | Construction | 30.304 ns | 288 B | 30.380 ns | 288 B | Effectively equivalent for this microbenchmark. |
| Recipient List | Execution | 123.057 ns | 992 B | 120.325 ns | 968 B | Generated was slightly faster and allocated slightly less for shipment fan-out. |
| Request-Reply | Construction | 79.15 ns | 672 B | 78.63 ns | 672 B | Effectively equivalent for request/reply topology composition. |
| Request-Reply | Execution | 11.196 us | 13,492 B | 10.864 us | 13,493 B | Generated was slightly faster; allocations were effectively equivalent for the request/reply exchange. |
| Repository | Construction | 9.793 ns | 112 B | 9.239 ns | 112 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Repository | Execution | 146.37 ns | 888 B | 143.27 ns | 888 B | Same allocation; generated was slightly faster for the seed-and-query workflow. |
| Resequencer | Construction | 16.89 ns | 192 B | 17.27 ns | 192 B | Effectively equivalent for this microbenchmark. |
| Resequencer | Execution | 311.90 ns | 2,456 B | 303.94 ns | 2,456 B | Same allocation; generated was slightly faster for the three-event shipment resequencing workflow. |
| Routing Slip | Construction | 35.508 ns | 256 B | 32.448 ns | 256 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Routing Slip | Execution | 515.138 ns | 3,504 B | 507.633 ns | 3,568 B | Generated was slightly faster; fluent allocated slightly less for the fulfillment itinerary. |
| Saga / Process Manager | Construction | 39.453 ns | 272 B | 42.138 ns | 272 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Saga / Process Manager | Execution | 89.969 ns | 672 B | 105.648 ns | 784 B | Fluent was faster and allocated less for the order saga workflow. |
| Singleton | Construction | 26.618 ns | 184 B | 0.366 ns | 0 B | Generated static singleton surface resolution avoids runtime builder allocation. |
| Singleton | Execution | 74.198 ns | 504 B | 0.205 ns | 0 B | Generated static singleton access avoids the fluent resolver allocation path. |
| Priority Queue | Construction | 14.67 ns | 128 B | 14.33 ns | 128 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Priority Queue | Execution | 95.63 ns | 536 B | 93.42 ns | 536 B | Same allocation; generated was slightly faster for the fulfillment scheduling workflow. |
| Queue-Based Load Leveling | Construction | 17.64 ns | 176 B | 17.54 ns | 176 B | Effectively equivalent for this microbenchmark. |
| Queue-Based Load Leveling | Execution | 94.49 ns | 480 B | 94.42 ns | 480 B | Effectively equivalent for the fulfillment enqueue workflow. |
| Retry | Construction | 25.36 ns | 208 B | 27.18 ns | 208 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Retry | Execution | 110.53 ns | 600 B | 109.52 ns | 600 B | Same allocation; generated was slightly faster for the transient retry workflow. |
| Rate Limiting | Construction | 19.16 ns | 168 B | 18.19 ns | 168 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Rate Limiting | Execution | 247.62 ns | 1,200 B | 245.56 ns | 1,200 B | Same allocation; generated was slightly faster for the tenant rejection workflow. |
| Scatter-Gather | Construction | 59.78 ns | 408 B | 62.41 ns | 408 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Scatter-Gather | Execution | 327.74 ns | 1,704 B | 388.12 ns | 2,064 B | Fluent was faster and allocated less for the supplier quote fan-out workflow. |
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
| Splitter | Construction | 3.664 ns | 24 B | 4.020 ns | 24 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Splitter | Execution | 135.516 ns | 808 B | 134.062 ns | 832 B | Generated was slightly faster; fluent allocated slightly less for order line splitting. |
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
| Chain of Responsibility | Construction | 58.0959 ns | 656 B | 3.0491 ns | 24 B | Generated chain construction was materially faster and allocated less. |
| Chain of Responsibility | Execution | 60.2624 ns | 656 B | 4.3664 ns | 0 B | Generated handler dispatch was faster and allocation-free for the approval route. |
| Command | Construction | 19.6229 ns | 208 B | 3.2500 ns | 24 B | Generated command handler setup was faster and allocated less. |
| Command | Execution | 23.1740 ns | 232 B | 0.0125 ns | 0 B | Generated static command dispatch removed fluent command allocation overhead in this microbenchmark. |
| Interpreter | Construction | 149.7429 ns | 1,352 B | 109.6622 ns | 896 B | Generated interpreter factory reduced construction time and allocation. |
| Interpreter | Execution | 240.8832 ns | 1,464 B | 189.7219 ns | 1,008 B | Generated interpreter rules were faster and allocated less for pricing expressions. |
| Iterator | Construction | 34.2051 ns | 352 B | 4.8293 ns | 48 B | Generated iterator construction was materially lighter than fluent flow composition. |
| Iterator | Execution | 84.8700 ns | 480 B | 14.2925 ns | 48 B | Generated iterator execution was faster and allocated less for revenue traversal. |
| Mediator | Construction | 97.3925 ns | 1,144 B | 37.7610 ns | 416 B | Generated dispatcher mediator construction was faster and allocated less. |
| Mediator | Execution | 154.8279 ns | 1,320 B | 64.4207 ns | 416 B | Generated dispatcher send path was faster and allocated less for command dispatch. |
| Memento | Construction | 12.9626 ns | 128 B | 15.7943 ns | 120 B | Fluent construction was slightly faster; generated history allocated slightly less. |
| Memento | Execution | 116.4308 ns | 376 B | 6.0277 ns | 48 B | Generated memento capture and restore was materially faster and allocated less. |
| Observer | Construction | 4.9422 ns | 40 B | 6.9757 ns | 56 B | Fluent observer construction was slightly lighter for this small instance. |
| Observer | Execution | 30.9549 ns | 176 B | 44.3640 ns | 368 B | Fluent publish was lighter for this single-subscriber microbenchmark. |
| State | Construction | 173.0951 ns | 1,688 B | 3.0356 ns | 24 B | Generated state machine construction was materially faster and allocated less. |
| State | Execution | 179.4049 ns | 1,688 B | 3.3041 ns | 24 B | Generated transition dispatch was materially faster and allocated less. |
| Strategy | Construction | 49.6712 ns | 432 B | 49.4027 ns | 432 B | Fluent and generated strategy builders were effectively equivalent. |
| Strategy | Execution | 51.8884 ns | 432 B | 49.9407 ns | 432 B | Generated strategy execution was slightly faster with the same allocation. |
| Template Method | Construction | 10.3334 ns | 88 B | 3.0359 ns | 24 B | Generated template construction was faster and allocated less. |
| Template Method | Execution | 12.9121 ns | 88 B | 0.1900 ns | 0 B | Generated template execution removed fluent delegate overhead in this microbenchmark. |
| Visitor | Construction | 166.0976 ns | 1,720 B | 9.3177 ns | 112 B | Generated visitor builder construction was materially faster and allocated less. |
| Visitor | Execution | 197.2918 ns | 1,760 B | 59.6870 ns | 432 B | Generated visitor dispatch was faster and allocated less for document nodes. |

The coverage matrix is separate from the scenario timings. Matrix benchmarks prove every catalog pattern and every generator source file has a reportable BenchmarkDotNet route; pattern-specific scenario benchmarks provide the fluent-vs-generated construction and execution numbers shown above. See [Benchmark Results](benchmark-results.md) for the full pattern and generator matrix.

## Interpreting Results

Use construction benchmarks to decide whether source-generated setup meaningfully reduces startup or registration overhead. Use execution benchmarks for hot-path decisions. Allocation columns are often the stronger signal for throughput services because lower allocation reduces GC pressure even when mean time is close.

For source generators, also consider maintainability and deployment shape: generated routes remove runtime boilerplate and make registration explicit, while fluent routes are often faster to read and adjust during application composition.
