# Null Object

The Null Object pattern replaces optional collaborators with a production fallback object that implements the same contract and performs deterministic no-op behavior.

Use it when a service should continue safely without scattering `null` checks through application code. Typical examples include optional notification channels, audit sinks, metrics publishers, feature integrations, and external adapters disabled by tenant or environment.

## Fluent Path

```csharp
var fallback = NullObject<ICustomerNotificationChannel>
    .Create(NullCustomerNotificationChannel.Instance)
    .Build();

services.AddSingleton(fallback);
services.AddSingleton(fallback.Instance);
```

The fluent wrapper gives IoC registrations a stable, explicit fallback collaborator.

## Source-Generated Path

```csharp
[GenerateNullObject(TypeName = "NullCustomerNotificationChannel")]
public interface ICustomerNotificationChannel
{
    [NullObjectDefault("suppressed")]
    string Send(CustomerNotification notification);
}
```

The generator emits a sealed implementation with an `Instance` singleton and safe default behavior for methods and properties.
