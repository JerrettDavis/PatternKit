# Benchmark Results

PatternKit publishes two kinds of benchmark results:

- Scenario timings compare fluent and source-generated routes for patterns with dedicated BenchmarkDotNet scenario classes.
- Coverage matrix results prove every catalog pattern and generator source has a reportable BenchmarkDotNet route with source, test, and documentation validation.

The latest measured timings below were captured on Windows 11, Intel Core i9-14900K, .NET SDK 10.0.108, .NET 10.0.8, BenchmarkDotNet 0.15.8, using the `current-tfm` job. Treat them as directional; run the suite on deployment-class hardware before making final hot-path decisions.

## Scenario Timing Results

| Pattern | Phase | Fluent mean | Fluent allocation | Generated mean | Generated allocation | Decision signal |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Abstract Factory | Construction | 715.345 ns | 5,992 B | 720.811 ns | 5,992 B | Effectively equivalent for tenant widget factory composition. |
| Abstract Factory | Execution | 750.189 ns | 6,200 B | 735.733 ns | 6,200 B | Same allocation; generated was slightly faster for login widget creation. |
| Adapter | Construction | 34.668 ns | 320 B | 3.607 ns | 24 B | Generated adapter construction was materially faster and allocated less. |
| Adapter | Execution | 59.084 ns | 416 B | 20.479 ns | 80 B | Generated adapter execution was faster and allocated less for shipment adaptation. |
| Activity Tracker | Construction | 13.09 ns | 152 B | 12.98 ns | 152 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Activity Tracker | Execution | 446.88 ns | 1,656 B | 452.36 ns | 1,656 B | Same allocation; fluent was slightly faster for dashboard loading gates. |
| Manual Task Gate | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Manual Task Gate | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Workflow Orchestration | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Workflow Orchestration | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Snapshot / Checkpoint Management | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Snapshot / Checkpoint Management | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Timeout Manager | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Timeout Manager | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Aggregate Root | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Aggregate Root | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
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
| Cache Stampede Protection | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Cache Stampede Protection | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Cache-Aside | Construction | 19.91 ns | 200 B | 19.85 ns | 200 B | Effectively equivalent for this microbenchmark. |
| Cache-Aside | Execution | 216.50 ns | 1,048 B | 208.60 ns | 1,048 B | Same allocation; generated was slightly faster for the miss-then-hit workflow. |
| Read-Through / Write-Through Cache | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Read-Through / Write-Through Cache | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Read-Through Cache | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix through the read/write-through cache route. |
| Read-Through Cache | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix through the read/write-through cache route. |
| Write-Through Cache | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix through the read/write-through cache route. |
| Write-Through Cache | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix through the read/write-through cache route. |
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
| Content Enricher | Construction | 36.39 ns | 312 B | 36.22 ns | 312 B | Effectively equivalent for this microbenchmark. |
| Content Enricher | Execution | 1.357 us | 1,400 B | 1.344 us | 1,400 B | Same allocation; generated was slightly faster for customer profile enrichment. |
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
| Message Bus | Construction | 186.7 ns | 1,264 B | 160.0 ns | 904 B | Generated reduced construction time and allocation for topic bus topology setup. |
| Message Bus | Execution | 503.8 ns | 3,192 B | 587.7 ns | 3,232 B | Fluent was faster and allocated slightly less for publishing order events to topic subscribers. |
| Messaging Bridge | Construction | 184.0 ns | 1,328 B | 185.5 ns | 1,328 B | Same allocation; fluent and generated bridge construction were effectively equivalent. |
| Messaging Bridge | Execution | 666.8 ns | 3,912 B | 670.8 ns | 3,912 B | Same allocation; fluent was slightly faster for partner order imports. |
| Correlation Identifier | Construction | 13.188 ns | 96 B | 6.133 ns | 48 B | Generated reduced construction time and allocation for correlation builder setup. |
| Correlation Identifier | Execution | 235.073 ns | 1,480 B | 224.406 ns | 1,432 B | Generated was slightly faster and allocated slightly less for order request/reply correlation. |
| Message History | Construction | 6.843 ns | 56 B | 6.075 ns | 56 B | Same allocation; generated was slightly faster for history recorder construction. |
| Message History | Execution | 275.298 ns | 1,400 B | 285.765 ns | 1,400 B | Same allocation; fluent was slightly faster for order history recording. |
| Decorator | Construction | 34.293 ns | 264 B | 17.669 ns | 168 B | Generated decorator composition was faster and allocated less. |
| Decorator | Execution | 60.765 ns | 384 B | 35.551 ns | 304 B | Generated decorator execution was faster and allocated less for decorated storage reads. |
| Domain Event | Construction | 199.5 ns | 1.34 KB | 157.6 ns | 1.04 KB | Generated reduced construction time and allocation in this microbenchmark. |
| Domain Event | Execution | 367.2 ns | 1.77 KB | 346.4 ns | 1.55 KB | Generated reduced execution time and allocation for the order-placed dispatch workflow. |
| Bounded Context | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Bounded Context | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Context Map | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Context Map | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Domain Service | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Domain Service | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
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
| Hosting Extensions | Construction | reportable | reportable | reportable | reportable | Dedicated route measures reusable `IServiceCollection` registration and provider build overhead for package-level hosting APIs. |
| Hosting Extensions | Execution | reportable | reportable | reportable | reportable | Dedicated route measures resolving and executing reusable PatternKit primitives through Microsoft.Extensions.DependencyInjection. |
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
| Message Expiration | Construction | 22.55 ns | 248 B | 14.63 ns | 144 B | Generated policy construction reduced time and allocation. |
| Message Expiration | Execution | 88.54 ns | 728 B | 103.86 ns | 536 B | Generated reduced allocation; fluent was faster for the stamp-and-evaluate flow. |
| Guaranteed Delivery | Construction | 22.778 ns | 200 B | 22.522 ns | 200 B | Fluent and generated queue construction were effectively equivalent. |
| Guaranteed Delivery | Execution | 175.488 ns | 752 B | 167.434 ns | 752 B | Generated was slightly faster with the same allocation for enqueue, receive, and acknowledge. |
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
| Value Object | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Value Object | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
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

