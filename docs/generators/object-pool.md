# Object Pool Generator

`GenerateObjectPoolAttribute` creates a static factory for `PatternKit.Creational.ObjectPool.ObjectPool<T>`.

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `itemType` | `Type` | Required | Pooled item type. It must be a value type or expose a public parameterless constructor. |
| `FactoryMethodName` | `string` | `Create` | Generated static factory method name. |
| `MaxRetained` | `int` | `-1` | When set to `0` or greater, emits `WithMaxRetained(value)`. |
| `ResetMethodName` | `string?` | `null` | Optional instance method invoked from `OnReturn`. |

```csharp
[GenerateObjectPool(typeof(FormulaEvaluationBuffer), FactoryMethodName = "CreateGenerated", MaxRetained = 16, ResetMethodName = nameof(FormulaEvaluationBuffer.Reset))]
public static partial class SpreadsheetFormulaBufferPools;
```

Diagnostics:

| ID | Severity | Description |
| --- | --- | --- |
| `PKOP001` | Error | The host type is not partial. |
| `PKOP002` | Error | The factory method name is blank or `MaxRetained` is below `-1`. |
| `PKOP003` | Error | The pooled item type cannot be created with `new T()`. |
