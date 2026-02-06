using PatternKit.Generators.Chain;

namespace PatternKit.Examples.Generators.Chain;

#region Domain Types

/// <summary>
/// Represents an HTTP-like request for demonstration purposes.
/// </summary>
public record struct HttpRequest(string Path, string Method, string? Body);

#endregion

#region Responsibility Model Example

/// <summary>
/// Demonstrates the Chain of Responsibility model where the first matching handler wins.
/// Simulates an HTTP request router that dispatches by method + path.
/// </summary>
[Chain(Model = ChainModel.Responsibility)]
public partial class RequestRouter
{
    [ChainHandler(0, Name = "HealthCheck")]
    private bool TryHandleHealth(in HttpRequest input, out string output)
    {
        if (input.Path == "/health" && input.Method == "GET")
        {
            output = "200 OK: healthy";
            return true;
        }
        output = default!;
        return false;
    }

    [ChainHandler(1, Name = "GetUsers")]
    private bool TryHandleGetUsers(in HttpRequest input, out string output)
    {
        if (input.Path == "/users" && input.Method == "GET")
        {
            output = "200 OK: [alice, bob]";
            return true;
        }
        output = default!;
        return false;
    }

    [ChainHandler(2, Name = "CreateUser")]
    private bool TryHandleCreateUser(in HttpRequest input, out string output)
    {
        if (input.Path == "/users" && input.Method == "POST")
        {
            output = "201 Created: " + (input.Body ?? "empty");
            return true;
        }
        output = default!;
        return false;
    }

    [ChainDefault]
    private string HandleNotFound(in HttpRequest input)
    {
        return $"404 Not Found: {input.Method} {input.Path}";
    }
}

#endregion

#region Pipeline Model Example

/// <summary>
/// Demonstrates the Pipeline model where handlers wrap each other like middleware.
/// Simulates a logging/auth/compression pipeline that wraps request processing.
/// </summary>
[Chain(Model = ChainModel.Pipeline)]
public partial class MiddlewarePipeline
{
    [ChainHandler(0, Name = "Logging")]
    private string LoggingMiddleware(in string input, Func<string, string> next)
    {
        var result = next(input);
        return $"[LOG] {result}";
    }

    [ChainHandler(1, Name = "Auth")]
    private string AuthMiddleware(in string input, Func<string, string> next)
    {
        if (input.StartsWith("DENY"))
            return "403 Forbidden";
        return next(input);
    }

    [ChainTerminal]
    private string ProcessRequest(in string input) => $"200 OK: {input}";
}

#endregion

/// <summary>
/// Runs the Chain generator demonstration showing both Responsibility and Pipeline models.
/// </summary>
public static class ChainGeneratorDemo
{
    /// <summary>
    /// Executes all chain demonstrations and returns logged output lines.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();

        // --- Responsibility Model ---
        log.Add("=== Responsibility Model: Request Router ===");

        var router = new RequestRouter();

        var req1 = new HttpRequest("/health", "GET", null);
        log.Add($"  {req1.Method} {req1.Path} -> {router.Handle(in req1)}");

        var req2 = new HttpRequest("/users", "GET", null);
        log.Add($"  {req2.Method} {req2.Path} -> {router.Handle(in req2)}");

        var req3 = new HttpRequest("/users", "POST", "charlie");
        log.Add($"  {req3.Method} {req3.Path} -> {router.Handle(in req3)}");

        var req4 = new HttpRequest("/unknown", "DELETE", null);
        log.Add($"  {req4.Method} {req4.Path} -> {router.Handle(in req4)}");

        // TryHandle demonstration
        var req5 = new HttpRequest("/health", "GET", null);
        var handled = router.TryHandle(in req5, out var result);
        log.Add($"  TryHandle /health -> handled={handled}, result={result}");

        // --- Pipeline Model ---
        log.Add("");
        log.Add("=== Pipeline Model: Middleware Pipeline ===");

        var pipeline = new MiddlewarePipeline();

        var msg1 = "hello";
        log.Add($"  '{msg1}' -> {pipeline.Handle(in msg1)}");

        var msg2 = "DENY request";
        log.Add($"  '{msg2}' -> {pipeline.Handle(in msg2)}");

        return log;
    }
}
