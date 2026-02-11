# Adapter Generator

## Overview

The **Adapter Generator** creates object adapters that implement a target contract (interface or abstract class) by delegating to an adaptee through explicit mapping methods. This pattern allows incompatible interfaces to work together without modifying either the target or adaptee.

## When to Use

Use the Adapter generator when you need to:

- **Integrate legacy code**: Wrap older implementations to work with modern interfaces
- **Abstract third-party libraries**: Create a clean boundary around external dependencies
- **Support multiple implementations**: Adapt different backends (payment gateways, loggers, etc.) to a unified interface
- **Compile-time safety**: Ensure all contract members are properly mapped

## Installation

The generator is included in the `PatternKit.Generators` package:

```bash
dotnet add package PatternKit.Generators
```

## Quick Start

```csharp
using PatternKit.Generators.Adapter;

// Target interface your app uses
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

// Legacy class with different API
public class LegacyClock
{
    public DateTime GetCurrentTimeUtc() => DateTime.UtcNow;
}

// Define mappings in a static partial class
[GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
public static partial class ClockAdapters
{
    [AdapterMap(TargetMember = nameof(IClock.UtcNow))]
    public static DateTimeOffset MapUtcNow(LegacyClock adaptee)
        => new(adaptee.GetCurrentTimeUtc(), TimeSpan.Zero);
}
```

Generated:
```csharp
public sealed partial class LegacyClockToIClockAdapter : IClock
{
    private readonly LegacyClock _adaptee;

    public LegacyClockToIClockAdapter(LegacyClock adaptee)
    {
        _adaptee = adaptee ?? throw new ArgumentNullException(nameof(adaptee));
    }

    public DateTimeOffset UtcNow
    {
        get => ClockAdapters.MapUtcNow(_adaptee);
    }
}
```

Usage:
```csharp
// Create the adapter
IClock clock = new LegacyClockToIClockAdapter(new LegacyClock());

// Use through the clean interface
var now = clock.UtcNow;
```

## Mapping Methods

Each target contract member needs a mapping method marked with `[AdapterMap]`.

### Property Mappings

For properties, the mapping method takes only the adaptee and returns the property type:

```csharp
public interface IService
{
    string Name { get; }
}

[AdapterMap(TargetMember = nameof(IService.Name))]
public static string MapName(LegacyService adaptee) => adaptee.ServiceName;
```

### Method Mappings

For methods, the mapping method takes the adaptee as the first parameter, followed by all method parameters:

```csharp
public interface ICalculator
{
    int Add(int a, int b);
}

[AdapterMap(TargetMember = nameof(ICalculator.Add))]
public static int MapAdd(OldCalculator adaptee, int a, int b)
    => adaptee.Sum(a, b);
```

### Async Method Mappings

Async methods work the same way - just match the return type:

```csharp
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(string token, decimal amount, CancellationToken ct);
}

[AdapterMap(TargetMember = nameof(IPaymentGateway.ChargeAsync))]
public static async Task<PaymentResult> MapChargeAsync(
    LegacyPaymentClient adaptee,
    string token,
    decimal amount,
    CancellationToken ct)
{
    var response = await adaptee.ProcessPaymentAsync(token, (int)(amount * 100), ct);
    return new PaymentResult(response.Success, response.Id);
}
```

## Attributes

### `[GenerateAdapter]`

Marks a static partial class as an adapter mapping host.

| Property | Type | Default | Description |
|---|---|---|---|
| `Target` | `Type` | Required | The interface or abstract class to implement |
| `Adaptee` | `Type` | Required | The class to adapt |
| `AdapterTypeName` | `string` | `{Adaptee}To{Target}Adapter` | Custom name for the generated adapter class |
| `MissingMap` | `AdapterMissingMapPolicy` | `Error` | How to handle unmapped members |
| `Sealed` | `bool` | `true` | Whether the adapter class is sealed |
| `Namespace` | `string` | Host namespace | Custom namespace for the adapter |

### `[AdapterMap]`

Marks a method as a mapping for a target member.

| Property | Type | Default | Description |
|---|---|---|---|
| `TargetMember` | `string` | Required | Name of the target member (use `nameof()`) |

## Missing Map Policies

Control what happens when a target member has no `[AdapterMap]`:

### Error (Default)