## Coverage Matrix Summary

The coverage matrix currently publishes 113 catalog patterns and 452 pattern route results. Each pattern has four BenchmarkDotNet routes: fluent construction, fluent execution, source-generated construction, and source-generated execution. The reusable hosting integration matrix publishes 9 reusable hosting integration route results for package-level `IServiceCollection` registrations.

| Category | Patterns | Published route results |
| --- | ---: | ---: |
| Application Architecture | 25 | 100 |
| Behavioral | 11 | 44 |
| Cloud Architecture | 20 | 80 |
| Creational | 5 | 20 |
| Enterprise Integration | 41 | 164 |
| Messaging Reliability | 3 | 12 |
| Structural | 7 | 28 |

The generator matrix currently publishes 107 generator source route results.

## Hosting Integration Matrix Results

| Pattern | Route | Registration | Source | Tests | Docs |
| --- | --- | --- | --- | --- | --- |
| Bulkhead | `IServiceCollection` | `AddPatternKitBulkheadPolicy<TResult>` | `src/PatternKit.Hosting.Extensions/DependencyInjection/PatternKitServiceCollectionExtensions.cs` | `test/PatternKit.Hosting.Extensions.Tests/DependencyInjection/PatternKitServiceCollectionExtensionsTests.cs` | `docs/guides/hosting-extensions.md` |
| Circuit Breaker | `IServiceCollection` | `AddPatternKitCircuitBreakerPolicy<TResult>` | `src/PatternKit.Hosting.Extensions/DependencyInjection/PatternKitServiceCollectionExtensions.cs` | `test/PatternKit.Hosting.Extensions.Tests/DependencyInjection/PatternKitServiceCollectionExtensionsTests.cs` | `docs/guides/hosting-extensions.md` |
| Guaranteed Delivery | `IServiceCollection` | `AddPatternKitGuaranteedDelivery<TPayload>` | `src/PatternKit.Hosting.Extensions/DependencyInjection/PatternKitServiceCollectionExtensions.cs` | `test/PatternKit.Hosting.Extensions.Tests/DependencyInjection/PatternKitServiceCollectionExtensionsTests.cs` | `docs/guides/hosting-extensions.md` |
| Message Channel | `IServiceCollection` | `AddPatternKitMessageChannel<TPayload>` | `src/PatternKit.Hosting.Extensions/DependencyInjection/PatternKitServiceCollectionExtensions.cs` | `test/PatternKit.Hosting.Extensions.Tests/DependencyInjection/PatternKitServiceCollectionExtensionsTests.cs` | `docs/guides/hosting-extensions.md` |
| Message Store | `IServiceCollection` | `AddPatternKitMessageStore<TPayload>` | `src/PatternKit.Hosting.Extensions/DependencyInjection/PatternKitServiceCollectionExtensions.cs` | `test/PatternKit.Hosting.Extensions.Tests/DependencyInjection/PatternKitServiceCollectionExtensionsTests.cs` | `docs/guides/hosting-extensions.md` |
| Priority Queue | `IServiceCollection` | `AddPatternKitPriorityQueue<TItem, TPriority>` | `src/PatternKit.Hosting.Extensions/DependencyInjection/PatternKitServiceCollectionExtensions.cs` | `test/PatternKit.Hosting.Extensions.Tests/DependencyInjection/PatternKitServiceCollectionExtensionsTests.cs` | `docs/guides/hosting-extensions.md` |
| Queue-Based Load Leveling | `IServiceCollection` | `AddPatternKitQueueLoadLevelingPolicy<TResult>` | `src/PatternKit.Hosting.Extensions/DependencyInjection/PatternKitServiceCollectionExtensions.cs` | `test/PatternKit.Hosting.Extensions.Tests/DependencyInjection/PatternKitServiceCollectionExtensionsTests.cs` | `docs/guides/hosting-extensions.md` |
| Rate Limiting | `IServiceCollection` | `AddPatternKitRateLimitPolicy<TResult>` | `src/PatternKit.Hosting.Extensions/DependencyInjection/PatternKitServiceCollectionExtensions.cs` | `test/PatternKit.Hosting.Extensions.Tests/DependencyInjection/PatternKitServiceCollectionExtensionsTests.cs` | `docs/guides/hosting-extensions.md` |
| Retry | `IServiceCollection` | `AddPatternKitRetryPolicy<TResult>` | `src/PatternKit.Hosting.Extensions/DependencyInjection/PatternKitServiceCollectionExtensions.cs` | `test/PatternKit.Hosting.Extensions.Tests/DependencyInjection/PatternKitServiceCollectionExtensionsTests.cs` | `docs/guides/hosting-extensions.md` |

