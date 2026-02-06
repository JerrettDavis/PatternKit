# Template Method Pattern Generator

The Template Method Pattern Generator automatically creates execution orchestration methods for workflows defined with ordered steps and lifecycle hooks. It eliminates boilerplate code for sequential processing pipelines while providing deterministic execution, error handling, and async/await support.

## Overview

The generator produces:

- **Execute method** for synchronous workflows
- **ExecuteAsync method** for asynchronous workflows with ValueTask and CancellationToken support
- **Deterministic step ordering** based on explicit Order values
- **Lifecycle hooks** (BeforeAll, AfterAll, OnError)
- **Error handling** with configurable policies
- **Zero runtime overhead** through source generation

## Quick Start

### 1. Define Your Workflow Host

Mark your workflow class with `[Template]` and define steps:

```csharp
using PatternKit.Generators.Template;

public class ImportContext
{
    public List<string> ProcessedItems { get; } = new();
}

[Template]
public partial class ImportWorkflow
{
    [TemplateStep(0)]
    private void Load(ImportContext ctx)
    {
        // Load data
    }

    [TemplateStep(1)]
    private void Validate(ImportContext ctx)
    {
        // Validate data
    }

    [TemplateStep(2)]
    private void Transform(ImportContext ctx)
    {
        // Transform data
    }

    [TemplateStep(3)]
    private void Persist(ImportContext ctx)
    {
        // Persist results
    }
}
```

### 2. Build Your Project

The generator runs during compilation and produces an `Execute` method:

```csharp
var ctx = new ImportContext();
var workflow = new ImportWorkflow();
workflow.Execute(ctx);  // Runs all steps in order
```

### 3. Generated Code

```csharp
partial class ImportWorkflow
{
    public void Execute(ImportContext ctx)
    {
        Load(ctx);
        Validate(ctx);
        Transform(ctx);
        Persist(ctx);
    }
}
```

## Core Concepts

### Steps

Steps define the sequence of operations in your workflow:

```csharp
[TemplateStep(order, Name = "StepName", Optional = false)]
private void MethodName(ContextType ctx) { }
```

**Properties:**
- `order` (required): Execution order (ascending). Must be unique.
- `Name` (optional): Human-readable name for diagnostics
- `Optional` (optional): Mark step as optional for error handling

**Requirements:**
- Must return `void` or `ValueTask`
- Must accept at least one parameter (the context)
- Async steps should accept `CancellationToken` as second parameter

### Hooks

Hooks provide lifecycle extension points:

```csharp
[TemplateHook(HookPoint.BeforeAll)]
private void Setup(ImportContext ctx) { }

[TemplateHook(HookPoint.AfterAll)]
private void Cleanup(ImportContext ctx) { }

[TemplateHook(HookPoint.OnError)]
private void HandleError(ImportContext ctx, Exception ex) { }
```

**Hook Points:**
- `BeforeAll`: Invoked before any steps execute
- `AfterAll`: Invoked after all steps complete successfully
- `OnError`: Invoked when any step throws an exception

### Async Workflows

The generator automatically creates async methods when:
- Any step returns `ValueTask`
- Any step or hook accepts `CancellationToken`
- `GenerateAsync = true` is set on the `[Template]` attribute

```csharp
[Template(GenerateAsync = true)]
public partial class AsyncWorkflow
{
    [TemplateStep(0)]
    private async ValueTask LoadAsync(ImportContext ctx, CancellationToken ct)
    {
        await Task.Delay(100, ct);
    }

    [TemplateStep(1)]
    private void Process(ImportContext ctx)
    {
        // Synchronous step in async workflow
    }
}

// Usage
await workflow.ExecuteAsync(ctx, cancellationToken);
```

## Real-World Examples

### Data Import Pipeline

