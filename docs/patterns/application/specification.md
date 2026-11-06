# Specification

Specification packages business rules as reusable predicates that can be composed, named, tested, and registered in dependency injection.

Use it when a domain decision has several independently meaningful rules, such as approval checks, eligibility gates, routing policies, or validation criteria.

## Fluent Path

```csharp
using PatternKit.Application.Specification;

var verified = Specification<LoanApplication>
    .Where("verified-identity", application => application.HasVerifiedIdentity);

var clearFraud = Specification<LoanApplication>
    .Where("clear-fraud", application => !application.HasFraudHold);

var approval = verified.And(clearFraud, "approval-ready");

var registry = SpecificationRegistry<LoanApplication>.Create()
    .Add(verified.Name, verified)
    .Add(clearFraud.Name, clearFraud)
    .Add(approval.Name, approval)
    .Build();
```

`SpecificationRegistry<T>` is the production integration point. It gives application services a stable named rule set without forcing them to know how each rule was composed.

## Generated Path

```csharp
using PatternKit.Generators.Specification;

[GenerateSpecificationRegistry(typeof(LoanApplication))]
public static partial class LoanApprovalRules
{
    [SpecificationRule("prime-credit")]
    private static bool PrimeCredit(LoanApplication application)
        => application.CreditScore >= 700;
}
```

The generator emits a static factory returning `SpecificationRegistry<LoanApplication>`. Generated registries are useful when rule names and method signatures should be compile-time checked.

## IoC Usage

```csharp
services.AddSingleton(_ => LoanApprovalRules.Create());
services.AddSingleton<LoanApprovalService>();
```

The example in `docs/examples/loan-approval-specifications.md` shows a complete importable `IServiceCollection` integration.