## Pattern Matrix Results

| Category | Pattern | Fluent construction | Fluent execution | Generated construction | Generated execution |
| --- | --- | --- | --- | --- | --- |
| Application Architecture | Activity Tracker | Covered | Covered | Covered | Covered |
| Application Architecture | Manual Task Gate | Covered | Covered | Covered | Covered |
| Application Architecture | Workflow Orchestration | Covered | Covered | Covered | Covered |
| Application Architecture | Snapshot / Checkpoint Management | Covered | Covered | Covered | Covered |
| Application Architecture | Timeout Manager | Covered | Covered | Covered | Covered |
| Application Architecture | Aggregate Root | Covered | Covered | Covered | Covered |
| Application Architecture | Anti-Corruption Layer | Covered | Covered | Covered | Covered |
| Application Architecture | Audit Log | Covered | Covered | Covered | Covered |
| Application Architecture | Bounded Context | Covered | Covered | Covered | Covered |
| Application Architecture | Context Map | Covered | Covered | Covered | Covered |
| Application Architecture | CQRS | Covered | Covered | Covered | Covered |
| Application Architecture | Data Mapper | Covered | Covered | Covered | Covered |
| Application Architecture | Domain Event | Covered | Covered | Covered | Covered |
| Application Architecture | Domain Service | Covered | Covered | Covered | Covered |
| Application Architecture | Event Sourcing | Covered | Covered | Covered | Covered |
| Application Architecture | Feature Toggle | Covered | Covered | Covered | Covered |
| Application Architecture | Identity Map | Covered | Covered | Covered | Covered |
| Application Architecture | Materialized View | Covered | Covered | Covered | Covered |
| Application Architecture | Repository | Covered | Covered | Covered | Covered |
| Application Architecture | Service Layer | Covered | Covered | Covered | Covered |
| Application Architecture | Specification | Covered | Covered | Covered | Covered |
| Application Architecture | Value Object | Covered | Covered | Covered | Covered |
| Application Architecture | Table Data Gateway | Covered | Covered | Covered | Covered |
| Application Architecture | Transaction Script | Covered | Covered | Covered | Covered |
| Application Architecture | Unit of Work | Covered | Covered | Covered | Covered |
| Behavioral | Chain of Responsibility | Covered | Covered | Covered | Covered |
| Behavioral | Command | Covered | Covered | Covered | Covered |
| Behavioral | Interpreter | Covered | Covered | Covered | Covered |
| Behavioral | Iterator | Covered | Covered | Covered | Covered |
| Behavioral | Mediator | Covered | Covered | Covered | Covered |
| Behavioral | Memento | Covered | Covered | Covered | Covered |
| Behavioral | Observer | Covered | Covered | Covered | Covered |
| Behavioral | State | Covered | Covered | Covered | Covered |
| Behavioral | Strategy | Covered | Covered | Covered | Covered |
| Behavioral | Template Method | Covered | Covered | Covered | Covered |
| Behavioral | Visitor | Covered | Covered | Covered | Covered |
| Cloud Architecture | Ambassador | Covered | Covered | Covered | Covered |
| Cloud Architecture | Backends for Frontends | Covered | Covered | Covered | Covered |
| Cloud Architecture | Bulkhead | Covered | Covered | Covered | Covered |
| Cloud Architecture | Cache Stampede Protection | Covered | Covered | Covered | Covered |
| Cloud Architecture | Cache-Aside | Covered | Covered | Covered | Covered |
| Cloud Architecture | Circuit Breaker | Covered | Covered | Covered | Covered |
| Cloud Architecture | External Configuration Store | Covered | Covered | Covered | Covered |
| Cloud Architecture | Gateway Aggregation | Covered | Covered | Covered | Covered |
| Cloud Architecture | Gateway Routing | Covered | Covered | Covered | Covered |
| Cloud Architecture | Health Endpoint Monitoring | Covered | Covered | Covered | Covered |
| Cloud Architecture | Leader Election | Covered | Covered | Covered | Covered |
| Cloud Architecture | Priority Queue | Covered | Covered | Covered | Covered |
| Cloud Architecture | Queue-Based Load Leveling | Covered | Covered | Covered | Covered |
| Cloud Architecture | Rate Limiting | Covered | Covered | Covered | Covered |
| Cloud Architecture | Read-Through Cache | Covered | Covered | Covered | Covered |
| Cloud Architecture | Retry | Covered | Covered | Covered | Covered |
| Cloud Architecture | Scheduler Agent Supervisor | Covered | Covered | Covered | Covered |
| Cloud Architecture | Sidecar | Covered | Covered | Covered | Covered |
| Cloud Architecture | Strangler Fig | Covered | Covered | Covered | Covered |
| Cloud Architecture | Write-Through Cache | Covered | Covered | Covered | Covered |
| Creational | Abstract Factory | Covered | Covered | Covered | Covered |
| Creational | Builder | Covered | Covered | Covered | Covered |
| Creational | Factory Method | Covered | Covered | Covered | Covered |
| Creational | Prototype | Covered | Covered | Covered | Covered |
| Creational | Singleton | Covered | Covered | Covered | Covered |
| Enterprise Integration | Aggregator | Covered | Covered | Covered | Covered |
| Enterprise Integration | Canonical Data Model | Covered | Covered | Covered | Covered |
| Enterprise Integration | Channel Adapter | Covered | Covered | Covered | Covered |
| Enterprise Integration | Channel Purger | Covered | Covered | Covered | Covered |
| Enterprise Integration | Invalid Message Channel | Covered | Covered | Covered | Covered |
| Enterprise Integration | Claim Check | Covered | Covered | Covered | Covered |
| Enterprise Integration | Competing Consumers | Covered | Covered | Covered | Covered |
| Enterprise Integration | Content-Based Router | Covered | Covered | Covered | Covered |
| Enterprise Integration | Content Enricher | Covered | Covered | Covered | Covered |
| Enterprise Integration | Control Bus | Covered | Covered | Covered | Covered |
| Enterprise Integration | Dead Letter Channel | Covered | Covered | Covered | Covered |
| Enterprise Integration | Durable Subscriber | Covered | Covered | Covered | Covered |
| Enterprise Integration | Dynamic Router | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Bus | Covered | Covered | Covered | Covered |
| Enterprise Integration | Messaging Bridge | Covered | Covered | Covered | Covered |
| Enterprise Integration | Correlation Identifier | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message History | Covered | Covered | Covered | Covered |
| Enterprise Integration | Event Notification | Covered | Covered | Covered | Covered |
| Enterprise Integration | Event-Carried State Transfer | Covered | Covered | Covered | Covered |
| Enterprise Integration | Event-Driven Consumer | Covered | Covered | Covered | Covered |
| Enterprise Integration | Mailbox | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Channel | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Envelope | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Filter | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Expiration | Covered | Covered | Covered | Covered |
| Enterprise Integration | Guaranteed Delivery | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Store | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Translator | Covered | Covered | Covered | Covered |
| Enterprise Integration | Messaging Gateway | Covered | Covered | Covered | Covered |
| Enterprise Integration | Pipes and Filters | Covered | Covered | Covered | Covered |
| Enterprise Integration | Polling Consumer | Covered | Covered | Covered | Covered |
| Enterprise Integration | Publish-Subscribe | Covered | Covered | Covered | Covered |
| Enterprise Integration | Recipient List | Covered | Covered | Covered | Covered |
| Enterprise Integration | Request-Reply | Covered | Covered | Covered | Covered |
| Enterprise Integration | Resequencer | Covered | Covered | Covered | Covered |
| Enterprise Integration | Routing Slip | Covered | Covered | Covered | Covered |
| Enterprise Integration | Saga / Process Manager | Covered | Covered | Covered | Covered |
| Enterprise Integration | Scatter-Gather | Covered | Covered | Covered | Covered |
| Enterprise Integration | Service Activator | Covered | Covered | Covered | Covered |
| Enterprise Integration | Splitter | Covered | Covered | Covered | Covered |
| Enterprise Integration | Wire Tap | Covered | Covered | Covered | Covered |
| Messaging Reliability | Idempotent Receiver | Covered | Covered | Covered | Covered |
| Messaging Reliability | Inbox | Covered | Covered | Covered | Covered |
| Messaging Reliability | Outbox | Covered | Covered | Covered | Covered |
| Structural | Adapter | Covered | Covered | Covered | Covered |
| Structural | Bridge | Covered | Covered | Covered | Covered |
| Structural | Composite | Covered | Covered | Covered | Covered |
| Structural | Decorator | Covered | Covered | Covered | Covered |
| Structural | Facade | Covered | Covered | Covered | Covered |
| Structural | Flyweight | Covered | Covered | Covered | Covered |
| Structural | Proxy | Covered | Covered | Covered | Covered |