```csharp
public class ImportContext
{
    public string FilePath { get; set; } = "";
    public string[] RawData { get; set; } = Array.Empty<string>();
    public List<DataRecord> Records { get; set; } = new();
    public List<string> Log { get; set; } = new();
}

[Template]
public partial class ImportWorkflow
{
    [TemplateHook(HookPoint.BeforeAll)]
    private void OnStart(ImportContext ctx)
    {
        ctx.Log.Add($"Starting import from: {ctx.FilePath}");
    }

    [TemplateStep(0)]
    private void LoadData(ImportContext ctx)
    {
        ctx.RawData = File.ReadAllLines(ctx.FilePath);
        ctx.Log.Add($"Loaded {ctx.RawData.Length} lines");
    }

    [TemplateStep(1)]
    private void ValidateData(ImportContext ctx)
    {
        var invalidLines = ctx.RawData
            .Where(line => !line.Contains(":"))
            .ToList();
        
        if (invalidLines.Any())
        {
            throw new InvalidOperationException(
                $"Validation failed: {invalidLines.Count} invalid lines");
        }
        
        ctx.Log.Add("Validation passed");
    }

    [TemplateStep(2)]
    private void TransformData(ImportContext ctx)
    {
        foreach (var line in ctx.RawData)
        {
            var parts = line.Split(':');
            ctx.Records.Add(new DataRecord(parts[0], parts[1]));
        }
        
        ctx.Log.Add($"Transformed {ctx.Records.Count} records");
    }

    [TemplateStep(3)]
    private void PersistData(ImportContext ctx)
    {
        // Save to database
        ctx.Log.Add($"Persisted {ctx.Records.Count} records");
    }

    [TemplateHook(HookPoint.OnError)]
    private void OnError(ImportContext ctx, Exception ex)
    {
        ctx.Log.Add($"ERROR: {ex.Message}");
    }

    [TemplateHook(HookPoint.AfterAll)]
    private void OnComplete(ImportContext ctx)
    {
        ctx.Log.Add("Import completed successfully");
    }
}
```

### Async Order Processing

```csharp
public class OrderContext
{
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
    public bool PaymentAuthorized { get; set; }
    public bool InventoryReserved { get; set; }
}

[Template(GenerateAsync = true)]
public partial class OrderProcessingWorkflow
{
    [TemplateStep(0)]
    private async ValueTask AuthorizePaymentAsync(
        OrderContext ctx, CancellationToken ct)
    {
        // Call payment gateway
        await Task.Delay(100, ct);
        ctx.PaymentAuthorized = true;
    }

    [TemplateStep(1)]
    private async ValueTask ReserveInventoryAsync(
        OrderContext ctx, CancellationToken ct)
    {
        // Reserve inventory
        await Task.Delay(100, ct);
        ctx.InventoryReserved = true;
    }

    [TemplateStep(2)]
    private void ConfirmOrder(OrderContext ctx)
    {
        // Synchronous confirmation
        if (!ctx.PaymentAuthorized || !ctx.InventoryReserved)
        {
            throw new InvalidOperationException("Cannot confirm order");
        }
    }

    [TemplateHook(HookPoint.OnError)]
    private void HandleError(OrderContext ctx, Exception ex)
    {
        // Compensating actions
        if (ctx.InventoryReserved)
        {
            // Release inventory
        }
        if (ctx.PaymentAuthorized)
        {
            // Refund payment
        }
    }
}

// Usage
var ctx = new OrderContext { OrderId = "ORD-001", Amount = 99.99m };
var workflow = new OrderProcessingWorkflow();
await workflow.ExecuteAsync(ctx, cancellationToken);
```

## Configuration

### Template Attribute Options

```csharp
[Template(
    ExecuteMethodName = "Process",           // Default: "Execute"
    ExecuteAsyncMethodName = "ProcessAsync", // Default: "ExecuteAsync"
    GenerateAsync = true,                     // Force async generation
    ForceAsync = false,                       // Generate async even without async steps
    ErrorPolicy = TemplateErrorPolicy.Rethrow // Error handling policy
)]
public partial class CustomWorkflow { }
```

### Error Policies

**Rethrow (Default):**
- OnError hook is invoked
- Exception is rethrown
- Workflow terminates

