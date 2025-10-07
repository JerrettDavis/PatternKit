using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace PatternKit.Structural.Facade;

/// <summary>
/// Typed, compile-time safe facade that uses an interface contract to eliminate magic strings.
/// Provides a simpler alternative to string-based facades with full IntelliSense and refactoring support.
/// </summary>
/// <typeparam name="TFacadeInterface">The interface defining the facade's operations.</typeparam>
/// <remarks>
/// <para>
/// <b>Usage Pattern</b>: Define an interface for your facade contract, then use the fluent builder
/// to map each method to its implementation handler.
/// </para>
/// <para>
/// <b>Advantages over string-based facade</b>:
/// <list type="bullet">
///   <item><description>Compile-time safety - typos caught at compile time.</description></item>
///   <item><description>IntelliSense support - IDE shows available operations.</description></item>
///   <item><description>Refactoring friendly - rename methods safely.</description></item>
///   <item><description>Type-safe parameters - method signatures enforce correct types.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// // 1. Define your interface
/// public interface ICalculator
/// {
///     int Add(int a, int b);
///     int Multiply(int a, int b);
/// }
///
/// // 2. Build using fluent API
/// var calc = TypedFacade&lt;ICalculator&gt;.Create()
///     .Map(x => x.Add, (int a, int b) => a + b)
///     .Map(x => x.Multiply, (int a, int b) => a * b)
///     .Build();
///
/// var result = calc.Add(5, 3); // Type-safe: 8
/// </code>
/// </example>
public static class TypedFacade<TFacadeInterface>
    where TFacadeInterface : class
{
    /// <summary>
    /// Creates a new builder for constructing a typed facade.
    /// </summary>
    /// <returns>A new <see cref="Builder"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="TFacadeInterface"/> is not an interface.</exception>
    public static Builder Create()
    {
        if (!typeof(TFacadeInterface).IsInterface)
            throw new InvalidOperationException($"{typeof(TFacadeInterface).Name} must be an interface.");
        
        return new Builder();
    }

    /// <summary>
    /// Fluent builder for <see cref="TypedFacade{TFacadeInterface}"/>.
    /// </summary>
    public sealed class Builder
    {
        private readonly Dictionary<MethodInfo, Delegate> _handlers = new();

        internal Builder() { }

        /// <summary>
        /// Maps a method with no parameters to its implementation handler.
        /// </summary>
        public Builder Map<TResult>(
            Expression<Func<TFacadeInterface, Func<TResult>>> methodSelector,
            Func<TResult> handler)
        {
            var method = ExtractMethodInfo(methodSelector);
            ValidateMethodSignature(method, typeof(TResult));
            
            if (_handlers.ContainsKey(method))
                throw new ArgumentException($"Method '{method.Name}' is already mapped.", nameof(methodSelector));

            _handlers[method] = handler;
            return this;
        }

        /// <summary>
        /// Maps a method with one parameter to its implementation handler.
        /// </summary>
        public Builder Map<T1, TResult>(
            Expression<Func<TFacadeInterface, Func<T1, TResult>>> methodSelector,
            Func<T1, TResult> handler)
        {
            var method = ExtractMethodInfo(methodSelector);
            ValidateMethodSignature(method, typeof(TResult), typeof(T1));
            
            if (_handlers.ContainsKey(method))
                throw new ArgumentException($"Method '{method.Name}' is already mapped.", nameof(methodSelector));

            _handlers[method] = handler;
            return this;
        }

        /// <summary>
        /// Maps a method with two parameters to its implementation handler.
        /// </summary>
        public Builder Map<T1, T2, TResult>(
            Expression<Func<TFacadeInterface, Func<T1, T2, TResult>>> methodSelector,
            Func<T1, T2, TResult> handler)
        {
            var method = ExtractMethodInfo(methodSelector);
            ValidateMethodSignature(method, typeof(TResult), typeof(T1), typeof(T2));
            
            if (_handlers.ContainsKey(method))
                throw new ArgumentException($"Method '{method.Name}' is already mapped.", nameof(methodSelector));

            _handlers[method] = handler;
            return this;
        }

        /// <summary>
        /// Maps a method with three parameters to its implementation handler.
        /// </summary>
        public Builder Map<T1, T2, T3, TResult>(
            Expression<Func<TFacadeInterface, Func<T1, T2, T3, TResult>>> methodSelector,
            Func<T1, T2, T3, TResult> handler)
        {
            var method = ExtractMethodInfo(methodSelector);
            ValidateMethodSignature(method, typeof(TResult), typeof(T1), typeof(T2), typeof(T3));
            
            if (_handlers.ContainsKey(method))
                throw new ArgumentException($"Method '{method.Name}' is already mapped.", nameof(methodSelector));

            _handlers[method] = handler;
            return this;
        }

        /// <summary>
        /// Maps a method with four parameters to its implementation handler.
        /// </summary>
        public Builder Map<T1, T2, T3, T4, TResult>(
            Expression<Func<TFacadeInterface, Func<T1, T2, T3, T4, TResult>>> methodSelector,
            Func<T1, T2, T3, T4, TResult> handler)
        {
            var method = ExtractMethodInfo(methodSelector);
            ValidateMethodSignature(method, typeof(TResult), typeof(T1), typeof(T2), typeof(T3), typeof(T4));
            
            if (_handlers.ContainsKey(method))
                throw new ArgumentException($"Method '{method.Name}' is already mapped.", nameof(methodSelector));

            _handlers[method] = handler;
            return this;
        }

        /// <summary>
        /// Builds the typed facade instance.
        /// </summary>
        /// <returns>An instance implementing <typeparamref name="TFacadeInterface"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not all interface methods are mapped.</exception>
        public TFacadeInterface Build()
        {
            // Validate all methods are implemented
            var interfaceMethods = typeof(TFacadeInterface)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName); // Exclude property getters/setters

            var unmappedMethods = interfaceMethods
                .Where(m => !_handlers.ContainsKey(m))
                .ToList();

            if (unmappedMethods.Any())
            {
                var methodNames = string.Join(", ", unmappedMethods.Select(m => m.Name));
                throw new InvalidOperationException(
                    $"Not all methods are mapped. Missing: {methodNames}");
            }

            // Create dynamic proxy instance using RealProxy fallback
            return TypedFacadeProxyFactory<TFacadeInterface>.CreateProxy(_handlers);
        }

        private static MethodInfo ExtractMethodInfo<T>(Expression<T> expression)
        {
            if (expression.Body is not UnaryExpression unary)
                throw new ArgumentException("Expression must be a method selector.", nameof(expression));

            if (unary.Operand is not MethodCallExpression methodCall)
                throw new ArgumentException("Expression must select a method.", nameof(expression));

            if (methodCall.Object is not ConstantExpression constant)
                throw new ArgumentException("Invalid method selector expression.", nameof(expression));

            if (constant.Value is not MethodInfo method)
                throw new ArgumentException("Expression does not reference a valid method.", nameof(expression));

            return method;
        }

        private static void ValidateMethodSignature(MethodInfo method, Type returnType, params Type[] parameterTypes)
        {
            if (method.ReturnType != returnType)
                throw new ArgumentException(
                    $"Method '{method.Name}' return type mismatch. Expected: {returnType.Name}, Actual: {method.ReturnType.Name}");

            var methodParams = method.GetParameters();
            if (methodParams.Length != parameterTypes.Length)
                throw new ArgumentException(
                    $"Method '{method.Name}' parameter count mismatch. Expected: {parameterTypes.Length}, Actual: {methodParams.Length}");

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                if (methodParams[i].ParameterType != parameterTypes[i])
                    throw new ArgumentException(
                        $"Method '{method.Name}' parameter {i} type mismatch. Expected: {parameterTypes[i].Name}, Actual: {methodParams[i].ParameterType.Name}");
            }
        }
    }
}

