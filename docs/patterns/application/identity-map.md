# Identity Map

Identity Map preserves object identity inside a request, unit of work, or other application scope. Use it when repeated loads of the same key should return the same object instance and duplicate tracked instances should be rejected.

## Fluent Path

```csharp
var map = IdentityMap<Order, string>.Create(order => order.OrderId)
    .UseComparer(StringComparer.OrdinalIgnoreCase)
    .Build();

var order = map.GetOrAdd("order-100", key => repository.Load(key));
var same = map.GetOrAdd("order-100", key => repository.Load(key));
```

`same` is the same object reference as `order`. `Track` returns `IdentityMapResult<T>` so duplicate-key conflicts are explicit.

## DI Usage

Register identity maps as scoped services:

```csharp
services.AddScoped<IIdentityMap<Order, string>>(_ =>
    IdentityMap<Order, string>.Create(order => order.OrderId).Build());
```

See [Order Identity Map Pattern](../../examples/order-identity-map-pattern.md).
