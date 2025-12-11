# Generator Troubleshooting

Common issues and how to resolve them when using the PatternKit factory generators.

## Build errors

- **PKKF001 / static partial**  
  Make the `[FactoryMethod]` type `public static partial class Foo { }`.

- **PKKF002 / signature mismatch**  
  Align return type and parameter list across all `[FactoryCase]` and `[FactoryDefault]` methods.

- **PKKF003 / duplicate key** or **PKCF004**  
  Ensure each key literal is unique per factory/base.

- **PKKF005 / PKCF005 invalid key value**  
  The key must convert implicitly to `KeyType`. Fix the literal or adjust `KeyType`.

- **PKKF006 / PKCF003 / PKCF006 static/ctor checks**  
  Mark factory methods `static`; add a public parameterless constructor or a `static CreateAsync()` on products.

## Async behavior surprises

- Async factory methods always surface as `ValueTask<T>` in generated APIs. If your implementation returns `Task<T>`, it is wrapped.
- Sync factory methods are awaited in async paths using `ValueTask.FromResult`; long-running work should be async to avoid blocking.

## Enum key generation

- `GenerateEnumKeys = true` creates a nested `Keys` enum and overloads that accept it. Identifier names are sanitized; if collisions occur (e.g., identical sanitized keys), adjust key values to be distinct.

## How to inspect generated code

- Build the project and look for `*.FactoryMethod.g.cs` or `*.FactoryClass.g.cs` in the `obj/` folder. These are readable partial classes—helpful when debugging behavior.

## Still stuck?

- Verify the analyzer package is referenced in the project being compiled (not only in a shared props file).
- Clean the solution (`dotnet clean`) and rebuild to force regeneration.
- Capture the exact diagnostic ID and message; these map directly to the tables on the generator pages.
