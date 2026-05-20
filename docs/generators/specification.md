# Specification Generator

The Specification generator turns annotated static rule methods into a named `SpecificationRegistry<TCandidate>` factory.

## Usage

```csharp
using PatternKit.Generators.Specification;

[GenerateSpecificationRegistry(typeof(LoanApplication), FactoryMethodName = "Build")]
public static partial class LoanApprovalRules
{
    [SpecificationRule("verified-identity")]
    private static bool VerifiedIdentity(LoanApplication application)
        => application.HasVerifiedIdentity;
}
```

Generated output:

```csharp
var registry = LoanApprovalRules.Build();
var approved = registry.IsSatisfiedBy("verified-identity", application);
```

## Rule Shape

Specification rules must be static methods with this shape:

```csharp
static bool Rule(TCandidate candidate)
```

Rule names must be unique within the generated registry.

## Diagnostics

| ID | Meaning |
|---|---|
| `PKSPEC001` | Host type must be `partial`. |
| `PKSPEC002` | Host type has no `[SpecificationRule]` methods. |
| `PKSPEC003` | Rule method signature is invalid. |
| `PKSPEC004` | Rule name is duplicated. |

## Dependency Injection

```csharp
services.AddSingleton(_ => LoanApprovalRules.Build());
services.AddSingleton<LoanApprovalService>();
```

Use generated registries when teams want a stable, named rule surface that can be injected into existing ASP.NET Core or Generic Host applications.
