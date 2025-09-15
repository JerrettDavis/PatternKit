using PatternKit.Behavioral.Chain;

namespace PatternKit.Examples.Chain;

/// <summary>
/// Minimal HTTP-ish request used by the <see cref="AuthLoggingDemo"/> chain.
/// </summary>
/// <param name="Method">HTTP method, e.g., <c>GET</c>, <c>POST</c>.</param>
/// <param name="Path">Request path beginning with <c>/</c> (e.g., <c>/admin/stats</c>).</param>
/// <param name="Headers">Request headers (case sensitivity is determined by the provided dictionary).</param>
/// <remarks>
/// This is a tiny, immutable record struct meant to keep the example focused on chain composition rather than I/O.
/// </remarks>
public readonly record struct HttpRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Minimal HTTP-ish response. Included for completeness; not used by this specific demo.
/// </summary>
/// <param name="Status">HTTP status code.</param>
/// <param name="Body">Payload body (if any).</param>
public readonly record struct HttpResponse(int Status, string Body);

/// <summary>
/// Demonstrates an <see cref="ActionChain{T}"/> over <see cref="HttpRequest"/> that composes
/// request-id logging and an auth gate for <c>/admin/*</c> without <c>if</c>/<c>else</c> ladders.
/// </summary>
/// <remarks>
/// <para>
/// The chain shows three kinds of steps:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       <b>Conditional log (continue):</b> If <c>X-Request-Id</c> is present,
///       log <c>reqid=&lt;id&gt;</c> and <i>continue</i> (via <see cref="ActionChain{T}.ThenContinue(System.Action{T})"/>).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Auth gate (stop):</b> If the path starts with <c>/admin</c> and no <c>Authorization</c> header exists,
///       log <c>deny: missing auth</c> and <i>stop</i> the chain early
///       (via <see cref="ActionChain{T}.ThenStop(System.Action{T})"/>).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Tail log (finally-on-continue):</b> In <see cref="ActionChain{T}.Finally(System.Action{T, Action{T}})"/>
///       we log <c>{Method} {Path}</c>. This <em>runs only if no prior step called <c>ThenStop</c></em>
///       (strict-stop semantics). In other words, a <c>Stop</c> short-circuits the chain and
///       prevents this tail from running.
///     </description>
///   </item>
/// </list>
/// <para>
/// If you want method/path logging to run <em>even when</em> a stop occurs, move that logic into an
/// <c>.Always(...)</c> step (if available in your version of PatternKit) or emit the log inside each
/// stop branch explicitly.
/// </para>
/// <para>
/// This demo executes two simulated requests:
/// </para>
/// <list type="bullet">
///   <item>
///     <description><c>GET /health</c> (no stop) → tail log runs.</description>
///   </item>
///   <item>
///     <description><c>GET /admin/metrics</c> (missing auth) → stop after deny; tail log does <em>not</em> run.</description>
///   </item>
/// </list>
/// </remarks>
/// <example>
/// <code language="csharp">
/// // Produces two log lines with strict-stop semantics:
/// // 1) "GET /health"
/// // 2) "deny: missing auth"
/// var lines = AuthLoggingDemo.Run();
/// </code>
/// </example>
/// <seealso cref="ActionChain{T}"/>
/// <seealso cref="PatternKit.Behavioral.Chain"/>
public static class AuthLoggingDemo
{
    /// <summary>
    /// Builds and executes the demo chain, returning the emitted log lines.
    /// </summary>
    /// <returns>
    /// A list of log lines in execution order. With strict-stop semantics the result is:
    /// <list type="number">
    ///   <item><description><c>GET /health</c></description></item>
    ///   <item><description><c>deny: missing auth</c></description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// The chain is built once and executed twice against in-memory <see cref="HttpRequest"/> instances.
    /// No I/O is performed; the focus is on composition and control flow.
    /// </para>
    /// <para>
    /// <b>Why strict-stop?</b> It keeps “deny” paths clean and predictable—once you stop, nothing else runs.
    /// To force logging regardless of stop/continue, prefer an explicit <c>.Always(...)</c> step (if present)
    /// or duplicate the necessary log in the stop branch.
    /// </para>
    /// </remarks>
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

            // tail log (runs only if the chain wasn't stopped earlier)
            .Finally((in r, next) =>
            {
                log.Add($"{r.Method} {r.Path}");
                next(r); // terminal "next" is a no-op
            })
            .Build();

        // simulate
        chain.Execute(new HttpRequest("GET", "/health", new Dictionary<string, string>()));
        chain.Execute(new HttpRequest("GET", "/admin/metrics", new Dictionary<string, string>()));

        return log; // ["GET /health", "deny: missing auth"]
    }
}