## Generator Matrix Results

| Generator | Source | Matrix result |
| --- | --- | --- |
| ActivityTrackerGenerator | `src/PatternKit.Generators/ActivityTracking/ActivityTrackerGenerator.cs` | Covered |
| TimeoutManagerGenerator | `src/PatternKit.Generators/Timeouts/TimeoutManagerGenerator.cs` | Covered |
| ManualTaskGateGenerator | `src/PatternKit.Generators/ManualTaskGates/ManualTaskGateGenerator.cs` | Covered |
| WorkflowOrchestrationGenerator | `src/PatternKit.Generators/WorkflowOrchestration/WorkflowOrchestrationGenerator.cs` | Covered |
| SnapshotCheckpointManagerGenerator | `src/PatternKit.Generators/SnapshotCheckpoints/SnapshotCheckpointManagerGenerator.cs` | Covered |
| AggregateCommandHandlerGenerator | `src/PatternKit.Generators/Aggregates/AggregateCommandHandlerGenerator.cs` | Covered |
| AdapterGenerator | `src/PatternKit.Generators/Adapter/AdapterGenerator.cs` | Covered |
| AmbassadorGenerator | `src/PatternKit.Generators/Ambassador/AmbassadorGenerator.cs` | Covered |
| AntiCorruptionLayerGenerator | `src/PatternKit.Generators/AntiCorruption/AntiCorruptionLayerGenerator.cs` | Covered |
| AuditLogGenerator | `src/PatternKit.Generators/AuditLog/AuditLogGenerator.cs` | Covered |
| BackendsForFrontendsGenerator | `src/PatternKit.Generators/BackendsForFrontends/BackendsForFrontendsGenerator.cs` | Covered |
| BridgeGenerator | `src/PatternKit.Generators/Bridge/BridgeGenerator.cs` | Covered |
| BuilderGenerator | `src/PatternKit.Generators/Builders/BuilderGenerator.cs` | Covered |
| BulkheadPolicyGenerator | `src/PatternKit.Generators/Bulkhead/BulkheadPolicyGenerator.cs` | Covered |
| CacheAsidePolicyGenerator | `src/PatternKit.Generators/CacheAside/CacheAsidePolicyGenerator.cs` | Covered |
| CacheStampedeProtectionGenerator | `src/PatternKit.Generators/CacheStampedeProtection/CacheStampedeProtectionGenerator.cs` | Covered |
| ReadWriteThroughCachePolicyGenerator | `src/PatternKit.Generators/ReadWriteThroughCache/ReadWriteThroughCachePolicyGenerator.cs` | Covered |
| CanonicalDataModelGenerator | `src/PatternKit.Generators/CanonicalDataModel/CanonicalDataModelGenerator.cs` | Covered |
| ChainGenerator | `src/PatternKit.Generators/Chain/ChainGenerator.cs` | Covered |
| CircuitBreakerPolicyGenerator | `src/PatternKit.Generators/CircuitBreaker/CircuitBreakerPolicyGenerator.cs` | Covered |
| ExternalConfigurationStoreGenerator | `src/PatternKit.Generators/Cloud/ExternalConfigurationStoreGenerator.cs` | Covered |
| CommandGenerator | `src/PatternKit.Generators/Command/CommandGenerator.cs` | Covered |
| ComposerGenerator | `src/PatternKit.Generators/ComposerGenerator.cs` | Covered |
| CompositeGenerator | `src/PatternKit.Generators/Composite/CompositeGenerator.cs` | Covered |
| DataMapperGenerator | `src/PatternKit.Generators/DataMapping/DataMapperGenerator.cs` | Covered |
| DecoratorGenerator | `src/PatternKit.Generators/DecoratorGenerator.cs` | Covered |
| DomainEventDispatcherGenerator | `src/PatternKit.Generators/DomainEvents/DomainEventDispatcherGenerator.cs` | Covered |
| BoundedContextDescriptorGenerator | `src/PatternKit.Generators/BoundedContexts/BoundedContextDescriptorGenerator.cs` | Covered |
| ContextMapDescriptorGenerator | `src/PatternKit.Generators/ContextMaps/ContextMapDescriptorGenerator.cs` | Covered |
| DomainServiceRegistryGenerator | `src/PatternKit.Generators/DomainServices/DomainServiceRegistryGenerator.cs` | Covered |
| EventCarriedStateTransferGenerator | `src/PatternKit.Generators/EventCarriedStateTransfer/EventCarriedStateTransferGenerator.cs` | Covered |
| EventNotificationGenerator | `src/PatternKit.Generators/EventNotification/EventNotificationGenerator.cs` | Covered |
| EventStoreGenerator | `src/PatternKit.Generators/EventSourcing/EventStoreGenerator.cs` | Covered |
| FacadeGenerator | `src/PatternKit.Generators/FacadeGenerator.cs` | Covered |
| AbstractFactoryGenerator | `src/PatternKit.Generators/Factories/AbstractFactoryGenerator.cs` | Covered |
| FactoriesGenerator | `src/PatternKit.Generators/Factories/FactoriesGenerator.cs` | Covered |
| FeatureToggleSetGenerator | `src/PatternKit.Generators/FeatureToggles/FeatureToggleSetGenerator.cs` | Covered |
| FlyweightGenerator | `src/PatternKit.Generators/Flyweight/FlyweightGenerator.cs` | Covered |
| GatewayAggregationGenerator | `src/PatternKit.Generators/GatewayAggregation/GatewayAggregationGenerator.cs` | Covered |
| GatewayRoutingGenerator | `src/PatternKit.Generators/GatewayRouting/GatewayRoutingGenerator.cs` | Covered |
| HealthEndpointMonitoringGenerator | `src/PatternKit.Generators/HealthEndpointMonitoring/HealthEndpointMonitoringGenerator.cs` | Covered |
| IdentityMapGenerator | `src/PatternKit.Generators/IdentityMap/IdentityMapGenerator.cs` | Covered |
| InterpreterGenerator | `src/PatternKit.Generators/Interpreter/InterpreterGenerator.cs` | Covered |
| IteratorGenerator | `src/PatternKit.Generators/Iterator/IteratorGenerator.cs` | Covered |
| LeaderElectionGenerator | `src/PatternKit.Generators/LeaderElection/LeaderElectionGenerator.cs` | Covered |
| MaterializedViewGenerator | `src/PatternKit.Generators/MaterializedViews/MaterializedViewGenerator.cs` | Covered |
| MementoGenerator | `src/PatternKit.Generators/MementoGenerator.cs` | Covered |
| BackplaneTopologyGenerator | `src/PatternKit.Generators/Messaging/BackplaneTopologyGenerator.cs` | Covered |
| ChannelAdapterGenerator | `src/PatternKit.Generators/Messaging/ChannelAdapterGenerator.cs` | Covered |
| ChannelPurgerGenerator | `src/PatternKit.Generators/Messaging/ChannelPurgerGenerator.cs` | Covered |
| InvalidMessageChannelGenerator | `src/PatternKit.Generators/Messaging/InvalidMessageChannelGenerator.cs` | Covered |
| ClaimCheckGenerator | `src/PatternKit.Generators/Messaging/ClaimCheckGenerator.cs` | Covered |
| CompetingConsumerGroupGenerator | `src/PatternKit.Generators/Messaging/CompetingConsumerGroupGenerator.cs` | Covered |
| ContentEnricherGenerator | `src/PatternKit.Generators/Messaging/ContentEnricherGenerator.cs` | Covered |
| ContentRouterGenerator | `src/PatternKit.Generators/Messaging/ContentRouterGenerator.cs` | Covered |
| ControlBusGenerator | `src/PatternKit.Generators/Messaging/ControlBusGenerator.cs` | Covered |
| DeadLetterChannelGenerator | `src/PatternKit.Generators/Messaging/DeadLetterChannelGenerator.cs` | Covered |
| DispatcherGenerator | `src/PatternKit.Generators/Messaging/DispatcherGenerator.cs` | Covered |
| DurableSubscriberGenerator | `src/PatternKit.Generators/Messaging/DurableSubscriberGenerator.cs` | Covered |
| DynamicRouterGenerator | `src/PatternKit.Generators/Messaging/DynamicRouterGenerator.cs` | Covered |
| EventDrivenConsumerGenerator | `src/PatternKit.Generators/Messaging/EventDrivenConsumerGenerator.cs` | Covered |
| MailboxGenerator | `src/PatternKit.Generators/Messaging/MailboxGenerator.cs` | Covered |
| MessageBusGenerator | `src/PatternKit.Generators/Messaging/MessageBusGenerator.cs` | Covered |
| MessagingBridgeGenerator | `src/PatternKit.Generators/Messaging/MessagingBridgeGenerator.cs` | Covered |
| MessageChannelGenerator | `src/PatternKit.Generators/Messaging/MessageChannelGenerator.cs` | Covered |
| MessageEnvelopeGenerator | `src/PatternKit.Generators/Messaging/MessageEnvelopeGenerator.cs` | Covered |
| MessageFilterGenerator | `src/PatternKit.Generators/Messaging/MessageFilterGenerator.cs` | Covered |
| MessageExpirationGenerator | `src/PatternKit.Generators/Messaging/MessageExpirationGenerator.cs` | Covered |
| GuaranteedDeliveryGenerator | `src/PatternKit.Generators/Messaging/GuaranteedDeliveryGenerator.cs` | Covered |
| CorrelationIdentifierGenerator | `src/PatternKit.Generators/Messaging/CorrelationIdentifierGenerator.cs` | Covered |
| MessageHistoryGenerator | `src/PatternKit.Generators/Messaging/MessageHistoryGenerator.cs` | Covered |
| MessageStoreGenerator | `src/PatternKit.Generators/Messaging/MessageStoreGenerator.cs` | Covered |
| MessageTranslatorGenerator | `src/PatternKit.Generators/Messaging/MessageTranslatorGenerator.cs` | Covered |
| MessagingGatewayGenerator | `src/PatternKit.Generators/Messaging/MessagingGatewayGenerator.cs` | Covered |
| PipesAndFiltersPipelineGenerator | `src/PatternKit.Generators/Messaging/PipesAndFiltersPipelineGenerator.cs` | Covered |
| PollingConsumerGenerator | `src/PatternKit.Generators/Messaging/PollingConsumerGenerator.cs` | Covered |
| RecipientListGenerator | `src/PatternKit.Generators/Messaging/RecipientListGenerator.cs` | Covered |
| ReliabilityPipelineGenerator | `src/PatternKit.Generators/Messaging/ReliabilityPipelineGenerator.cs` | Covered |
| ResequencerGenerator | `src/PatternKit.Generators/Messaging/ResequencerGenerator.cs` | Covered |
| RoutingSlipGenerator | `src/PatternKit.Generators/Messaging/RoutingSlipGenerator.cs` | Covered |
| SagaGenerator | `src/PatternKit.Generators/Messaging/SagaGenerator.cs` | Covered |
| ScatterGatherGenerator | `src/PatternKit.Generators/Messaging/ScatterGatherGenerator.cs` | Covered |
| ServiceActivatorGenerator | `src/PatternKit.Generators/Messaging/ServiceActivatorGenerator.cs` | Covered |
| SplitterAggregatorGenerator | `src/PatternKit.Generators/Messaging/SplitterAggregatorGenerator.cs` | Covered |
| WireTapGenerator | `src/PatternKit.Generators/Messaging/WireTapGenerator.cs` | Covered |
| ObserverGenerator | `src/PatternKit.Generators/Observer/ObserverGenerator.cs` | Covered |
| PriorityQueueGenerator | `src/PatternKit.Generators/PriorityQueue/PriorityQueueGenerator.cs` | Covered |
| PrototypeGenerator | `src/PatternKit.Generators/PrototypeGenerator.cs` | Covered |
| ProxyGenerator | `src/PatternKit.Generators/ProxyGenerator.cs` | Covered |
| QueueLoadLevelingPolicyGenerator | `src/PatternKit.Generators/QueueLoadLeveling/QueueLoadLevelingPolicyGenerator.cs` | Covered |
| RateLimitPolicyGenerator | `src/PatternKit.Generators/RateLimiting/RateLimitPolicyGenerator.cs` | Covered |
| RepositoryGenerator | `src/PatternKit.Generators/Repository/RepositoryGenerator.cs` | Covered |
| RetryPolicyGenerator | `src/PatternKit.Generators/Retry/RetryPolicyGenerator.cs` | Covered |
| SchedulerAgentSupervisorGenerator | `src/PatternKit.Generators/SchedulerAgentSupervisor/SchedulerAgentSupervisorGenerator.cs` | Covered |
| ServiceLayerOperationGenerator | `src/PatternKit.Generators/ServiceLayer/ServiceLayerOperationGenerator.cs` | Covered |
| SidecarGenerator | `src/PatternKit.Generators/Sidecar/SidecarGenerator.cs` | Covered |
| SingletonGenerator | `src/PatternKit.Generators/Singleton/SingletonGenerator.cs` | Covered |
| SpecificationGenerator | `src/PatternKit.Generators/Specification/SpecificationGenerator.cs` | Covered |
| ValueObjectGenerator | `src/PatternKit.Generators/ValueObjects/ValueObjectGenerator.cs` | Covered |
| StateMachineGenerator | `src/PatternKit.Generators/StateMachineGenerator.cs` | Covered |
| StranglerFigGenerator | `src/PatternKit.Generators/StranglerFig/StranglerFigGenerator.cs` | Covered |
| StrategyGenerator | `src/PatternKit.Generators/StrategyGenerator.cs` | Covered |
| TableDataGatewayGenerator | `src/PatternKit.Generators/TableDataGateway/TableDataGatewayGenerator.cs` | Covered |
| TemplateGenerator | `src/PatternKit.Generators/TemplateGenerator.cs` | Covered |
| TransactionScriptGenerator | `src/PatternKit.Generators/TransactionScript/TransactionScriptGenerator.cs` | Covered |
| UnitOfWorkGenerator | `src/PatternKit.Generators/UnitOfWork/UnitOfWorkGenerator.cs` | Covered |
| VisitorGenerator | `src/PatternKit.Generators/VisitorGenerator.cs` | Covered |

## Reproducing Results

Run the scenario benchmarks:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --filter *LeaderElection* --artifacts artifacts/benchmarks --join
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --filter *SchedulerAgentSupervisor* --artifacts artifacts/benchmarks --join
```

Run the full reportable benchmark suite:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --artifacts artifacts/benchmarks --join
```

Run only the matrix routes when validating benchmark coverage changes:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --filter *Matrix* --artifacts artifacts/benchmarks --join --job short
```
