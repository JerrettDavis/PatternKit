# Context Map

Context Map documents how bounded contexts relate to each other. It captures upstream/downstream direction, the relationship style, and the contract used between contexts.

Use the fluent path when the map is assembled from configuration or architecture metadata:

```csharp
var map = ContextMapDescriptor.Create("Commerce")
    .AddRelationship("Catalog", "Fulfillment", ContextRelationshipKind.PublishedLanguage, "ProductFeed")
    .AddRelationship("Fulfillment", "Billing", ContextRelationshipKind.CustomerSupplier, "ShipmentBilling")
    .Build();
```

Use the generated path when the map is stable and should be reviewed in source:

```csharp
[GenerateContextMapDescriptor("Commerce")]
[ContextMapRelationship("Catalog", "Fulfillment", ContextMapRelationshipKind.PublishedLanguage, "ProductFeed")]
[ContextMapRelationship("Fulfillment", "Billing", ContextMapRelationshipKind.CustomerSupplier, "ShipmentBilling")]
public static partial class CommerceMap;
```

Register the generated descriptor in DI so services, health checks, documentation tools, or architecture tests can inspect context relationships at runtime.
