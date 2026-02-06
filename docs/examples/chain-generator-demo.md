# Chain Generator Demo

## Goal

Demonstrate both Chain of Responsibility and Pipeline models using the `[Chain]` source generator. The example shows an HTTP request router (Responsibility) and a middleware pipeline (Pipeline).

## Key Idea

The Chain generator creates handler orchestration code at compile time. In **Responsibility** mode, handlers are tried in order until one succeeds. In **Pipeline** mode, handlers wrap each other like middleware layers around a terminal handler.

## Responsibility Model: Request Router

Handlers return `bool` to indicate whether they handled the request:

```csharp
[Chain(Model = ChainModel.Responsibility)]
public partial class RequestRouter
{
    [ChainHandler(0)]
    private bool TryHandleHealth(in HttpRequest input, out string output)
    {
        if (input.Path == "/health" && input.Method == "GET")
        { output = "200 OK: healthy"; return true; }
        output = default!; return false;
    }

    [ChainHandler(1)]
    private bool TryHandleGetUsers(in HttpRequest input, out string output) { ... }

    [ChainDefault]
    private string HandleNotFound(in HttpRequest input)
        => $"404 Not Found: {input.Method} {input.Path}";
}
```

Usage:

```csharp
var router = new RequestRouter();
var req = new HttpRequest("/health", "GET", null);
string result = router.Handle(in req);        // "200 OK: healthy"
bool ok = router.TryHandle(in req, out var r); // true
```

## Pipeline Model: Middleware

Handlers call `next` to continue the chain:

```csharp
[Chain(Model = ChainModel.Pipeline)]
public partial class MiddlewarePipeline
{
    [ChainHandler(0)]
    private string LoggingMiddleware(in string input, Func<string, string> next)
        => $"[LOG] {next(input)}";

    [ChainHandler(1)]
    private string AuthMiddleware(in string input, Func<string, string> next)
        => input.StartsWith("DENY") ? "403 Forbidden" : next(input);

    [ChainTerminal]
    private string ProcessRequest(in string input) => $"200 OK: {input}";
}
```

## Mental Model

**Responsibility**: Think of a call center where each agent checks if they can help. The first agent who can handle your issue takes over.

**Pipeline**: Think of a series of filters on a water pipe. Water flows through each filter in order, with each filter able to modify or block the flow.

```
Responsibility:  Handler0 -> Handler1 -> Handler2 -> Default
                    |            |            |
                 (match?)    (match?)    (match?)

Pipeline:  Logging( Auth( Terminal(input) ) )
              ^       ^        ^
           outer    middle   inner
```

## Test References

- Generator tests: `test/PatternKit.Generators.Tests/ChainGeneratorTests.cs`
- Example tests: `test/PatternKit.Examples.Tests/Generators/Chain/ChainGeneratorDemoTests.cs`
