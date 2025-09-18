using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Behavioral.Mediator;

namespace PatternKit.Examples.MediatorDemo;

// Marker request/response abstractions (MediatR-like)
public interface ICommand<TResponse> { }
public interface INotification { }
public interface IStreamRequest<TItem> { }

// Handlers
public interface ICommandHandler<in TRequest, TResponse> where TRequest : ICommand<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken ct);
}

public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    ValueTask Handle(TNotification notification, CancellationToken ct);
}

public interface IStreamRequestHandler<in TRequest, out TItem> where TRequest : IStreamRequest<TItem>
{
    IAsyncEnumerable<TItem> Handle(TRequest request, CancellationToken ct);
}

// Pipeline behavior (request/response)
public interface IPipelineBehavior<TRequest, TResponse> where TRequest : ICommand<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken ct, Func<TRequest, CancellationToken, ValueTask<TResponse>> next);
}

// Simple IMediator facade used by the demo
public interface IAppMediator
{
    ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> request, CancellationToken ct = default);
    ValueTask Publish(INotification notification, CancellationToken ct = default);
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    IAsyncEnumerable<TItem> Stream<TItem>(IStreamRequest<TItem> request, CancellationToken ct = default);
#endif
}

internal sealed class MediatorRegistry
{
    public required (Type Request, Type Response, Type Handler)[] Commands { get; init; }
    public required (Type Notification, Type Handler)[] Notifications { get; init; }
    public required (Type Request, Type Item, Type Handler)[] Streams { get; init; }
    public required (Type BehaviorType, Type Request, Type Response)[] Behaviors { get; init; }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPatternKitMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
            assemblies = [Assembly.GetExecutingAssembly()];

        var commands = new List<(Type, Type, Type)>();
        var notes = new List<(Type, Type)>();
        var streams = new List<(Type, Type, Type)>();
        var behaviors = new List<(Type, Type, Type)>();

        foreach (var asm in assemblies.Distinct())
        {
            foreach (var t in asm.GetTypes())
            {
                if (t.IsAbstract || t.IsInterface) continue;
                foreach (var it in t.GetInterfaces())
                {
                    if (!it.IsGenericType)
                        continue;
                    
                    var def = it.GetGenericTypeDefinition();
                    var args = it.GetGenericArguments();

                    if (def == typeof(ICommandHandler<,>))
                    {
                        services.AddTransient(it, t);
                        services.AddTransient(t); // allow resolving by concrete type if needed
                        commands.Add((args[0], args[1], t));
                    }
                    else if (def == typeof(INotificationHandler<>))
                    {
                        services.AddTransient(it, t);
                        services.AddTransient(t); // allow resolving by concrete type if needed
                        notes.Add((args[0], t));
                    }
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
                    else if (def == typeof(IStreamRequestHandler<,>))
                    {
                        services.AddTransient(it, t);
                        services.AddTransient(t); // allow resolving by concrete type if needed
                        streams.Add((args[0], args[1], t));
                    }
#endif
                    else if (def == typeof(IPipelineBehavior<,>))
                    {
                        // Behaviors can be open generic or closed; avoid invalid DI registrations for open generics.
                        if (t.IsGenericTypeDefinition)
                        {
                            // Skip registering open-generic behavior as a concrete service mapping.
                            // We'll resolve a closed instance via ActivatorUtilities when executing the pipeline.
                        }
                        else
                        {
                            // Closed/concrete behavior: allow DI to construct it if needed.
                            services.AddTransient(it, t);
                            services.AddTransient(t);
                        }
                        behaviors.Add((t, args[0], args[1]));
                    }
                }
            }
        }

        services.AddSingleton(new MediatorRegistry
        {
            Commands = commands.ToArray(),
            Notifications = notes.ToArray(),
            Streams = streams.ToArray(),
            Behaviors = behaviors.ToArray(),
        });

        services.AddScoped<IAppMediator, AppMediator>();
        return services;
    }
}

internal sealed class AppMediator : IAppMediator
{
    private readonly IServiceProvider _sp;
    private readonly Mediator _mediator;

    private readonly Dictionary<Type, Func<object, CancellationToken, ValueTask<object?>>> _sendInvokers = new();
    private readonly Dictionary<Type, Func<object, CancellationToken, ValueTask>> _publishInvokers = new();
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    private readonly Dictionary<Type, Func<object, CancellationToken, IAsyncEnumerable<object?>>> _streamInvokers = new();
#endif

