# Abstract Factory Pattern Guide

This guide covers everything you need to know about using the Abstract Factory pattern in PatternKit.

## Overview

Abstract Factory creates families of related objects without specifying their concrete classes. When you have multiple product types that need to work together (like UI widgets in a theme), Abstract Factory ensures you get a consistent set of compatible products.

## Getting Started

### Installation

The Abstract Factory pattern is included in the core PatternKit package:

```csharp
using PatternKit.Creational.AbstractFactory;
```

### Basic Usage

Create a factory in three steps:

```csharp
// 1. Define product interfaces
public interface IButton { string Render(); }
public interface ITextBox { string Render(); }

// Concrete products for "light" family
public class LightButton : IButton { public string Render() => "[Light]"; }
public class LightTextBox : ITextBox { public string Render() => "|Light|"; }

// Concrete products for "dark" family
public class DarkButton : IButton { public string Render() => "[Dark]"; }
public class DarkTextBox : ITextBox { public string Render() => "|Dark|"; }

// 2. Create the factory with product families
var factory = AbstractFactory<string>.Create()
    .Family("light")
        .Product<IButton>(() => new LightButton())
        .Product<ITextBox>(() => new LightTextBox())
    .Family("dark")
        .Product<IButton>(() => new DarkButton())
        .Product<ITextBox>(() => new DarkTextBox())
    .Build();

// 3. Get a family and create products
var lightFamily = factory.GetFamily("light");
var button = lightFamily.Create<IButton>();   // LightButton
var textBox = lightFamily.Create<ITextBox>(); // LightTextBox
```

## Core Concepts

### Product Families

A product family is a set of related products designed to work together. Each family is identified by a key and contains creators for different product types:

```csharp
var factory = AbstractFactory<Platform>.Create()
    .Family(Platform.Windows)
        .Product<IButton>(() => new WindowsButton())
        .Product<IDialog>(() => new WindowsDialog())
        .Product<IMenu>(() => new WindowsMenu())
    .Family(Platform.MacOS)
        .Product<IButton>(() => new MacButton())
        .Product<IDialog>(() => new MacDialog())
        .Product<IMenu>(() => new MacMenu())
    .Build();
```

### Product Interfaces

Products are defined by interfaces. This allows the factory to return different implementations based on the selected family:

```csharp
public interface IButton
{
    string Render();
    void Click();
}

public interface IDialog
{
    void Show(string title, string message);
    DialogResult ShowModal();
}
```

### Family Selection

Families are selected at runtime by key:

```csharp
// Get family by enum key
var family = factory.GetFamily(Platform.Windows);

// Or by string key
var themeFactory = AbstractFactory<string>.Create()
    .Family("light")
        .Product<IButton>(() => new LightButton())
    .Build();

var lightFamily = themeFactory.GetFamily("light");
```

### Default Family

Define a default family for unknown keys:

```csharp
var factory = AbstractFactory<string>.Create()
    .Family("light")
        .Product<IButton>(() => new LightButton())
    .Family("dark")
        .Product<IButton>(() => new DarkButton())
    .DefaultFamily()
        .DefaultProduct<IButton>(() => new FallbackButton())
    .Build();

// Uses default family for unknown key
var family = factory.GetFamily("unknown"); // Returns default family
var button = family.Create<IButton>();     // FallbackButton
```

## Safe Access Patterns

### TryGetFamily

Safely check if a family exists:

```csharp
if (factory.TryGetFamily("custom", out var family))
{
    var button = family.Create<IButton>();
    button.Render();
}
else
{
    Console.WriteLine("Custom family not available");
}
```

### TryCreate

Safely check if a product type is available:

```csharp
var family = factory.GetFamily("light");

if (family.TryCreate<IAdvancedButton>(out var advanced))
{
    advanced.DoAdvancedStuff();
}
else
{
    // Fall back to basic button
    var basic = family.Create<IButton>();
    basic.Click();
}
```

### Capability Checking

Check what a family can create:

```csharp
var family = factory.GetFamily("light");

Console.WriteLine($"Can create IButton: {family.CanCreate<IButton>()}");
Console.WriteLine($"Can create IDialog: {family.CanCreate<IDialog>()}");
Console.WriteLine($"Can create IMenu: {family.CanCreate<IMenu>()}");
```

## Custom Key Comparers

Use custom comparers for key matching:

```csharp
// Case-insensitive string keys
var factory = AbstractFactory<string>.Create(StringComparer.OrdinalIgnoreCase)
    .Family("LIGHT")
        .Product<IButton>(() => new LightButton())
    .Build();

// Matches despite case difference
var family = factory.GetFamily("light");
```

## Common Patterns

### Platform-Specific UI

```csharp
public enum Platform { Windows, MacOS, Linux }

var uiFactory = AbstractFactory<Platform>.Create()
    .Family(Platform.Windows)
        .Product<IButton>(() => new WindowsButton())
        .Product<ITextBox>(() => new WindowsTextBox())
        .Product<IMenu>(() => new WindowsMenu())
    .Family(Platform.MacOS)
        .Product<IButton>(() => new MacButton())
        .Product<ITextBox>(() => new MacTextBox())
        .Product<IMenu>(() => new MacMenu())
    .Family(Platform.Linux)
        .Product<IButton>(() => new LinuxButton())
        .Product<ITextBox>(() => new LinuxTextBox())
        .Product<IMenu>(() => new LinuxMenu())
    .Build();

// Detect platform at runtime
var platform = DetectPlatform();
var widgets = uiFactory.GetFamily(platform);

// Create platform-appropriate widgets
var button = widgets.Create<IButton>();
var textBox = widgets.Create<ITextBox>();
```

