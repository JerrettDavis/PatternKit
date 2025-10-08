namespace PatternKit.Examples.ApiGateway;

public static class Demo
{
    public static void Run()
    {
        Run(Console.Out);
    }

    public static void Run(TextWriter writer)
    {
        var router = MiniRouter.Create()
            // --- middleware (first-match-wins) ---
            // capture request-id when present
            .Use(
                static (in r) => r.Headers.ContainsKey("X-Request-Id"),
                (in r) => writer.WriteLine($"reqid={r.Headers["X-Request-Id"]}"))
            // auth short-circuit: /admin requires bearer token
            .Use(
                static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal) &&
                                 !r.Headers.ContainsKey("Authorization"),
                (in _) => writer.WriteLine("Denied: missing Authorization"))
            // default is noop (set in Build)

            // --- routes (first-match-wins) ---
            .Map(
                static (in r) => r is { Method: "GET", Path: "/health" },
                static (in _) => Responses.Text(200, "OK"))
            .Map(
                static (in r) => r.Method == "GET" && r.Path.StartsWith("/users/", StringComparison.Ordinal),
                static (in r) =>
                {
                    var idStr = r.Path["/users/".Length..];
                    return int.TryParse(idStr, out var id)
                        ? Responses.Json(200, $"{{\"id\":{id},\"name\":\"user{id}\"}}")
                        : Responses.Text(404, "User not found");
                })
            .Map(
                static (in r) => r is { Method: "POST", Path: "/users" },

                // pretend to create the user from r.Body...
                static (in _) => Responses.Json(201, "{\"ok\":true}"))
            .Map(
                static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal) &&
                                 !r.Headers.ContainsKey("Authorization"),
                static (in _) => Responses.Unauthorized())
            .NotFound(static (in _) => Responses.NotFound())
            .Build();

        // --- simulate a few calls ---
        var commonHeaders = new Dictionary<string, string> { ["Accept"] = "application/json" };

        Print(router.Handle(new Request("GET", "/health", commonHeaders)), writer);
        Print(router.Handle(new Request("GET", "/users/42", commonHeaders)), writer);
        Print(router.Handle(new Request("GET", "/users/abc", commonHeaders)), writer);
        Print(router.Handle(new Request("GET", "/admin/metrics", new Dictionary<string, string>())), writer); // unauthorized
        Print(router.Handle(new Request("POST", "/users", commonHeaders, "{\"name\":\"Ada\"}")), writer);
        Print(router.Handle(new Request("GET", "/nope", commonHeaders)), writer);

        return;

        static void Print(Response res, TextWriter w)
            => w.WriteLine($"{res.StatusCode} {res.ContentType}\n{res.Body}\n");
    }
}