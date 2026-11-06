# Generated Recipient List

The generated recipient-list example shows event fan-out with both runtime fluent composition and an attribute-driven source-generated factory.

## Register

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;

var services = new ServiceCollection()
    .AddGeneratedRecipientListExample();

using var provider = services.BuildServiceProvider(validateScopes: true);
var example = provider.GetRequiredService<GeneratedRecipientListExample>();
```

## Fluent Path

```csharp
var fluent = example.Runner.RunFluent();
```

The fluent path builds a `RecipientList<GeneratedShipmentEvent>` with predicates and handlers registered in code.

## Source-Generated Path

```csharp
var generated = example.Runner.RunGenerated();
```

The generated path uses `[GenerateRecipientList]` on a partial type and `[RecipientListRecipient]` on static recipient handlers. The generator emits a strongly typed factory that builds the same `RecipientList<TPayload>` used by the fluent API.

## Production Shape

The TinyBDD scenarios validate that:

- fluent and generated paths deliver the same recipients in deterministic order
- handler side effects are visible through a scoped `MessageContext`
- the example is importable through `IServiceCollection`
- the example advertises dependency injection, messaging, and source-generation integration surfaces
