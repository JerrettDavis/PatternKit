# Template Method Demo

This demo shows two ways to use PatternKit’s Template Method:

- Subclassing: derive from `TemplateMethod<TContext, TResult>` and override hooks.
- Fluent: compose a `Template<TContext, TResult>` with `Before/After/OnError/Synchronized`.

Both give you a consistent workflow shape with customizable steps and optional synchronization.

## Subclassing demo (DataProcessor)
Counts words in a string while logging before/after.

```csharp
public class DataProcessor : TemplateMethod<string, int>
{
    protected override void OnBefore(string context)
    {
        Console.WriteLine($"Preparing to process: {context}");
    }

    protected override int Step(string context)
    {
        return context.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    protected override void OnAfter(string context, int result)
    {
        Console.WriteLine($"Processed '{context}' with result: {result}");
    }
}

var processor = new DataProcessor();
var result = processor.Execute("The quick brown fox jumps over the lazy dog");
Console.WriteLine($"Word count: {result}");
```

## Fluent demo (TemplateFluentDemo)
Same behavior using the fluent builder, plus non-throwing `TryExecute` and an `OnError` hook.

```csharp
var tpl = Template<string, int>
    .Create(ctx => ctx.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
    .Before(ctx => Console.WriteLine($"[Before] Input: '{ctx}'"))
    .After((ctx, res) => Console.WriteLine($"[After] '{ctx}' -> words: {res}"))
    .OnError((ctx, err) => Console.WriteLine($"[Error] Input '{ctx}', error: {err}"))
    .Synchronized() // optional
    .Build();

if (tpl.TryExecute("The quick brown fox", out var count, out var error))
    Console.WriteLine($"Word count: {count}");
else
    Console.WriteLine($"Failed: {error}");
```

## When to use which
- Prefer subclassing when the algorithm is a stable concept in your domain, and you want a strongly‑named type.
- Prefer fluent when you want to compose quickly, add multiple hooks, or opt into `TryExecute` easily.

## Thread safety
- Subclassing: override `Synchronized` to serialize `Execute` calls.
- Fluent: call `.Synchronized()` on the builder.
- Leave off for maximum concurrency when your step/hook logic is already thread-safe.

## See Also
- [Template Method Pattern Documentation](../patterns/behavioral/template/templatemethod.md)
- Refactoring Guru: Template Method — https://refactoring.guru/design-patterns/template-method