### Theme Systems

```csharp
public enum Theme { Light, Dark, HighContrast }

var themeFactory = AbstractFactory<Theme>.Create()
    .Family(Theme.Light)
        .Product<IColorPalette>(() => new LightPalette())
        .Product<IIconSet>(() => new LightIcons())
    .Family(Theme.Dark)
        .Product<IColorPalette>(() => new DarkPalette())
        .Product<IIconSet>(() => new DarkIcons())
    .Family(Theme.HighContrast)
        .Product<IColorPalette>(() => new HighContrastPalette())
        .Product<IIconSet>(() => new HighContrastIcons())
    .Build();

// Apply theme throughout application
void ApplyTheme(Theme theme)
{
    var themeFamily = themeFactory.GetFamily(theme);
    var palette = themeFamily.Create<IColorPalette>();
    var icons = themeFamily.Create<IIconSet>();

    // Apply to UI...
}
```

### Database Provider Families

```csharp
public enum DatabaseProvider { SqlServer, PostgreSQL, SQLite }

var dbFactory = AbstractFactory<DatabaseProvider>.Create()
    .Family(DatabaseProvider.SqlServer)
        .Product<IConnection>(() => new SqlServerConnection())
        .Product<ICommand>(() => new SqlServerCommand())
        .Product<IParameter>(() => new SqlServerParameter())
    .Family(DatabaseProvider.PostgreSQL)
        .Product<IConnection>(() => new PostgreSqlConnection())
        .Product<ICommand>(() => new PostgreSqlCommand())
        .Product<IParameter>(() => new PostgreSqlParameter())
    .Family(DatabaseProvider.SQLite)
        .Product<IConnection>(() => new SQLiteConnection())
        .Product<ICommand>(() => new SQLiteCommand())
        .Product<IParameter>(() => new SQLiteParameter())
    .Build();

// Database-agnostic data access
void ExecuteQuery(DatabaseProvider provider, string sql)
{
    var db = dbFactory.GetFamily(provider);

    using var connection = db.Create<IConnection>();
    using var command = db.Create<ICommand>();

    command.Connection = connection;
    command.CommandText = sql;
    connection.Open();
    command.ExecuteNonQuery();
}
```

## Combining with Other Patterns

### With Strategy

Use Strategy to select which family to use:

```csharp
var familySelector = Strategy<UserPreferences, Theme>.Create()
    .When(p => p.UseSystemTheme && IsSystemDarkMode())
        .Then(Theme.Dark)
    .When(p => p.UseHighContrast)
        .Then(Theme.HighContrast)
    .Default(Theme.Light)
    .Build();

var theme = familySelector.Execute(userPrefs);
var widgets = themeFactory.GetFamily(theme);
```

### With Decorator

Decorate products after creation:

```csharp
var family = factory.GetFamily("production");
var button = family.Create<IButton>();

// Wrap with logging decorator
var loggingButton = Decorator<IButton>.Create(button)
    .Before((b, args) => Log.Debug("Button clicked"))
    .Build();
```

### With Factory

Use Factory to create the Abstract Factory itself:

```csharp
var factoryFactory = Factory<string, AbstractFactory<Theme>>.Create()
    .Map("desktop", () => CreateDesktopFactory())
    .Map("mobile", () => CreateMobileFactory())
    .Map("web", () => CreateWebFactory())
    .Build();

var uiFactory = factoryFactory.Create(platform);
```

## Best Practices

1. **Design product interfaces carefully**: They define the contract all families must fulfill

2. **Keep families consistent**: Each family should provide the same product types

3. **Use meaningful keys**: Enum keys are more type-safe than strings

4. **Provide defaults**: Use `DefaultFamily()` for graceful fallback

5. **Cache the factory**: Build once, reuse throughout the application

6. **Inject the factory**: Pass the factory to consuming code for testability

## Troubleshooting

### "No product family registered for key"

Family key doesn't exist and no default:

```csharp
// Add a default family
.DefaultFamily()
    .DefaultProduct<IButton>(() => new FallbackButton())
```

### "No creator registered for product type"

Product type not registered in the family:

```csharp
// Ensure all families have the same products
.Family("light")
    .Product<IButton>(...)
    .Product<IDialog>(...) // Add missing product
```

### Products from different families don't work together

This is expected behavior - products from different families aren't designed to interact.

## FAQ

**Q: Can I add families after building?**
A: No. Factories are immutable after `Build()`. Create a new factory if needed.

**Q: Can families have different product types?**
A: Yes, but be careful. Use `CanCreate<T>()` or `TryCreate<T>()` to handle missing products.

**Q: How does this differ from Dependency Injection?**
A: Abstract Factory creates objects directly; DI resolves dependencies. They can work together - inject the factory, then use it to create products.

**Q: What's the performance overhead?**
A: Minimal. Family lookup is dictionary-based O(1). Product creation invokes the registered factory function.
