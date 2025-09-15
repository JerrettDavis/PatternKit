using PatternKit.Behavioral.Strategy;

namespace PatternKit.Examples.ApiGateway;

// Ultra-minimal request/response types used by the demo.
public readonly record struct Request(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    string? Body = null
);

public readonly record struct Response(
    int StatusCode,
    string ContentType,
    string Body
);

public static class Responses
{
    public static Response Text(int status, string body)
        => new(status, "text/plain; charset=utf-8", body);

    public static Response Json(int status, string json)
        => new(status, "application/json; charset=utf-8", json);

    public static Response NotFound()
        => Text(404, "Not Found");

    public static Response Unauthorized()
        => Text(401, "Unauthorized");
}

/// <summary>
/// A tiny API gateway/router showing how ActionStrategy + Strategy + TryStrategy
/// compose into a pragmatic HTTP-ish pipeline.
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

        public MiniRouter Build()
        {
            // Middleware default: do nothing if nothing matched
            _mw.Default(static (in _) => { });

            var mw = _mw.Build();
            var routes = _routes.Build();
            var neg = _neg ?? DefaultNegotiator();
            return new MiniRouter(mw, routes, neg);
        }

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