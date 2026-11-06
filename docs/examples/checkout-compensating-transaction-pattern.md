# Checkout Compensating Transaction Pattern

The checkout compensating transaction example models inventory reservation, payment authorization, and shipment creation. When shipment creation fails, the transaction voids payment and releases inventory in reverse order.

It demonstrates fluent and source-generated transaction construction, TinyBDD coverage, BenchmarkDotNet coverage, and `IServiceCollection` import through `AddCheckoutCompensatingTransactionDemo()`.

Files:

- `src/PatternKit.Examples/CompensatingTransactionDemo/CheckoutCompensatingTransactionDemo.cs`
- `test/PatternKit.Examples.Tests/CompensatingTransactionDemo/CheckoutCompensatingTransactionDemoTests.cs`
