using System;

namespace PatternKit.Generators.Proxy;

/// <summary>
/// Marks a member (method or property) to be excluded from proxy generation.
/// The member will not be implemented in the generated proxy class.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute when you want to:
/// <list type="bullet">
/// <item>Exclude specific members from proxying</item>
/// <item>Handle certain members manually in a partial proxy implementation</item>
/// <item>Skip deprecated or unsupported members</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [GenerateProxy]
/// public interface IUserService
/// {
///     User Get(Guid id);
///     
///     [ProxyIgnore]
///     void LegacyMethod(); // This will not be included in the proxy
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ProxyIgnoreAttribute : Attribute
{
}
