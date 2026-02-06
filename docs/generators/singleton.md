# Singleton Generator

## Overview

The **Singleton Generator** creates thread-safe singleton implementations with explicit initialization and threading semantics. It eliminates common singleton footguns like incorrect lazy initialization and double-checked locking bugs.

## When to Use

Use the Singleton generator when you need:

- **A single instance** of a class throughout the application lifecycle
- **Explicit initialization timing**: Control whether the instance is created eagerly or lazily
- **Thread-safety guarantees**: Configurable threading model for your use case
- **Compile-time safety**: The generator validates your singleton at build time

## Installation

The generator is included in the `PatternKit.Generators` package:

```bash
dotnet add package PatternKit.Generators
```

## Quick Start

```csharp
using PatternKit.Generators.Singleton;

[Singleton]
public partial class AppClock
{
    public DateTime Now => DateTime.UtcNow;
    
    private AppClock() { }
}
```

Generated:
```csharp
public partial class AppClock
{
    private static readonly AppClock __PatternKit_Instance = new AppClock();
    
    /// <summary>Gets the singleton instance of this type.</summary>
    public static AppClock Instance => __PatternKit_Instance;
}
```

Usage:
```csharp
var now = AppClock.Instance.Now;
```

## Initialization Modes

### Eager Initialization (Default)

The instance is created when the type is first accessed. This is the simplest and safest approach:

```csharp
[Singleton] // Mode defaults to SingletonMode.Eager
public partial class Configuration
{
    public string ConnectionString { get; }
    
    private Configuration() 
    {
        ConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "";
    }
}
```

Generated:
```csharp
private static readonly Configuration __PatternKit_Instance = new Configuration();
public static Configuration Instance => __PatternKit_Instance;
```

**Pros:**
- Simple and thread-safe by CLR guarantee
- No runtime overhead on access
- Deterministic initialization

**Cons:**
- Instance created even if never accessed
- Initialization order depends on type access order

### Lazy Initialization

The instance is created on first access to the `Instance` property:

```csharp
[Singleton(Mode = SingletonMode.Lazy)]
public partial class ExpensiveService
{
    private ExpensiveService() 
    {
        // Expensive initialization here
    }
}
```

Generated (thread-safe):
```csharp
private static readonly Lazy<ExpensiveService> __PatternKit_LazyInstance =
    new Lazy<ExpensiveService>(() => new ExpensiveService());

public static ExpensiveService Instance => __PatternKit_LazyInstance.Value;
```

**Pros:**
- Instance only created when actually needed
- Can reduce startup time for rarely-used services

**Cons:**
- Slight runtime overhead on first access
- Less predictable initialization timing

## Threading Options

When using `SingletonMode.Lazy`, you can configure the threading model:

### ThreadSafe (Default)

Uses `Lazy<T>` which is thread-safe by default:

```csharp
[Singleton(Mode = SingletonMode.Lazy, Threading = SingletonThreading.ThreadSafe)]
public partial class SafeCache { }
```

### SingleThreadedFast

For scenarios where you guarantee single-threaded access:

```csharp
[Singleton(Mode = SingletonMode.Lazy, Threading = SingletonThreading.SingleThreadedFast)]
public partial class UiService
{
    // Only accessed from UI thread
    private UiService() { }
}
```

Generated:
```csharp
private static UiService? __PatternKit_Instance;

/// <summary>
/// Gets the singleton instance of this type.
/// WARNING: This implementation is not thread-safe.
/// </summary>
public static UiService Instance => __PatternKit_Instance ??= new UiService();
```

⚠️ **Warning:** Only use `SingleThreadedFast` when you can guarantee single-threaded access. Multi-threaded access may result in multiple instances being created.

## Custom Factory Methods

When your singleton needs custom initialization logic, use `[SingletonFactory]`:

```csharp
[Singleton(Mode = SingletonMode.Lazy)]
public partial class ConfigManager
{
    public string ConfigPath { get; }
    
    private ConfigManager(string path)
    {
        ConfigPath = path;
    }

    [SingletonFactory]
    private static ConfigManager Create()
    {
        var path = Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "config.json";
        return new ConfigManager(path);
    }
}
```

Factory method requirements:
- Must be `static`
- Must be parameterless
- Must return the containing type
- Only one factory method per type

## Attributes

### `[Singleton]`

Marks a partial class for singleton generation.

| Property | Type | Default | Description |
|---|---|---|---|
| `Mode` | `SingletonMode` | `Eager` | When the instance is created |
| `Threading` | `SingletonThreading` | `ThreadSafe` | Threading model (for Lazy mode) |
| `InstancePropertyName` | `string` | `"Instance"` | Name of the generated property |

### `[SingletonFactory]`

Marks a static method as the factory for creating the singleton instance.

```csharp
[SingletonFactory]
private static MyService Create() => new MyService();
```

## Supported Types

The generator supports:

| Type | Supported |
|---|---|
| `partial class` | ✅ |
| `partial record class` | ✅ |
| `struct` | ❌ (singleton value semantics are odd) |
| `interface` | ❌ |

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| **PKSNG001** | Error | Type marked with `[Singleton]` must be `partial` |
| **PKSNG002** | Error | Singleton type must be a class (not struct/interface) |
| **PKSNG003** | Error | No usable constructor or `[SingletonFactory]` found |
| **PKSNG004** | Error | Multiple `[SingletonFactory]` methods found |
| **PKSNG005** | Warning | Public constructor detected; singleton can be bypassed |
| **PKSNG006** | Error | Instance property name conflicts with existing member |
| **PKSNG007** | Error | Generic types are not supported for singleton generation |
| **PKSNG008** | Error | Nested types are not supported for singleton generation |
| **PKSNG009** | Error | Invalid instance property name (not a valid C# identifier) |
| **PKSNG010** | Error | Abstract types not supported (unless `[SingletonFactory]` provided) |

## Best Practices

### 1. Make Constructors Private

Always make your constructor private to prevent bypassing the singleton:

```csharp
// ✅ Good: Private constructor
[Singleton]
public partial class Logger
{
    private Logger() { }
}

// ⚠️ Bad: Public constructor (generates PKSNG005 warning)
[Singleton]
public partial class Logger
{
    public Logger() { }  // Anyone can create instances!
}
```

### 2. Prefer Eager Initialization

Unless you have a specific reason for lazy initialization, prefer eager mode:

```csharp
// ✅ Preferred: Simple, predictable, no overhead
[Singleton]
public partial class AppConfig { }

// Only use lazy when needed
[Singleton(Mode = SingletonMode.Lazy)]
public partial class ExpensiveToCreate { }
```

### 3. Avoid Mutable State

Singletons with mutable state can cause subtle bugs:

```csharp
// ⚠️ Caution: Mutable singleton state
[Singleton]
public partial class Counter
{
    public int Value { get; set; } // Shared mutable state
}

// ✅ Better: Immutable or thread-safe state
[Singleton]
public partial class Counter
{
    private int _value;
    public int Value => _value;
    public int Increment() => Interlocked.Increment(ref _value);
}
```

### 4. Use Custom Property Names When Needed

Avoid conflicts with existing members:

```csharp
[Singleton(InstancePropertyName = "Default")]
public partial class Settings
{
    // Can't use "Instance" if it already exists
    public int Instance { get; set; }
}
```

## Examples

### Configuration Manager

```csharp
using PatternKit.Generators.Singleton;

[Singleton(Mode = SingletonMode.Lazy)]
public partial class ConfigManager
{
    public string AppName { get; }
    public string Environment { get; }
    public string ConnectionString { get; }

    private ConfigManager(string appName, string env, string connStr)
    {
        AppName = appName;
        Environment = env;
        ConnectionString = connStr;
    }

    [SingletonFactory]
    private static ConfigManager Create()
    {
        // Load from environment or config file
        return new ConfigManager(
            Environment.GetEnvironmentVariable("APP_NAME") ?? "MyApp",
            Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Development",
            Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "");
    }
}

// Usage
var config = ConfigManager.Instance;
Console.WriteLine($"Running {config.AppName} in {config.Environment}");
```

### Service Locator

```csharp
[Singleton]
public partial class ServiceLocator
{
    private readonly Dictionary<Type, object> _services = new();
    
    private ServiceLocator() { }
    
    public void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }
    
    public T Resolve<T>() where T : class
    {
        return (T)_services[typeof(T)];
    }
}

// Usage
ServiceLocator.Instance.Register<ILogger>(new ConsoleLogger());
var logger = ServiceLocator.Instance.Resolve<ILogger>();
```

### Application Clock

```csharp
[Singleton]
public partial class AppClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTimeOffset Now => DateTimeOffset.Now;
    
    private AppClock() { }
}

// Usage - consistent time source throughout app
var timestamp = AppClock.Instance.UtcNow;
```

## Troubleshooting

### PKSNG001: Type must be partial

**Cause:** Missing `partial` keyword.

**Fix:**
```csharp
// ❌ Wrong
[Singleton]
public class MySingleton { }

// ✅ Correct
[Singleton]
public partial class MySingleton { }
```

### PKSNG003: No usable constructor or factory

**Cause:** Type has no parameterless constructor and no factory method.

**Fix:** Add a parameterless constructor or factory:
```csharp
// Option 1: Parameterless constructor
[Singleton]
public partial class MySingleton
{
    private MySingleton() { }
}

// Option 2: Factory method
[Singleton]
public partial class MySingleton
{
    private MySingleton(string config) { }

    [SingletonFactory]
    private static MySingleton Create() => new("default.json");
}
```

### PKSNG005: Public constructor warning

**Cause:** Public constructor allows bypassing singleton.

**Fix:** Make constructor private:
```csharp
// ❌ Generates warning
public MySingleton() { }

// ✅ No warning
private MySingleton() { }
```

## See Also

- [Memento Generator](memento.md) — For saving/restoring singleton state
- [Prototype Generator](prototype.md) — For cloning patterns
- [GoF: Singleton](https://en.wikipedia.org/wiki/Singleton_pattern)
