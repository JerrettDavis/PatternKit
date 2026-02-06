using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace PatternKit.Examples.MediatorDemo;

/// <summary>
/// Pure (side-effect minimized) assembly scanner for mediator components. Discovers handler/behavior types,
/// produces immutable registration descriptors, then applies DI registrations in a single pass.
/// </summary>
internal static class MediatorAssemblyScanner
{
    private enum RegistrationKind { Command, Notification, Stream, Behavior }

    private sealed record Registration(
        RegistrationKind Kind,
        Type Implementation,
        Type Interface,
        Type? RequestType,
        Type? ResponseOrItemType,
        bool RegisterWithDi,
        bool IsOpenBehavior);

    private interface IStrategy
    {
        Registration? Create(Type impl, Type iface);
    }

    private sealed class CommandStrategy : IStrategy
    {
        public Registration? Create(Type impl, Type iface)
        {
            if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(ICommandHandler<,>)) return null;
            var args = iface.GetGenericArguments();
            return new(RegistrationKind.Command, impl, iface, args[0], args[1], RegisterWithDi: true, IsOpenBehavior: false);
        }
    }

    private sealed class NotificationStrategy : IStrategy
    {
        public Registration? Create(Type impl, Type iface)
        {
            if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(INotificationHandler<>)) return null;
            var arg = iface.GetGenericArguments()[0];
            return new(RegistrationKind.Notification, impl, iface, arg, null, RegisterWithDi: true, IsOpenBehavior: false);
        }
    }

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    private sealed class StreamStrategy : IStrategy
    {
        public Registration? Create(Type impl, Type iface)
        {
            if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IStreamRequestHandler<,>)) return null;
            var args = iface.GetGenericArguments();
            return new(RegistrationKind.Stream, impl, iface, args[0], args[1], RegisterWithDi: true, IsOpenBehavior: false);
        }
    }
#endif

    private sealed class BehaviorStrategy : IStrategy
    {
        public Registration? Create(Type impl, Type iface)
        {
            if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IPipelineBehavior<,>)) return null;
            var args = iface.GetGenericArguments();
            var isOpen = impl.IsGenericTypeDefinition;
            // Open behaviors are resolved/closed manually later => don't DI-register the open definition.
            return new(RegistrationKind.Behavior, impl, iface, args[0], args[1], RegisterWithDi: !isOpen, IsOpenBehavior: isOpen);
        }
    }

    private static readonly IStrategy[] Strategies = CreateStrategies();
    private static IStrategy[] CreateStrategies()
    {
        var list = new List<IStrategy>
        {
            new CommandStrategy(),
            new NotificationStrategy(),
            new BehaviorStrategy()
        };
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        list.Add(new StreamStrategy());
#endif
        return list.ToArray();
    }

    /// <summary>
    /// Discover mediator registrations in the provided assemblies, build a registry, then apply DI registrations.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <param name="services">Service collection to populate.</param>
    public static MediatorRegistry Scan(IEnumerable<Assembly> assemblies, IServiceCollection services)
    {
        var regs = assemblies
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .SelectMany(impl => impl.GetInterfaces()
                .Where(i => i.IsGenericType)
                .Select(i => Strategies.Select(s => s.Create(impl, i)).FirstOrDefault(r => r is not null)))
            .Where(r => r is not null)
            .Cast<Registration>()
            .ToList();

        // Apply DI registrations (single pass) without duplicates.
        var diPairs = new HashSet<(Type iface, Type impl)>();
        foreach (var r in regs.Where(r => r.RegisterWithDi))
        {
            var key = (r.Interface, r.Implementation);
            if (!diPairs.Add(key))
                continue;

            services.AddTransient(r.Interface, r.Implementation);
            services.AddTransient(r.Implementation); // allow resolving by concrete type
        }

        var commands = regs.Where(r => r.Kind == RegistrationKind.Command)
            .Select(r => (r.RequestType!, r.ResponseOrItemType!, r.Implementation))
            .ToArray();
        var notifications = regs.Where(r => r.Kind == RegistrationKind.Notification)
            .Select(r => (r.RequestType!, r.Implementation))
            .ToArray();
        var streams = regs.Where(r => r.Kind == RegistrationKind.Stream)
            .Select(r => (r.RequestType!, r.ResponseOrItemType!, r.Implementation))
            .ToArray();
        var behaviors = regs.Where(r => r.Kind == RegistrationKind.Behavior)
            .Select(r => (r.Implementation, r.RequestType!, r.ResponseOrItemType!))
            .ToArray();

        return new MediatorRegistry
        {
            Commands = commands,
            Notifications = notifications,
            Streams = streams,
            Behaviors = behaviors
        };
    }
}