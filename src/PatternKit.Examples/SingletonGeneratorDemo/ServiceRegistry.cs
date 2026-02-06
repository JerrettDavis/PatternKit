using PatternKit.Generators.Singleton;
using System.Collections.Concurrent;

namespace PatternKit.Examples.SingletonGeneratorDemo;

/// <summary>
/// Simple service registry singleton demonstrating thread-safe lazy initialization.
/// In production, consider using a proper DI container like Microsoft.Extensions.DependencyInjection.
/// </summary>
[Singleton(Mode = SingletonMode.Lazy, Threading = SingletonThreading.ThreadSafe)]
public partial class ServiceRegistry
{
    // Use Lazy<object> for thread-safe single-call factory semantics
    private readonly ConcurrentDictionary<Type, Lazy<object>> _services = new();

    private ServiceRegistry() { }

    /// <summary>
    /// Registers a service instance.
    /// </summary>
    public void Register<TService>(TService service) where TService : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(TService)] = new Lazy<object>(() => service);
    }

    /// <summary>
    /// Registers a factory for lazy service creation.
    /// The factory is guaranteed to be called at most once, even under concurrent access.
    /// </summary>
    public void RegisterFactory<TService>(Func<TService> factory) where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        _services[typeof(TService)] = new Lazy<object>(() => factory());
    }

    /// <summary>
    /// Resolves a registered service.
    /// </summary>
    /// <exception cref="InvalidOperationException">Service not registered.</exception>
    public TService Resolve<TService>() where TService : class
    {
        var type = typeof(TService);

        if (_services.TryGetValue(type, out var lazy))
        {
            return (TService)lazy.Value;
        }

        throw new InvalidOperationException($"Service {type.Name} is not registered.");
    }

    /// <summary>
    /// Tries to resolve a service, returning null if not found.
    /// </summary>
    public TService? TryResolve<TService>() where TService : class
    {
        var type = typeof(TService);

        if (_services.TryGetValue(type, out var lazy))
        {
            return (TService)lazy.Value;
        }

        return null;
    }

    /// <summary>
    /// Checks if a service is registered.
    /// </summary>
    public bool IsRegistered<TService>() where TService : class
    {
        return _services.ContainsKey(typeof(TService));
    }

    /// <summary>
    /// Clears all registrations. Useful for testing.
    /// </summary>
    public void Clear()
    {
        _services.Clear();
    }
}
