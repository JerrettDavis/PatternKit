# Factory Class Generator

Generate a GoF-style factory class from an abstract base or interface using `[FactoryClass]` on the base and `[FactoryClassKey]` on concrete implementations.

## Quick start

```csharp
using PatternKit.Generators.Factories;

namespace Demo;

[FactoryClass(typeof(string), FactoryTypeName = "NotificationFactory")]
public interface INotification { }

[FactoryClassKey("email")]
public class Email : INotification { }

[FactoryClassKey("sms")]
public class Sms : INotification { }
```

Generated factory (sync):

```csharp
public sealed partial class NotificationFactory
{
    public INotification Create(string key);
    public bool TryCreate(string key, out INotification result); // unless GenerateTryCreate = false
}
```

Async is generated when any product exposes `public static CreateAsync()` returning `Task<TBase>` or `ValueTask<TBase>`:

```csharp
public ValueTask<INotification> CreateAsync(string key);
public ValueTask<(bool Success, INotification Result)> TryCreateAsync(string key);
```

## Enum keys

Set `GenerateEnumKeys = true` to emit:

- Nested `enum Keys` containing all registered keys (sanitized identifiers).
- Overloads of `Create`/`TryCreate`/`CreateAsync`/`TryCreateAsync` that accept the enum.

## Behavior

- **Base requirement** — `[FactoryClass]` must decorate an interface or abstract class.
- **Product discovery** — `[FactoryClassKey]` types must be concrete and implement/derive exactly one `[FactoryClass]` base.
- **Construction** — Uses public parameterless ctor when present; otherwise looks for `static CreateAsync()` on the product. Async factories are awaited; sync products are wrapped in `ValueTask.FromResult` for async APIs.
- **Key handling** — Keys must convert implicitly to `KeyType`. Duplicate keys per base are rejected.
- **Defaults** — No default branch; unknown keys throw in `Create` and return `false` in `TryCreate`.

## Diagnostics

| Id | Message | Fix |
| --- | --- | --- |
| PKCF001 | `[FactoryClass]` must be interface or abstract class | Mark the base `interface` or `abstract class`. |
| PKCF002 | `[FactoryClassKey]` type maps to multiple bases | Ensure each product implements only one `[FactoryClass]` base. |
| PKCF003 | `[FactoryClassKey]` type must be concrete | Remove `abstract` and supply an accessible ctor. |
| PKCF004 | Duplicate factory key | Remove duplicate keys per base type. |
| PKCF005 | Invalid factory key value | Ensure the key literal converts implicitly to `KeyType`. |
| PKCF006 | Missing accessible constructor | Add public parameterless ctor or `static CreateAsync()` returning `Task/ValueTask<TBase>`. |

## Tips

- Use `FactoryTypeName` to control the emitted factory name; otherwise `{BaseName}Factory` is used.
- When you need enum-friendly calling code (e.g., switches), enable `GenerateEnumKeys` for typed overloads.
- Prefer `ValueTask<TBase>` for `CreateAsync` on products; `Task<TBase>` is wrapped when present.
