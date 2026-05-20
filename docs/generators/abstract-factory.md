# Abstract Factory Generator

`[GenerateAbstractFactory]` emits an `AbstractFactory<TKey>` from declarative product-family attributes. Use it when the family matrix is known at compile time and should stay reviewable, type-safe, and importable through normal application composition.

## Quick Start

```csharp
using PatternKit.Generators.Factories;

[GenerateAbstractFactory(typeof(Platform), FactoryMethodName = "Create", ServiceProviderFactoryMethodName = "CreateFromServices")]
[AbstractFactoryProduct(Platform.Windows, typeof(IButton), typeof(WindowsButton))]
[AbstractFactoryProduct(Platform.Windows, typeof(ITextBox), typeof(WindowsTextBox))]
[AbstractFactoryProduct(Platform.Linux, typeof(IButton), typeof(LinuxButton))]
[AbstractFactoryProduct(Platform.Linux, typeof(ITextBox), typeof(LinuxTextBox))]
public static partial class PlatformWidgetFactory;
```

Generated API:

```csharp
public static AbstractFactory<Platform> Create();
public static AbstractFactory<Platform> CreateFromServices(IServiceProvider services);
```

The normal factory path uses public parameterless constructors. The optional service-provider path uses `ActivatorUtilities.CreateInstance<T>(services)`, so products can add constructor dependencies when the importing app registers them with `IServiceCollection`.

## Behavior

- `[GenerateAbstractFactory]` must be placed on a partial class or struct.
- `[AbstractFactoryProduct]` declares one contract/implementation pair for one family key.
- `ImplementationType` must be a concrete class assignable to `ContractType`.
- Each generated factory calls the runtime fluent API: `AbstractFactory<TKey>.Create().Family(...).Product<...>().Build()`.
- Set `IsDefaultFamily = true` on a product to add it to the default family instead of a keyed family.

## Diagnostics

| ID | Meaning |
| --- | --- |
| `PKAF001` | The generated host type is not partial. |
| `PKAF002` | No products were declared. |
| `PKAF003` | A product declaration has an invalid key, contract, implementation, or constructor. |
| `PKAF004` | A family declares the same product contract more than once. |

## Example Source

- `src/PatternKit.Examples/AbstractFactoryDemo/AbstractFactoryDemo.cs`
- `test/PatternKit.Examples.Tests/AbstractFactoryDemo/AbstractFactoryDemoTests.cs`
- `test/PatternKit.Generators.Tests/AbstractFactoryGeneratorTests.cs`
