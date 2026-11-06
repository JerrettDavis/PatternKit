# Context Map Generator

`ContextMapDescriptorGenerator` emits a `ContextMapDescriptor` factory from relationship attributes on a partial class or struct.

```csharp
[GenerateContextMapDescriptor("Commerce", FactoryMethodName = "Build")]
[ContextMapRelationship("Catalog", "Fulfillment", ContextMapRelationshipKind.PublishedLanguage, "ProductFeed")]
public static partial class CommerceMap;
```

Diagnostics:

| Id | Meaning |
| --- | --- |
| `PKCMAP001` | The host type must be partial. |
| `PKCMAP002` | At least one relationship is required. |
| `PKCMAP003` | Relationship registrations must be unique. |
