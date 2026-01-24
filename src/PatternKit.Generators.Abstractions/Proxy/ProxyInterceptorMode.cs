namespace PatternKit.Generators.Proxy;

/// <summary>
/// Specifies the interceptor support mode for proxy generation.
/// </summary>
public enum ProxyInterceptorMode
{
    /// <summary>
    /// No interceptor support. Proxy only delegates calls to the inner instance.
    /// </summary>
    None = 0,

    /// <summary>
    /// Single interceptor support. Proxy accepts a single <c>I{TypeName}Interceptor</c> instance.
    /// </summary>
    Single = 1,

    /// <summary>
    /// Pipeline interceptor support. Proxy accepts <c>IReadOnlyList&lt;I{TypeName}Interceptor&gt;</c>
    /// and invokes interceptors in deterministic order (Before: ascending, After: descending).
    /// </summary>
    Pipeline = 2
}
