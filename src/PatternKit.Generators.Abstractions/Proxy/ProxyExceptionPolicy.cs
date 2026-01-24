namespace PatternKit.Generators.Proxy;

/// <summary>
/// Specifies how exceptions are handled in proxy interceptors.
/// </summary>
public enum ProxyExceptionPolicy
{
    /// <summary>
    /// Exceptions are rethrown after invoking OnException/OnExceptionAsync.
    /// This is the default and recommended behavior.
    /// </summary>
    Rethrow = 0,

    /// <summary>
    /// Exceptions are swallowed (suppressed) after invoking OnException/OnExceptionAsync.
    /// Use with caution as this can hide errors and cause unexpected behavior.
    /// </summary>
    Swallow = 1
}
