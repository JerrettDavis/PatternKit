using System;

namespace PatternKit.Generators.Proxy;

/// <summary>
/// Marks an interface or abstract class for proxy pattern code generation.
/// Generates a proxy type that wraps an inner instance and optionally supports interceptors
/// for cross-cutting concerns (logging, timing, caching, authentication, etc.).
/// </summary>
/// <remarks>
/// <para>
/// The proxy generator creates:
/// <list type="bullet">
/// <item>A proxy class implementing the contract that delegates all calls to an inner instance</item>
/// <item>Optional interceptor interfaces for Before/After/OnException hooks (sync + async)</item>
/// <item>Optional MethodContext types for strongly-typed interceptor parameters</item>
/// </list>
/// </para>
/// <para>
/// <strong>Supported Targets:</strong>
/// <list type="bullet">
/// <item><c>interface</c> - All members are proxied</item>
/// <item><c>abstract class</c> - Only virtual/abstract members are proxied</item>
/// </list>
/// </para>
/// <para>
/// <strong>Limitations (v1):</strong>
/// <list type="bullet">
/// <item>Generic contracts are not supported</item>
/// <item>Nested types are not supported</item>
/// <item>Events are not supported</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// Basic proxy generation:
/// <code>
/// [GenerateProxy]
/// public partial interface IUserService
/// {
///     User Get(Guid id);
///     ValueTask&lt;User&gt; GetAsync(Guid id, CancellationToken ct = default);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateProxyAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the generated proxy type.
    /// If not specified, defaults to <c>{ContractName}Proxy</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// [GenerateProxy(ProxyTypeName = "UserServiceLoggingProxy")]
    /// public interface IUserService { }
    /// </code>
    /// </example>
    public string? ProxyTypeName { get; set; }

    /// <summary>
    /// Gets or sets the interceptor mode for this proxy.
    /// Defaults to <see cref="ProxyInterceptorMode.Single"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="ProxyInterceptorMode.None"/> - No interceptor support, pure delegation</item>
    /// <item><see cref="ProxyInterceptorMode.Single"/> - Single interceptor instance</item>
    /// <item><see cref="ProxyInterceptorMode.Pipeline"/> - Multiple interceptors with deterministic ordering</item>
    /// </list>
    /// </remarks>
    public ProxyInterceptorMode InterceptorMode { get; set; } = ProxyInterceptorMode.Single;

    /// <summary>
    /// Gets or sets whether async interceptor hooks should be generated.
    /// If not specified, async support is inferred from the contract
    /// (enabled if any member returns Task/ValueTask or has a CancellationToken parameter).
    /// </summary>
    public bool? GenerateAsync { get; set; }

    /// <summary>
    /// Gets or sets whether to force async interceptor hooks even if no async members are detected.
    /// This is useful for future-proofing or when async support may be added later.
    /// </summary>
    public bool ForceAsync { get; set; }

    /// <summary>
    /// Gets or sets the exception handling policy for interceptors.
    /// Defaults to <see cref="ProxyExceptionPolicy.Rethrow"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="ProxyExceptionPolicy.Rethrow"/> - OnException is called, then exception is rethrown (default)</item>
    /// <item><see cref="ProxyExceptionPolicy.Swallow"/> - OnException is called, exception is suppressed (use with caution)</item>
    /// </list>
    /// </remarks>
    public ProxyExceptionPolicy Exceptions { get; set; } = ProxyExceptionPolicy.Rethrow;
}