Emits a compiler error. Recommended for production code:

```csharp
[GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
// MissingMap = AdapterMissingMapPolicy.Error is the default
```

### ThrowingStub

Generates a stub that throws `NotImplementedException`. Useful during incremental development:

```csharp
[GenerateAdapter(
    Target = typeof(IClock),
    Adaptee = typeof(LegacyClock),
    MissingMap = AdapterMissingMapPolicy.ThrowingStub)]
```

### Ignore

Silently ignores unmapped members. May cause compilation errors if the target is an interface (missing implementations):

```csharp
[GenerateAdapter(
    Target = typeof(IPartialService),
    Adaptee = typeof(Legacy),
    MissingMap = AdapterMissingMapPolicy.Ignore)]
```

## Multiple Adapters

You can define multiple adapters in the same host class:

```csharp
[GenerateAdapter(Target = typeof(IPaymentGateway), Adaptee = typeof(StripeClient))]
[GenerateAdapter(Target = typeof(IPaymentGateway), Adaptee = typeof(PayPalClient))]
public static partial class PaymentAdapters
{
    [AdapterMap(TargetMember = nameof(IPaymentGateway.ChargeAsync))]
    public static Task<PaymentResult> MapStripeChargeAsync(StripeClient adaptee, ...) { ... }

    [AdapterMap(TargetMember = nameof(IPaymentGateway.ChargeAsync))]
    public static Task<PaymentResult> MapPayPalChargeAsync(PayPalClient adaptee, ...) { ... }
}
```

The generator matches mapping methods to adapters by the first parameter type (adaptee).

## Abstract Class Targets

The generator supports abstract classes as targets:

```csharp
public abstract class ClockBase
{
    public abstract DateTimeOffset Now { get; }
    public virtual string TimeZone => "UTC"; // Inherited, not in contract
}

[GenerateAdapter(Target = typeof(ClockBase), Adaptee = typeof(LegacyClock))]
public static partial class Adapters
{
    [AdapterMap(TargetMember = nameof(ClockBase.Now))]
    public static DateTimeOffset MapNow(LegacyClock adaptee) => ...;
    // Only abstract members need mapping
}
```

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| **PKADP001** | Error | Adapter host must be `static partial` |
| **PKADP002** | Error | Target must be interface or abstract class |
| **PKADP003** | Error | Missing `[AdapterMap]` for target member |
| **PKADP004** | Error | Multiple `[AdapterMap]` methods for same target member |
| **PKADP005** | Error | Mapping method signature doesn't match target member |
| **PKADP006** | Error | Adapter type name conflicts with existing type |
| **PKADP007** | Error | Adaptee must be a concrete class or struct |
| **PKADP008** | Error | Mapping method must be static |
| **PKADP009** | Error | Events are not supported |
| **PKADP010** | Error | Generic methods are not supported |
| **PKADP011** | Error | Overloaded methods are not supported |
| **PKADP012** | Error | Abstract class target requires accessible parameterless constructor |
| **PKADP013** | Error | Settable properties are not supported |
| **PKADP014** | Error | Nested or generic host not supported |
| **PKADP015** | Error | Mapping method must be accessible (public or internal) |
| **PKADP016** | Error | Static members are not supported |
| **PKADP017** | Error | Ref-return members are not supported |
| **PKADP018** | Error | Indexers are not supported |

## Limitations

### Multiple Adapters with Shared Adaptee

When defining multiple `[GenerateAdapter]` attributes within the same host class that share the same adaptee type, mapping ambiguity can occur. The generator matches `[AdapterMap]` methods to adapters solely by adaptee type and then by `TargetMember` name. If two target types have overlapping member names (both use `nameof(...)` resulting in the same string), mappings become ambiguous and may trigger false `PKADP004` duplicate mapping diagnostics.

**Workaround:** Define separate host classes for each adapter when they share the same adaptee type:

```csharp
// ✅ Good: Separate hosts avoid ambiguity
[GenerateAdapter(Target = typeof(IServiceA), Adaptee = typeof(LegacyService))]
public static partial class ServiceAAdapters
{
    [AdapterMap(TargetMember = nameof(IServiceA.DoWork))]
    public static void MapDoWork(LegacyService adaptee) => adaptee.Execute();
}

[GenerateAdapter(Target = typeof(IServiceB), Adaptee = typeof(LegacyService))]
public static partial class ServiceBAdapters
{
    [AdapterMap(TargetMember = nameof(IServiceB.DoWork))]
    public static void MapDoWork(LegacyService adaptee) => adaptee.Run();
}

// ⚠️ Problematic: Multiple adapters with same adaptee in one host
public static partial class AllAdapters
{
    // Both IServiceA and IServiceB have DoWork() members
    // The generator cannot distinguish which mapping is for which target
}
```

## Best Practices

### 1. Use `nameof()` for Type Safety

```csharp
// ✅ Good: Compile-time checked
[AdapterMap(TargetMember = nameof(IClock.Now))]

// ❌ Bad: String literals can drift
[AdapterMap(TargetMember = "Now")]
```

### 2. Keep Mapping Methods Simple

Mapping methods should be thin wrappers, not business logic:

```csharp
// ✅ Good: Simple delegation with conversion
[AdapterMap(TargetMember = nameof(IService.DoWork))]
public static void MapDoWork(Legacy adaptee, string input)
    => adaptee.PerformTask(input);

// ❌ Bad: Business logic in mapping
[AdapterMap(TargetMember = nameof(IService.DoWork))]
public static void MapDoWork(Legacy adaptee, string input)
{
    if (string.IsNullOrEmpty(input)) throw new ArgumentException();
    var processed = input.ToUpper().Trim();
    adaptee.PerformTask(processed);
    // This logic should be elsewhere
}
```

### 3. Separate Mapping Hosts by Domain

```csharp
// ✅ Good: Organized by domain
public static partial class PaymentAdapters { ... }
public static partial class LoggingAdapters { ... }

// ❌ Bad: Everything in one place
public static partial class AllAdapters { ... }
```

### 4. Document Complex Mappings

```csharp
/// <summary>
/// Maps the legacy millisecond-based delay to TimeSpan.
/// Note: Precision is limited to milliseconds.
/// </summary>
[AdapterMap(TargetMember = nameof(IClock.DelayAsync))]
public static ValueTask MapDelayAsync(LegacyClock adaptee, TimeSpan duration, CancellationToken ct)
    => new(adaptee.Sleep((int)duration.TotalMilliseconds, ct));
```

## Real-World Example: Payment Gateway Abstraction

```csharp
// Unified interface for your application
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(string token, decimal amount, string currency, CancellationToken ct);
    Task<RefundResult> RefundAsync(string transactionId, decimal amount, CancellationToken ct);
    string GatewayName { get; }
}

// Stripe adapter
[GenerateAdapter(Target = typeof(IPaymentGateway), Adaptee = typeof(StripeClient), AdapterTypeName = "StripePaymentAdapter")]
public static partial class StripeAdapters
{
    [AdapterMap(TargetMember = nameof(IPaymentGateway.GatewayName))]
    public static string MapGatewayName(StripeClient adaptee) => "Stripe";

    [AdapterMap(TargetMember = nameof(IPaymentGateway.ChargeAsync))]
    public static async Task<PaymentResult> MapChargeAsync(
        StripeClient adaptee, string token, decimal amount, string currency, CancellationToken ct)
    {
        var request = new StripeChargeRequest { Source = token, Amount = (long)(amount * 100), Currency = currency };
        var response = await adaptee.CreateChargeAsync(request, ct);
        return new PaymentResult(response.Succeeded, response.ChargeId, response.Error);
    }

    [AdapterMap(TargetMember = nameof(IPaymentGateway.RefundAsync))]
    public static async Task<RefundResult> MapRefundAsync(
        StripeClient adaptee, string transactionId, decimal amount, CancellationToken ct)
    {
        var response = await adaptee.CreateRefundAsync(transactionId, (long)(amount * 100), ct);
        return new RefundResult(response.Succeeded, response.RefundId, response.Error);
    }
}

// Usage with DI
services.AddSingleton<StripeClient>();
services.AddSingleton<IPaymentGateway>(sp => new StripePaymentAdapter(sp.GetRequiredService<StripeClient>()));
```

## See Also

- [Facade Generator](facade.md) - For simplifying complex subsystems
- [Decorator Generator](decorator.md) - For adding behavior to objects
- [Proxy Generator](proxy.md) - For controlling access to objects
