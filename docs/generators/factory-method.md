# Factory Method Generator

Convert a `static partial` class into a keyed factory using `[FactoryMethod]`, `[FactoryCase]`, and `[FactoryDefault]`. The generator produces `Create`, `TryCreate`, and async siblings when needed.

## Quick start

```csharp
using PatternKit.Generators.Factories;

namespace Demo;

[FactoryMethod(typeof(string), CreateMethodName = "Make")]
public static partial class MimeFactory
{
    [FactoryCase("json")]
    public static string Json() => "application/json";

    [FactoryDefault]
    public static string Default() => "application/octet-stream";
}
```

Generated API (sync):

```csharp
public static string Make(string key);
public static bool TryCreate(string key, out string value);
```

Async is generated when any case/default returns `ValueTask<T>` or `Task<T>`:

```csharp
public static ValueTask<string> MakeAsync(string key);
public static ValueTask<(bool Success, string Result)> TryCreateAsync(string key);
```

## Behavior

- **Key matching**  
  - `KeyType == string` and `CaseInsensitiveStrings == true` (default) → `String.Equals` with `OrdinalIgnoreCase`.  
  - Otherwise exact match (`Ordinal` for strings).
- **Default path**  
  - If `[FactoryDefault]` exists, it is used when no case matches.  
  - Otherwise `Create` throws `ArgumentOutOfRangeException`; `TryCreate` returns `false` and `default`.
- **Parameters**  
  - All `[FactoryCase]`/`[FactoryDefault]` methods must be `static` with identical signatures (return + parameters). Parameters are forwarded into generated methods.
- **Async rules**  
  - If any mapping is async, generated async APIs use `ValueTask<T>`.  
  - `Task<T>` cases are wrapped in `ValueTask<T>`.  
  - Synchronous cases are wrapped with `ValueTask.FromResult`.

## Diagnostics

| Id | Message | Fix |
| --- | --- | --- |
| PKKF001 | `[FactoryMethod]` type must be static partial | Mark the class `static partial`. |
| PKKF002 | Factory methods must share the same signature | Align return type and parameters across all cases/default. |
| PKKF003 | Duplicate factory key | Remove or rename the duplicate key. |
| PKKF004 | Multiple default factory methods | Keep only one `[FactoryDefault]`. |
| PKKF005 | Invalid factory key value | Ensure the key literal converts implicitly to `KeyType`. |
| PKKF006 | Factory methods must be static | Mark all `[FactoryCase]/[FactoryDefault]` methods `static`. |

## Tips

- Prefer small, pure mapping methods; let the generated factory handle validation and branching.
- For string keys that must be case-sensitive, set `CaseInsensitiveStrings = false` and test both `Create` and `TryCreate`.
- Use `CreateMethodName` to match existing APIs (`Make`, `FromKey`, etc.).
