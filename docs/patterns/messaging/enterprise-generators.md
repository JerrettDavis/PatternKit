# Enterprise Integration Source Generators

PatternKit source generators remove repetitive registration code for explicit enterprise integration patterns. They do not scan assemblies implicitly; each generated factory is opt-in through attributes on a partial type.

Use generators when routes, routing-slip steps, or saga transitions are static enough to validate at compile time and you want AOT-friendly factories without reflection.

## Generated Content Router

`[GenerateContentRouter]` creates a `ContentRouter<TPayload, TResult>` factory from static route predicates and handlers.

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

[GenerateContentRouter(typeof(Order), typeof(string))]
public static partial class OrderRouter
{
    private static bool IsWholesale(Message<Order> message, MessageContext context)
        => message.Payload.Channel == "wholesale";

    [ContentRoute("wholesale", 10, nameof(IsWholesale))]
    private static string Wholesale(Message<Order> message, MessageContext context)
        => "wholesale";

    [ContentRouteDefault]
    private static string Default(Message<Order> message, MessageContext context)
        => "default";
}

var route = OrderRouter.Create().Route(Message<Order>.Create(order));
```

Route handlers must be static and have this shape:

```csharp
TResult Handler(Message<TPayload> message, MessageContext context)
```

Predicates referenced by `ContentRouteAttribute.PredicateMethodName` must be static and have this shape:

```csharp
bool Predicate(Message<TPayload> message, MessageContext context)
```

The generator orders routes by `ContentRouteAttribute.Order`, then by route name. Route names and orders must be unique so the generated first-match behavior is clear.

## Existing Enterprise Generators

Routing-slip generation is documented in [Routing Slip](routing-slip.md). It discovers `[RoutingSlipStep]` methods and emits sync or async itinerary factories.

Saga/process-manager generation is documented in [Saga / Process Manager](saga.md). It discovers `[SagaStep]` transition methods and optional `[SagaCompleteWhen]` completion checks.

Mailbox and reliability helpers stay runtime-only for now. Their registration is already small and lifecycle-sensitive, so a generator would add indirection without removing meaningful boilerplate.

## Diagnostics

| ID | Meaning |
| --- | --- |
| `PKCR001` | `[GenerateContentRouter]` was placed on a non-partial type. |
| `PKCR002` | The generated content router has no `[ContentRoute]` methods. |
| `PKCR003` | A route handler or referenced predicate has an invalid signature. |
| `PKCR004` | The default route handler has an invalid signature or more than one default handler is declared. |
| `PKCR005` | A route name or route order is duplicated. |
| `PKRS001`-`PKRS003` | Routing-slip generator validation. |
| `PKSG001`-`PKSG004` | Saga generator validation. |

## Troubleshooting

- Make the generated host type `partial`.
- Keep route, step, and saga methods `static`; generated factories reference them directly.
- Use `nameof(PredicateMethod)` in `[ContentRoute]` so renames remain compile-time safe.
- Use unique route names and orders. Content routers are first-match, so ambiguous ordering should fail at build time.
- Ensure generated code builds under nullable enabled; the tests compile generated examples with Release settings.

## API

- <xref:PatternKit.Generators.Messaging.GenerateContentRouterAttribute>
- <xref:PatternKit.Generators.Messaging.ContentRouteAttribute>
- <xref:PatternKit.Generators.Messaging.ContentRouteDefaultAttribute>
- <xref:PatternKit.Generators.Messaging.GenerateRoutingSlipAttribute>
- <xref:PatternKit.Generators.Messaging.RoutingSlipStepAttribute>
- <xref:PatternKit.Generators.Messaging.GenerateSagaAttribute>
- <xref:PatternKit.Generators.Messaging.SagaStepAttribute>
- <xref:PatternKit.Generators.Messaging.SagaCompleteWhenAttribute>
- <xref:PatternKit.Messaging.Routing.ContentRouter`2>

## Example Source

- `src/PatternKit.Examples/Messaging/ContentRouterGeneratorExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/ContentRouterGeneratorExampleTests.cs`