    public AppMediator(IServiceProvider sp, MediatorRegistry reg)
    {
        _sp = sp;
        var b = Mediator.Create();

        foreach (var (reqT, resT, handlerT) in reg.Commands)
        {
            RegisterCommand(b, reqT, resT, handlerT);
            _sendInvokers[reqT] = CreateSendInvoker(reqT, resT);
        }

        foreach (var (noteT, handlerT) in reg.Notifications)
        {
            RegisterNotification(b, noteT, handlerT);
            _publishInvokers[noteT] = CreatePublishInvoker(noteT);
        }

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        foreach (var (reqT, itemT, handlerT) in reg.Streams)
        {
            RegisterStream(b, reqT, itemT, handlerT);
            _streamInvokers[reqT] = CreateStreamInvoker(reqT, itemT);
        }
#endif

        foreach (var (behaviorT, requestT, responseT) in reg.Behaviors)
        {
            if (behaviorT.IsGenericTypeDefinition || requestT.ContainsGenericParameters || responseT.ContainsGenericParameters)
                RegisterBehaviorOpenGeneric(b, behaviorT);
            else
                RegisterBehavior(b, behaviorT, requestT, responseT);
        }

        _mediator = b.Build();
    }

    public async ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> request, CancellationToken ct = default)
    {
        var t = request.GetType();
        if (!_sendInvokers.TryGetValue(t, out var inv))
            throw new InvalidOperationException($"No handler for request type {t}");
        var obj = await inv(request, ct).ConfigureAwait(false);
        return obj is TResponse r ? r : default!;
    }

    public ValueTask Publish(INotification notification, CancellationToken ct = default)
        => _publishInvokers.TryGetValue(notification.GetType(), out var inv) 
            ? inv(notification, ct) 
            : default;

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    public IAsyncEnumerable<TItem> Stream<TItem>(IStreamRequest<TItem> request, CancellationToken ct = default)
    {
        var t = request.GetType();
        return _streamInvokers.TryGetValue(t, out var inv) 
            ? Adapt<TItem>(inv(request, ct), ct) 
            : throw new InvalidOperationException($"No stream handler for request type {t}");

        static async IAsyncEnumerable<TItem> Adapt<TItem>(IAsyncEnumerable<object?> src, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var o in src.WithCancellation(token).ConfigureAwait(false))
                yield return o is TItem i ? i : default!;
        }
    }