**HandleAndContinue:**
- OnError hook is invoked
- Exception is suppressed (not rethrown)
- Workflow terminates
- Only allowed when all steps are optional

```csharp
[Template(ErrorPolicy = TemplateErrorPolicy.HandleAndContinue)]
public partial class ResilientWorkflow
{
    [TemplateStep(0, Optional = true)]
    private void Step1(Context ctx) { }  // Must be optional

    [TemplateStep(1, Optional = true)]
    private void Step2(Context ctx) { }  // Must be optional

    [TemplateStep(2, Optional = true)]
    private void Step3(Context ctx) { }  // Must be optional
}
```

## Diagnostics

The generator provides actionable diagnostics:

| Code | Message | Resolution |
|------|---------|------------|
| PKTMP001 | Type must be partial | Add `partial` keyword to type declaration |
| PKTMP002 | No template steps found | Add at least one `[TemplateStep]` method |
| PKTMP003 | Duplicate step order | Ensure each step has unique Order value |
| PKTMP004 | Invalid step signature | Step must return void/ValueTask and accept context |
| PKTMP005 | Invalid hook signature | Hook signature doesn't match requirements |
| PKTMP007 | Missing CancellationToken | Add CancellationToken parameter to async steps |
| PKTMP008 | HandleAndContinue policy invalid | Make all steps optional or use Rethrow policy |

## Supported Type Targets

The generator works with:
- `partial class`
- `partial struct`
- `partial record class`
- `partial record struct`

```csharp
[Template]
public partial class ClassWorkflow { }

[Template]
public partial struct StructWorkflow { }

[Template]
public partial record class RecordClassWorkflow { }

[Template]
public partial record struct RecordStructWorkflow { }
```

## Best Practices

### 1. Use Meaningful Context

Create a dedicated context class that carries all state:

```csharp
public class WorkflowContext
{
    public required string Input { get; init; }
    public List<string> Log { get; } = new();
    public Dictionary<string, object> Metadata { get; } = new();
}
```

### 2. Keep Steps Focused

Each step should have a single responsibility:

```csharp
[TemplateStep(0)]
private void ValidateInput(Context ctx) { /* Only validation */ }

[TemplateStep(1)]
private void TransformData(Context ctx) { /* Only transformation */ }
```

### 3. Use Hooks for Cross-Cutting Concerns

```csharp
[TemplateHook(HookPoint.BeforeAll)]
private void StartTimer(Context ctx)
{
    ctx.StartTime = DateTime.UtcNow;
}

[TemplateHook(HookPoint.AfterAll)]
private void LogDuration(Context ctx)
{
    var duration = DateTime.UtcNow - ctx.StartTime;
    ctx.Log.Add($"Duration: {duration.TotalMilliseconds}ms");
}
```

### 4. Leverage Error Hooks for Cleanup

```csharp
[TemplateHook(HookPoint.OnError)]
private void Cleanup(Context ctx, Exception ex)
{
    // Release resources
    // Log error details
    // Send notifications
}
```

## Migration from Runtime Template Method

If you're using a runtime base class:

**Before:**
```csharp
public class MyWorkflow : TemplateMethod<Context, Result>
{
    protected override void OnBefore(Context ctx) { }
    protected override Result Step(Context ctx) { }
    protected override void OnAfter(Context ctx, Result result) { }
}
```

**After:**
```csharp
[Template]
public partial class MyWorkflow
{
    [TemplateHook(HookPoint.BeforeAll)]
    private void OnBefore(Context ctx) { }

    [TemplateStep(0)]
    private void Step(Context ctx) { }

    [TemplateHook(HookPoint.AfterAll)]
    private void OnAfter(Context ctx) { }
}
```

**Benefits:**
- No inheritance required
- Multiple workflows per class
- Better testability
- Compile-time validation
- Zero runtime overhead

## See Also

- [API Reference](../api/PatternKit.Generators.Template.html)
- [Examples](examples.md)
- [Troubleshooting](troubleshooting.md)
