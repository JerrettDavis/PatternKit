# Factory Pattern API Reference

Complete API documentation for the Factory pattern in PatternKit.

## Namespace

```csharp
using PatternKit.Creational.Factory;
```

---

## Factory\<TKey, TOut\>

Immutable factory mapping keys to parameterless creators.

```csharp
public sealed class Factory<TKey, TOut>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TKey` | Key type for lookup |
| `TOut` | Output type created |

### Delegates

```csharp
public delegate TOut Creator();
```

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create(IEqualityComparer<TKey>? comparer = null)` | `Builder` | Create builder with optional comparer |

### Instance Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create(TKey key)` | `TOut` | Create by key, throws if missing |
| `TryCreate(TKey key, out TOut value)` | `bool` | Safe creation, returns false if missing |

### Exceptions

| Method | Exception | Condition |
|--------|-----------|-----------|
| `Create` | `InvalidOperationException` | No mapping for key and no default |

### Example

```csharp
var factory = Factory<string, IService>
    .Create(StringComparer.OrdinalIgnoreCase)
    .Map("serviceA", () => new ServiceA())
    .Map("serviceB", () => new ServiceB())
    .Default(() => new FallbackService())
    .Build();

var service = factory.Create("servicea"); // ServiceA (case-insensitive)
```

---

## Factory\<TKey, TOut\>.Builder

Builder for configuring the factory.

```csharp
public sealed class Builder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Map(TKey key, Creator creator)` | `Builder` | Register key-to-creator mapping |
| `Default(Creator creator)` | `Builder` | Set default creator |
| `Build()` | `Factory<TKey, TOut>` | Build immutable factory |

### Semantics

- **Last mapping wins**: Calling `Map` with the same key replaces the previous
- **Default is optional**: Without default, missing keys throw
- **Snapshot**: `Build()` captures current state; further modifications don't affect built factories

---

## Factory\<TKey, TIn, TOut\>

Factory with input parameter for creators.

```csharp
public sealed class Factory<TKey, TIn, TOut>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TKey` | Key type for lookup |
| `TIn` | Input type passed to creators |
| `TOut` | Output type created |

### Delegates

```csharp
public delegate TOut Creator(in TIn input);
```

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create(IEqualityComparer<TKey>? comparer = null)` | `Builder` | Create builder |

### Instance Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create(TKey key, in TIn input)` | `TOut` | Create with input |
| `TryCreate(TKey key, in TIn input, out TOut value)` | `bool` | Safe creation |

### Example

```csharp
var math = Factory<string, int, int>
    .Create()
    .Map("double", static (in int x) => x * 2)
    .Map("square", static (in int x) => x * x)
    .Map("negate", static (in int x) => -x)
    .Default(static (in int x) => x)
    .Build();

var doubled = math.Create("double", 5);  // 10
var squared = math.Create("square", 4);  // 16
var identity = math.Create("unknown", 7); // 7 (default)
```

---

## Factory\<TKey, TIn, TOut\>.Builder

Builder for factory with input.

```csharp
public sealed class Builder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Map(TKey key, Creator creator)` | `Builder` | Register mapping |
| `Default(Creator creator)` | `Builder` | Set default |
| `Build()` | `Factory<TKey, TIn, TOut>` | Build factory |

---

## Thread Safety

| Component | Thread-Safe |
|-----------|-------------|
| `Builder` | No - single-threaded configuration |
| `Factory<TKey, TOut>` | Yes - immutable after build |
| `Factory<TKey, TIn, TOut>` | Yes - immutable after build |
| `Create` | Yes - dictionary read only |
| `TryCreate` | Yes - dictionary read only |

### Implementation Notes

- Single dictionary lookup per `Create` call
- No LINQ, reflection, or allocations in hot path
- `in TIn` parameter avoids struct copies

---

## Complete Example

```csharp
using PatternKit.Creational.Factory;

// Define types
public interface IFormatter
{
    string Format(object data);
}

public class JsonFormatter : IFormatter
{
    public string Format(object data) => JsonSerializer.Serialize(data);
}

public class XmlFormatter : IFormatter
{
    public string Format(object data) => XmlSerialize(data);
}

public class CsvFormatter : IFormatter
{
    public string Format(object data) => ToCsv(data);
}

// Create factory
public class FormatterFactory
{
    private readonly Factory<string, IFormatter> _factory;

    public FormatterFactory()
    {
        _factory = Factory<string, IFormatter>
            .Create(StringComparer.OrdinalIgnoreCase)
            .Map("json", static () => new JsonFormatter())
            .Map("xml", static () => new XmlFormatter())
            .Map("csv", static () => new CsvFormatter())
            .Default(static () => new JsonFormatter()) // JSON as fallback
            .Build();
    }

    public IFormatter GetFormatter(string format) =>
        _factory.Create(format);

    public bool TryGetFormatter(string format, out IFormatter formatter) =>
        _factory.TryCreate(format, out formatter);
}

// Usage
var factory = new FormatterFactory();
var json = factory.GetFormatter("JSON");     // JsonFormatter
var xml = factory.GetFormatter("xml");       // XmlFormatter
var fallback = factory.GetFormatter("yaml"); // JsonFormatter (default)

if (factory.TryGetFormatter("csv", out var csv))
{
    var output = csv.Format(data);
}
```

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [Real-World Examples](real-world-examples.md)