#endif

    // ---- build typed invokers ----
    private Func<object, CancellationToken, ValueTask<object?>> CreateSendInvoker(Type reqT, Type resT)
    {
        var mi = typeof(AppMediator).GetMethod(nameof(CreateSendInvokerGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Func<object, CancellationToken, ValueTask<object?>>)mi.MakeGenericMethod(reqT, resT).Invoke(this, null)!;
    }

    private Func<object, CancellationToken, ValueTask<object?>> CreateSendInvokerGeneric<TRequest, TResponse>() where TRequest : ICommand<TResponse>
        => (obj, ct) => BoxHelper.Box(_mediator.Send<TRequest, TResponse>((TRequest)obj, ct));

    private Func<object, CancellationToken, ValueTask> CreatePublishInvoker(Type noteT)
    {
        var mi = typeof(AppMediator).GetMethod(nameof(CreatePublishInvokerGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Func<object, CancellationToken, ValueTask>)mi.MakeGenericMethod(noteT).Invoke(this, null)!;
    }

    private Func<object, CancellationToken, ValueTask> CreatePublishInvokerGeneric<TNotification>() where TNotification : INotification
        => (obj, ct) => _mediator.Publish((TNotification)obj, ct);

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    private Func<object, CancellationToken, IAsyncEnumerable<object?>> CreateStreamInvoker(Type reqT, Type itemT)
    {
        var mi = typeof(AppMediator).GetMethod(nameof(CreateStreamInvokerGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Func<object, CancellationToken, IAsyncEnumerable<object?>>)mi.MakeGenericMethod(reqT, itemT).Invoke(this, null)!;
    }

    private Func<object, CancellationToken, IAsyncEnumerable<object?>> CreateStreamInvokerGeneric<TRequest, TItem>() where TRequest : IStreamRequest<TItem>
        => (obj, ct) => Adapt(_mediator.Stream<TRequest, TItem>((TRequest)obj, ct), ct);

    private static async IAsyncEnumerable<object?> Adapt<T>(IAsyncEnumerable<T> src, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var x in src.WithCancellation(ct).ConfigureAwait(false))
            yield return x;
    }
#endif

    private void RegisterCommand(Mediator.Builder b, Type reqT, Type resT, Type handlerT)
    {
        var method = typeof(AppMediator).GetMethod(nameof(RegisterCommandGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(reqT, resT, handlerT);
        method.Invoke(this, new object[] { b });
    }

    private void RegisterNotification(Mediator.Builder b, Type noteT, Type handlerT)
    {
        var method = typeof(AppMediator).GetMethod(nameof(RegisterNotificationGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(noteT, handlerT);
        method.Invoke(this, new object[] { b });
    }

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    private void RegisterStream(Mediator.Builder b, Type reqT, Type itemT, Type handlerT)
    {
        var method = typeof(AppMediator).GetMethod(nameof(RegisterStreamGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(reqT, itemT, handlerT);
        method.Invoke(this, new object[] { b });
    }
#endif

    private void RegisterBehavior(Mediator.Builder b, Type behaviorT, Type requestT, Type responseT)
    {
        var method = typeof(AppMediator).GetMethod(nameof(RegisterBehaviorGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(behaviorT, requestT, responseT);
        method.Invoke(this, new object[] { b });
    }

    private void RegisterBehaviorOpenGeneric(Mediator.Builder b, Type openBehavior)
    {
        b.Whole((in reqObj, ct, next) =>
        {
            var reqType = reqObj.GetType();
            var cmdIface = reqType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
            if (cmdIface is null)
                return next(in reqObj, ct);
            var resType = cmdIface.GetGenericArguments()[0];
            var closedBehavior = openBehavior.IsGenericTypeDefinition ? openBehavior.MakeGenericType(reqType, resType) : openBehavior;
            // Resolve behavior via ActivatorUtilities to avoid requiring concrete registrations
            var beh = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(_sp, closedBehavior);
            var mi = typeof(AppMediator).GetMethod(nameof(InvokeBehavior), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(reqType, resType, closedBehavior);
            return (ValueTask<object?>)mi.Invoke(this, new object[] { beh, reqObj, ct, next })!;
        });
    }

    private ValueTask<object?> InvokeBehavior<TRequest, TResponse, TBehavior>(object behavior, object reqObj, CancellationToken ct, Mediator.MediatorNext next)
        where TRequest : ICommand<TResponse>
        where TBehavior : IPipelineBehavior<TRequest, TResponse>
    {
        var req = (TRequest)reqObj;
        return BoxHelper.Box(((TBehavior)behavior).Handle(req, ct, TypedNext));

        ValueTask<TResponse> TypedNext(TRequest r, CancellationToken c)
        {
            object o = r;
            return MediatorHelpersUnbox.Unbox<TResponse>(next(in o, c));
        }
    }

    // ----- Generic helpers (closed at runtime) -----

    private void RegisterCommandGeneric<TRequest, TResponse, THandler>(Mediator.Builder b)
        where TRequest : ICommand<TResponse>
        where THandler : class, ICommandHandler<TRequest, TResponse>
    {
        b.Command((in TRequest req, CancellationToken ct) => _sp.GetRequiredService<ICommandHandler<TRequest, TResponse>>().Handle(req, ct));
    }

    private void RegisterNotificationGeneric<TNotification, THandler>(Mediator.Builder b)
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification>
    {
        b.Notification((in TNotification n, CancellationToken ct) =>
        {
            // Fan out to all registered handlers for this notification type
            return FanoutAsync(n, ct);

            async ValueTask FanoutAsync(TNotification note, CancellationToken token)
            {
                // Resolve IEnumerable each time to respect scoped lifetimes
                var handlers = _sp.GetServices<INotificationHandler<TNotification>>();
                foreach (var h in handlers)
                    await h.Handle(note, token).ConfigureAwait(false);
            }
        });
    }

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    private void RegisterStreamGeneric<TRequest, TItem, THandler>(Mediator.Builder b)
        where TRequest : IStreamRequest<TItem>
        where THandler : class, IStreamRequestHandler<TRequest, TItem>
    {
        b.Stream((in TRequest r, CancellationToken ct) => _sp.GetRequiredService<IStreamRequestHandler<TRequest, TItem>>().Handle(r, ct));
    }
#endif

    private void RegisterBehaviorGeneric<TBehavior, TRequest, TResponse>(Mediator.Builder b)
        where TBehavior : class, IPipelineBehavior<TRequest, TResponse>
        where TRequest : ICommand<TResponse>
    {
        b.Whole((in reqObj, ct, next) =>
        {
            if (reqObj is TRequest req)
            {
                // Resolve behavior via ActivatorUtilities to avoid requiring concrete registrations
                var beh = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<TBehavior>(_sp);
                return BoxHelper.Box(beh.Handle(req, ct, TypedNext));

                ValueTask<TResponse> TypedNext(TRequest r, CancellationToken c)
                {
                    object o = r!;
                    return MediatorHelpersUnbox.Unbox<TResponse>(next(in o, c));
                }
            }
            return next(in reqObj, ct);
        });
    }
}

internal static class MediatorHelpersUnbox
{
    public static ValueTask<T> Unbox<T>(ValueTask<object?> vt)
    {
        if (vt.IsCompletedSuccessfully)
            return new ValueTask<T>((T)vt.Result!);
        return Await(vt);
        static async ValueTask<T> Await(ValueTask<object?> v)
        {
            var o = await v.ConfigureAwait(false);
            return o is null ? default! : (T)o;
        }
    }
}

internal static class BoxHelper
{
    public static ValueTask<object?> Box<T>(ValueTask<T> vt)
    {
        return vt.IsCompletedSuccessfully ? new ValueTask<object?>(vt.Result) : Await(vt);

        static async ValueTask<object?> Await(ValueTask<T> v)
        {
            var r = await v.ConfigureAwait(false);
            return r;
        }
    }

    public static ValueTask<object?> UnsafeBox<T>(ValueTask vt)
    {
        // ValueTask (non-generic) boxing helper for reflected calls; used only for Send wrapper
        return Await(vt);
        static async ValueTask<object?> Await(ValueTask v)
        {
            await v.ConfigureAwait(false);
            return null;
        }
    }
}
