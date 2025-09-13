using PatternKit.Behavioral.Chain;

namespace PatternKit.Examples.Chain;

public readonly record struct HttpRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers);

public readonly record struct HttpResponse(int Status, string Body);

public static class AuthLoggingDemo
{
    public static List<string> Run()
    {
        var log = new List<string>();

        var chain = ActionChain<HttpRequest>.Create()
            // request id (continue)
            .When(static (in r) => r.Headers.ContainsKey("X-Request-Id"))
            .ThenContinue(r => log.Add($"reqid={r.Headers["X-Request-Id"]}"))

            // admin requires auth (stop)
            .When(static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal)
                                   && !r.Headers.ContainsKey("Authorization"))
            .ThenStop(r => log.Add("deny: missing auth"))

            // finally always logs method/path
            .Finally((in r, next) =>
            {
                log.Add($"{r.Method} {r.Path}");
                next(r); // terminal "next" is a no-op
            })
            .Build();

        // simulate
        chain.Execute(new HttpRequest("GET", "/health", new Dictionary<string, string>()));
        chain.Execute(new HttpRequest("GET", "/admin/metrics", new Dictionary<string, string>()));

        return log; // ["GET /health", "deny: missing auth", "GET /admin/metrics"]
    }
}