# Strangler Fig Generator

`[GenerateStranglerFig]` creates a typed `StranglerFig<TRequest, TResponse>` factory from route predicates and one handler for each side of the migration.

```csharp
[GenerateStranglerFig(typeof(CheckoutMigrationRequest), typeof(CheckoutMigrationResponse), MigrationName = "checkout-strangler")]
public static partial class CheckoutMigration
{
    [StranglerFigRoute("enterprise-tenant")]
    private static bool IsEnterprise(CheckoutMigrationRequest request) => request.TenantId.StartsWith("enterprise-");

    [StranglerFigLegacy]
    private static CheckoutMigrationResponse Legacy(CheckoutMigrationRequest request) => LegacySystem.Submit(request);

    [StranglerFigModern]
    private static CheckoutMigrationResponse Modern(CheckoutMigrationRequest request) => ModernSystem.Submit(request);
}
```

Diagnostics:

- `PKSF001`: host type must be partial.
- `PKSF002`: at least one route, exactly one legacy handler, and exactly one modern handler are required.
- `PKSF003`: route or handler signature is invalid.
- `PKSF004`: route names must be unique.
