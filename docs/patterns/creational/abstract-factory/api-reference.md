# Abstract Factory Pattern API Reference

Complete API documentation for the Abstract Factory pattern in PatternKit.

## Namespace

```csharp
using PatternKit.Creational.AbstractFactory;
```

---

## AbstractFactory\<TKey\>

Creates families of related objects by key.

```csharp
public sealed class AbstractFactory<TKey> where TKey : notnull
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TKey` | The type of key used to identify product families |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetFamily(TKey key)` | `ProductFamily` | Gets a product family by key |
| `TryGetFamily(TKey key, out ProductFamily family)` | `bool` | Attempts to get a family; returns false if not found |
| `HasFamily(TKey key)` | `bool` | Checks if a family is registered for the key |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `FamilyKeys` | `IEnumerable<TKey>` | All registered family keys |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |
| `Create(IEqualityComparer<TKey> comparer)` | `Builder` | Creates a builder with custom key comparer |

### Exceptions

| Method | Exception | Condition |
|--------|-----------|-----------|
| `GetFamily` | `KeyNotFoundException` | Key not registered and no default exists |

### Example

```csharp
var factory = AbstractFactory<string>.Create()
    .Family("light")
        .Product<IButton>(() => new LightButton())
        .Product<ITextBox>(() => new LightTextBox())
    .Family("dark")
        .Product<IButton>(() => new DarkButton())
        .Product<ITextBox>(() => new DarkTextBox())
    .Build();

// Get family and create products
var lightFamily = factory.GetFamily("light");
var button = lightFamily.Create<IButton>();

// Safe access
if (factory.TryGetFamily("custom", out var custom))
{
    var textBox = custom.Create<ITextBox>();
}

// Check availability
bool hasDark = factory.HasFamily("dark"); // true
```

---

## AbstractFactory\<TKey\>.Builder

Fluent builder for configuring product families.

```csharp
public sealed class Builder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Family(TKey key)` | `Builder` | Starts or switches to a product family |
| `Product<TProduct>(Func<TProduct> creator)` | `Builder` | Registers a product creator for current family |
| `DefaultFamily()` | `Builder` | Starts the default family definition |
| `DefaultProduct<TProduct>(Func<TProduct> creator)` | `Builder` | Registers a product creator for default family |
| `Build()` | `AbstractFactory<TKey>` | Builds the immutable factory |

### Exceptions

| Method | Exception | Condition |
|--------|-----------|-----------|
| `Product<TProduct>` | `InvalidOperationException` | Called before `Family()` |

### Example

```csharp
var factory = AbstractFactory<Theme>.Create()
    // First family
    .Family(Theme.Light)
        .Product<IButton>(() => new LightButton())
        .Product<IDialog>(() => new LightDialog())

    // Second family
    .Family(Theme.Dark)
        .Product<IButton>(() => new DarkButton())
        .Product<IDialog>(() => new DarkDialog())

    // Default for unknown keys
    .DefaultFamily()
        .DefaultProduct<IButton>(() => new DefaultButton())
        .DefaultProduct<IDialog>(() => new DefaultDialog())

    .Build();
```

---

## AbstractFactory\<TKey\>.ProductFamily

Represents a family of related products.

```csharp
public sealed class ProductFamily
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create<TProduct>()` | `TProduct` | Creates a product of the specified type |
| `TryCreate<TProduct>(out TProduct product)` | `bool` | Attempts to create a product; returns false if not registered |
| `CanCreate<TProduct>()` | `bool` | Checks if a product type is registered |

### Exceptions

| Method | Exception | Condition |
|--------|-----------|-----------|
| `Create<TProduct>` | `InvalidOperationException` | Product type not registered in family |

### Example

```csharp
var family = factory.GetFamily("light");

// Direct creation (throws if not registered)
var button = family.Create<IButton>();

// Safe creation
if (family.TryCreate<IAdvancedButton>(out var advanced))
{
    advanced.DoAdvancedThing();
}

// Capability check
if (family.CanCreate<IDialog>())
{
    var dialog = family.Create<IDialog>();
    dialog.Show("Title", "Message");
}
```

---

## Thread Safety

| Component | Thread-Safe |
|-----------|-------------|
| `Builder` | No - use from single thread during configuration |
| `AbstractFactory<TKey>` | Yes - immutable after build |
| `ProductFamily` | Yes - immutable after build |

---

## Type Constraints

| Constraint | Location | Purpose |
|------------|----------|---------|
| `TKey : notnull` | `AbstractFactory<TKey>` | Keys must not be null |

---

## Complete Example

```csharp
using PatternKit.Creational.AbstractFactory;

// Product interfaces
public interface IButton { string Render(); void Click(); }
public interface ITextBox { string Render(); string Text { get; set; } }
public interface ICheckBox { string Render(); bool IsChecked { get; set; } }

// Platform enum
public enum Platform { Windows, MacOS, Linux }

// Create the factory
var uiFactory = AbstractFactory<Platform>.Create()
    .Family(Platform.Windows)
        .Product<IButton>(() => new WindowsButton())
        .Product<ITextBox>(() => new WindowsTextBox())
        .Product<ICheckBox>(() => new WindowsCheckBox())

    .Family(Platform.MacOS)
        .Product<IButton>(() => new MacButton())
        .Product<ITextBox>(() => new MacTextBox())
        .Product<ICheckBox>(() => new MacCheckBox())

    .Family(Platform.Linux)
        .Product<IButton>(() => new LinuxButton())
        .Product<ITextBox>(() => new LinuxTextBox())
        .Product<ICheckBox>(() => new LinuxCheckBox())

    .Build();

// Usage: Platform-agnostic UI construction
void CreateLoginForm(Platform platform)
{
    var widgets = uiFactory.GetFamily(platform);

    var usernameBox = widgets.Create<ITextBox>();
    var passwordBox = widgets.Create<ITextBox>();
    var rememberMe = widgets.Create<ICheckBox>();
    var loginButton = widgets.Create<IButton>();

    usernameBox.Text = "Enter username";
    passwordBox.Text = "";
    rememberMe.IsChecked = false;

    Console.WriteLine(usernameBox.Render());
    Console.WriteLine(passwordBox.Render());
    Console.WriteLine(rememberMe.Render());
    Console.WriteLine(loginButton.Render());
}

// Enumerate all families
foreach (var key in uiFactory.FamilyKeys)
{
    Console.WriteLine($"Platform: {key}");
    var family = uiFactory.GetFamily(key);
    Console.WriteLine($"  Has Button: {family.CanCreate<IButton>()}");
    Console.WriteLine($"  Has TextBox: {family.CanCreate<ITextBox>()}");
}
```

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [Real-World Examples](real-world-examples.md)
