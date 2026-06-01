# Null Object Notification Example

The customer notification example shows how to keep a workflow production-safe when an optional notification provider is not configured.

The generated `NullCustomerNotificationChannel` implements `ICustomerNotificationChannel` and returns a deterministic `"suppressed"` status. The fluent `NullObject<ICustomerNotificationChannel>` wrapper is registered through `IServiceCollection` so importing applications can inject the fallback channel without special-case `null` logic.

Relevant files:

- `src/PatternKit.Examples/NullObjectDemo/CustomerNotificationNullObjectDemo.cs`
- `test/PatternKit.Examples.Tests/NullObjectDemo/CustomerNotificationNullObjectDemoTests.cs`
