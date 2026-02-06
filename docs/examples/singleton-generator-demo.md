# Singleton Generator Demo

## Goal

Show two common singleton variants using the `[Singleton]` source generator:

1. **Eager singleton** -- an application clock created at class-load time.
2. **Lazy singleton with factory** -- a configuration service that loads settings on first access.

## Key Idea

The `[Singleton]` attribute on a partial class generates a static `Instance` property (or custom-named property) backed by either a direct initializer (eager) or `System.Lazy<T>` (lazy). The `[SingletonFactory]` attribute lets you specify a custom creation method instead of `new T()`.

## Code Snippet

```csharp
// Eager: created immediately
[Singleton]
public partial class AppClock
{
    private AppClock() { }
    public string Now => DateTime.UtcNow.ToString("O");
}

// Lazy with factory: created on first access
[Singleton(Mode = SingletonMode.Lazy, InstancePropertyName = "Current")]
public partial class AppConfig
{
    private AppConfig(Dictionary<string, string> settings) { ... }

    [SingletonFactory]
    private static AppConfig LoadConfig() => new AppConfig(LoadSettings());
}
```

## Mental Model

```
AppClock.Instance        -->  static field initializer (type-load time)
AppConfig.Current        -->  Lazy<AppConfig>.Value     (first-access time)
                               +--factory method---------+
```

Both properties always return the same reference. The generator enforces that only one factory method exists and warns if constructors are public.

## Test References

- `SingletonGeneratorDemoTests.Eager_Singleton_Returns_Same_Instance`
- `SingletonGeneratorDemoTests.Lazy_Singleton_Returns_Same_Instance`
- `SingletonGeneratorDemoTests.Lazy_Singleton_Loads_Config_Via_Factory`
- `SingletonGeneratorDemoTests.Demo_Run_Executes_Without_Errors`
