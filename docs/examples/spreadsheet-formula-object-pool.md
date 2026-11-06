# Spreadsheet Formula Object Pool

This example pools resettable formula evaluation buffers for a spreadsheet service. It demonstrates both the fluent and source-generated Object Pool routes, then registers the generated pool through `IServiceCollection`.

```csharp
services.AddSpreadsheetFormulaObjectPoolDemo();

var runner = provider.GetRequiredService<SpreadsheetFormulaObjectPoolDemoRunner>();
var result = runner.Run(new FormulaEvaluationRequest(
    "D4",
    "subtotal + tax + 5",
    new Dictionary<string, decimal>
    {
        ["subtotal"] = 100m,
        ["tax"] = 8.25m
    }));
```

The pool is singleton-scoped, while every formula evaluation uses a short-lived lease. Returning the lease resets the buffer and keeps the retained pool bounded.
