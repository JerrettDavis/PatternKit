# Loan Approval Specifications

This example demonstrates a production-style Specification pattern for loan approvals. It includes a fluent registry, a source-generated registry, TinyBDD tests, and an `IServiceCollection` extension.

## Import

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.SpecificationDemo;

var services = new ServiceCollection();
services.AddLoanApprovalSpecifications();

using var provider = services.BuildServiceProvider(validateScopes: true);
var service = provider.GetRequiredService<LoanApprovalService>();
```

## Evaluate

```csharp
var application = LoanApprovalSpecificationDemo.CreatePrimeApplication();
var decision = service.Evaluate(application);
```

The service uses a generated `SpecificationRegistry<LoanApplication>` so importing applications can replace, decorate, or inspect the named rule registry through standard .NET IoC tooling.

## Rules

- `verified-identity`
- `clear-fraud`
- `prime-credit`
- `stable-income`
- `affordable`
- `approval-ready`

The fluent and generated paths are tested against the same loan applications so consumers can choose either style without changing application behavior.
