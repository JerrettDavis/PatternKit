# Abstract Factory Widget Families

This example demonstrates a generated Abstract Factory for platform-specific UI widget families. The runtime path is still `AbstractFactory<Platform>`; the generated path removes repetitive family registration code.

Source:

- `src/PatternKit.Examples/AbstractFactoryDemo/AbstractFactoryDemo.cs`
- `test/PatternKit.Examples.Tests/AbstractFactoryDemo/AbstractFactoryDemoTests.cs`

## Generated Family Matrix

The example declares the family matrix once:

```csharp
[GenerateAbstractFactory(typeof(AbstractFactoryDemo.Platform), FactoryMethodName = "Create", ServiceProviderFactoryMethodName = "CreateFromServices")]
[AbstractFactoryProduct(AbstractFactoryDemo.Platform.Windows, typeof(AbstractFactoryDemo.IButton), typeof(AbstractFactoryDemo.WindowsButton))]
[AbstractFactoryProduct(AbstractFactoryDemo.Platform.Windows, typeof(AbstractFactoryDemo.ITextBox), typeof(AbstractFactoryDemo.WindowsTextBox))]
[AbstractFactoryProduct(AbstractFactoryDemo.Platform.Linux, typeof(AbstractFactoryDemo.IButton), typeof(AbstractFactoryDemo.LinuxButton))]
[AbstractFactoryProduct(AbstractFactoryDemo.Platform.Linux, typeof(AbstractFactoryDemo.ITextBox), typeof(AbstractFactoryDemo.LinuxTextBox))]
public static partial class GeneratedPlatformWidgetFactory;
```

`CreateUIFactory()` calls the generated factory and returns the same runtime abstraction consumers already use:

```csharp
var factory = AbstractFactoryDemo.CreateUIFactory();
var windows = factory.GetFamily(AbstractFactoryDemo.Platform.Windows);
var button = windows.Create<AbstractFactoryDemo.IButton>();
```

## IServiceCollection Import

The example also exposes an importable DI path:

```csharp
services.AddAbstractFactoryWidgetExample();
```

The registration uses the generated `CreateFromServices(IServiceProvider)` overload so concrete widget products can evolve toward constructor-injected dependencies without changing client code.

## Tested Behavior

The TinyBDD coverage validates that every platform family can create every widget contract, product behavior remains platform-specific, and the DI registration resolves an `AbstractFactory<Platform>` that importing applications can use directly.
