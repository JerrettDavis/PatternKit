# Decorator Pattern

The Decorator pattern provides a flexible alternative to subclassing for extending functionality. PatternKit's fluent implementation allows you to wrap components with layered behavior enhancements.

## Key Features

- **Before decorators**: Transform input before component execution
- **After decorators**: Transform output after component execution  
- **Around decorators**: Full control over execution flow
- **Fluent API**: Chainable, discoverable builder pattern
- **Immutable**: Thread-safe after build
- **Zero-allocation hot paths**: Minimal overhead

## Documentation

- [Decorator Pattern Guide](decorator.md) - Complete documentation with examples
- [API Reference](xref:PatternKit.Structural.Decorator) - Auto-generated API docs

## Quick Example

```csharp
using PatternKit.Structural.Decorator;

// Add logging and caching to any operation
var cache = new Dictionary<int, int>();
var decorated = Decorator<int, int>.Create(x => ExpensiveComputation(x))
    .Around((x, next) => {
        Console.WriteLine($"Computing {x}");
        if (cache.TryGetValue(x, out var cached))
            return cached;
        var result = next(x);
        cache[x] = result;
        return result;
    })
    .Build();
```

## Common Use Cases

- **Logging**: Add tracing without modifying core logic
- **Caching**: Memoize expensive operations
- **Validation**: Verify inputs/outputs
- **Authorization**: Check permissions before execution
- **Error Handling**: Add retry logic or circuit breakers
- **Performance Monitoring**: Track execution metrics
- **Transformation**: Modify inputs/outputs declaratively