/// <summary>
/// Factory for creating typed facade proxy instances using System.Reflection.DispatchProxy where available,
/// or falling back to a reflection-based approach for older frameworks.
/// </summary>
internal static class TypedFacadeProxyFactory<TInterface> where TInterface : class
{
    public static TInterface CreateProxy(Dictionary<MethodInfo, Delegate> handlers)
    {
#if NETSTANDARD2_0
        // For .NET Standard 2.0, use a simple object wrapper with reflection
        return new ReflectionProxy(handlers).GetTransparentProxy();
#else
        // For modern .NET, use DispatchProxy
        var proxy = System.Reflection.DispatchProxy.Create<TInterface, TypedFacadeDispatchProxy<TInterface>>();
        ((TypedFacadeDispatchProxy<TInterface>)(object)proxy).Initialize(handlers);
        return proxy;
#endif
    }

#if NETSTANDARD2_0
    private sealed class ReflectionProxy
    {
        private readonly Dictionary<string, Delegate> _handlersByName;

        public ReflectionProxy(Dictionary<MethodInfo, Delegate> handlers)
        {
            _handlersByName = handlers.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
        }

        public TInterface GetTransparentProxy()
        {
            // For .NET Standard 2.0, we need to use Castle.DynamicProxy or require manual implementation
            // Since we want zero dependencies, we'll throw an instructive error
            throw new PlatformNotSupportedException(
                $"TypedFacade<{typeof(TInterface).Name}> requires .NET Standard 2.1 or higher for automatic proxy generation. " +
                "For .NET Standard 2.0, please use TypedFacadeBase<T> with manual implementation.");
        }
    }
#endif
}

#if !NETSTANDARD2_0
/// <summary>
/// DispatchProxy implementation for typed facades.
/// </summary>
/// <typeparam name="TInterface">The interface type to proxy.</typeparam>
public class TypedFacadeDispatchProxy<TInterface> : System.Reflection.DispatchProxy
    where TInterface : class
{
    private Dictionary<MethodInfo, Delegate> _handlers = new();

    internal void Initialize(Dictionary<MethodInfo, Delegate> handlers)
    {
        _handlers = handlers;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
            throw new InvalidOperationException("Target method is null.");

        if (!_handlers.TryGetValue(targetMethod, out var handler))
            throw new InvalidOperationException($"No handler registered for method '{targetMethod.Name}'");

        try
        {
            return handler.DynamicInvoke(args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Unwrap TargetInvocationException to preserve the original exception
            throw ex.InnerException;
        }
    }
}
#endif
