# Generated Splitter And Aggregator

This example shows the fluent and source-generated paths for two Enterprise Integration Patterns that are often used together:

- Splitter turns one aggregate message into item-level messages while preserving correlation metadata.
- Aggregator collects related item messages and projects a result when the completion policy is satisfied.

The source-generated path is useful when the split projection, correlation key, completion rule, and projection are stable application contracts. It emits ordinary `Splitter<TPayload, TItem>` and `Aggregator<TKey, TItem, TResult>` factories, so the generated artifacts can be registered through `IServiceCollection` like any other PatternKit primitive.

## Source

- `src/PatternKit.Examples/Messaging/MessageRoutingExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/MessageRoutingExampleTests.cs`

## Fluent Path

```csharp
var splitter = Splitter<RoutedOrder, RoutedLine>.Create()
    .Use((message, context) => message.Payload.Lines)
    .Build();

var lineMessages = splitter.Split(orderMessage);

var aggregator = Aggregator<string, RoutedLine, decimal>.Create()
    .KeyBy((message, context) => message.Headers.CorrelationId ?? message.Payload.Sku)
    .CompleteWhen((key, messages, context) => messages.Count == lineMessages.Count)
    .Project((key, messages, context) => messages.Sum(message => message.Payload.Amount))
    .Build();
```

## Source-Generated Path

```csharp
[GenerateSplitter(typeof(RoutedOrder), typeof(RoutedLine), FactoryName = "CreateLineSplitter")]
public static partial class GeneratedOrderLineSplitter
{
    [SplitterProjection]
    private static IEnumerable<RoutedLine> ProjectLines(Message<RoutedOrder> message, MessageContext context)
        => message.Payload.Lines;
}

[GenerateAggregator(typeof(string), typeof(RoutedLine), typeof(decimal), FactoryName = "CreateLineTotalAggregator")]
public static partial class GeneratedOrderLineAggregator
{
    [AggregatorCorrelation]
    private static string Correlate(Message<RoutedLine> message, MessageContext context)
        => message.Headers.CorrelationId ?? message.Payload.Sku;

    [AggregatorCompletion]
    private static bool Complete(string key, IReadOnlyList<Message<RoutedLine>> messages, MessageContext context)
        => messages.Count == 2;

    [AggregatorProjection]
    private static decimal Project(string key, IReadOnlyList<Message<RoutedLine>> messages, MessageContext context)
        => messages.Sum(message => message.Payload.Amount);
}
```

## Dependency Injection

```csharp
var services = new ServiceCollection();
services.AddGeneratedSplitterAggregatorExample();

using var provider = services.BuildServiceProvider(validateScopes: true);
var example = provider.GetRequiredService<GeneratedSplitterAggregatorExample>();
var summary = example.Runner.RunGenerated();
```

The extension registers a `MessageRoutingExampleRunner` with fluent and generated entry points. Applications can copy that shape directly: build the generated factories once at startup, register them as singletons, and inject the resulting splitter or aggregator into handlers.

## Validation

The TinyBDD tests assert that:

- fluent and generated paths route, split, and aggregate the same order message
- child messages preserve causation and correlation metadata
- the generated example is importable through `Microsoft.Extensions.DependencyInjection`
- the example catalog advertises messaging, source-generation, and DI integration surfaces
