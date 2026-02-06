using PatternKit.Behavioral.Strategy;

namespace PatternKit.Examples.ApiGateway;

/// <summary>
/// Minimal HTTP-like request used by the API gateway demo.
/// </summary>
/// <param name="Method">HTTP method (e.g., GET, POST).</param>
/// <param name="Path">Request path (e.g., /orders/123).</param>
/// <param name="Headers">Request headers map.</param>
/// <param name="Body">Optional raw body string.</param>
public readonly record struct Request(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    string? Body = null
);

/// <summary>
/// Minimal HTTP-like response produced by routes.
/// </summary>
/// <param name="StatusCode">HTTP status code.</param>
/// <param name="ContentType">MIME content type.</param>
/// <param name="Body">Raw response body.</param>
public readonly record struct Response(
    int StatusCode,
    string ContentType,
    string Body
);

/// <summary>
/// Small helpers to build common <see cref="Response"/> shapes.
/// </summary>
public static class Responses
{
    /// <summary>Create a text/plain response with status and body.</summary>
    /// <param name="status">HTTP status code.</param>
    /// <param name="body">Response text body.</param>
    public static Response Text(int status, string body)
        => new(status, "text/plain; charset=utf-8", body);

    /// <summary>Create an application/json response with status and pre‑serialized JSON body.</summary>
    /// <param name="status">HTTP status code.</param>
    /// <param name="json">Serialized JSON string.</param>
    public static Response Json(int status, string json)
        => new(status, "application/json; charset=utf-8", json);

    /// <summary>Build a standard 404 Not Found response.</summary>
    public static Response NotFound()
        => Text(404, "Not Found");

    /// <summary>Build a standard 401 Unauthorized response.</summary>
    public static Response Unauthorized()
        => Text(401, "Unauthorized");
}

/// <summary>
/// A tiny API gateway/router showing how ActionStrategy + Strategy + TryStrategy
/// compose into a pragmatic HTTP-ish pipeline.
/// </summary>
/// <summary>
/// A tiny API gateway/router composing middleware, routes, and content negotiation.
/// </summary>
public sealed class MiniRouter
{
    private readonly ActionStrategy<Request> _middleware;
    private readonly Strategy<Request, Response> _routes;
    private readonly TryStrategy<Request, string> _negotiate; // produces a content-type

    private MiniRouter(
        ActionStrategy<Request> middleware,
        Strategy<Request, Response> routes,
        TryStrategy<Request, string> negotiate)
        => (_middleware, _routes, _negotiate) = (middleware, routes, negotiate);

    /// <summary>
    /// Processes a request through middleware, routes, and content negotiation.
    /// </summary>
    /// <param name="req">The incoming request.</param>
    /// <returns>The route response with content type negotiated.</returns>
    public Response Handle(in Request req)
    {
        // fire first-matching side-effect (e.g., logging, auth short-circuit)
        _middleware.TryExecute(in req);

        var res = _routes.Execute(in req);

        // simple content negotiation: pick content-type if handler left it blank
        if (string.IsNullOrWhiteSpace(res.ContentType)
            && _negotiate.Execute(in req, out var ct)
            && ct is { Length: > 0 })
            return res with { ContentType = ct };

        return res;
    }

    /// <summary>Create a new builder for <see cref="MiniRouter"/>.</summary>
    public static Builder Create() => new();

    public sealed class Builder
    {
        private readonly ActionStrategy<Request>.Builder _mw = ActionStrategy<Request>.Create();
        private readonly Strategy<Request, Response>.Builder _routes = Strategy<Request, Response>.Create();
        private TryStrategy<Request, string>? _neg;

        /// <summary>Add first-match middleware (e.g. logging, metrics, CORS/OPTIONS, auth).</summary>
        public Builder Use(ActionStrategy<Request>.Predicate when, ActionStrategy<Request>.ActionHandler then)
        {
            _mw.When(when).Then(then);
            return this;
        }

        /// <summary>Map a route: first predicate that matches returns the response.</summary>
        public Builder Map(Strategy<Request, Response>.Predicate when, Strategy<Request, Response>.Handler then)
        {
            _routes.When(when).Then(then);
            return this;
        }

        /// <summary>Default route when nothing matches.</summary>
        public Builder NotFound(Strategy<Request, Response>.Handler handler)
        {
            _routes.Default(handler);
            return this;
        }

        /// <summary>Provide a custom content-negotiator (optional).</summary>
        public Builder WithNegotiator(TryStrategy<Request, string> negotiator)
        {
            _neg = negotiator;
            return this;
        }

        /// <summary>Builds an immutable router instance.</summary>
        public MiniRouter Build()
        {
            // Middleware default: do nothing if nothing matched
            _mw.Default(static (in _) => { });

            var mw = _mw.Build();
            var routes = _routes.Build();
            var neg = _neg ?? DefaultNegotiator();
            return new MiniRouter(mw, routes, neg);
        }

        /// <summary>
        /// Default content negotiator that picks JSON if requested, then text, otherwise JSON.
        /// </summary>
        private static TryStrategy<Request, string> DefaultNegotiator()
        {
            // Tiny Accept negotiator:
            //  - if Accept contains "application/json" -> pick json
            //  - if Accept contains "text/plain" -> pick text
            //  - else default to json
            return TryStrategy<Request, string>.Create()
                .Always(static (in r, out ct) =>
                {
                    if (r.Headers.TryGetValue("Accept", out var a) &&
                        a.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                    {
                        ct = "application/json; charset=utf-8";
                        return true;
                    }

                    ct = null;
                    return false;
                })
                .Or.Always(static (in r, out ct) =>
                {
                    if (r.Headers.TryGetValue("Accept", out var a) &&
                        a.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
                    {
                        ct = "text/plain; charset=utf-8";
                        return true;
                    }

                    ct = null;
                    return false;
                })
                .Finally(static (in _, out ct) =>
                {
                    ct = "application/json; charset=utf-8";
                    return true;
                })
                .Build();
        }
    }
}
