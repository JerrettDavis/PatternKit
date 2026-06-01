# Ports and Adapters Generator

Annotate a partial host with `[GeneratePortsAndAdapters]` and mark one inbound adapter, one application port, and one outbound adapter.

```csharp
[GeneratePortsAndAdapters(typeof(HttpRequest), typeof(Command), typeof(Result), typeof(HttpResponse))]
public static partial class OrderEntry
{
    [InboundAdapter]
    private static Command Inbound(HttpRequest request) => new(request.Id);

    [ApplicationPort]
    private static ValueTask<Result> Handle(Command command, CancellationToken ct) => new(new(command.Id));

    [OutboundAdapter]
    private static HttpResponse Outbound(Result result) => new(202, result.Id);
}
```

The generated `Create()` factory returns a `PortsAndAdaptersPipeline<TInbound,TCommand,TResult,TOutbound>`.
