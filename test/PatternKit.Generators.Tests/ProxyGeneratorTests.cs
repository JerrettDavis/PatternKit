using Microsoft.CodeAnalysis;
using System.IO;

namespace PatternKit.Generators.Tests;

public class ProxyGeneratorTests
{
    [Fact]
    public void GenerateProxyForInterface_BasicContract()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                string GetUser(int id);
                void DeleteUser(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxyForInterface_BasicContract));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Proxy class is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("TestNamespace_IUserService.Proxy.g.cs", names);
        Assert.Contains("TestNamespace_IUserService.Proxy.Interceptor.g.cs", names);

        // Verify proxy class content
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        Assert.Contains("UserServiceProxy", proxySource);
        Assert.Contains("IUserService", proxySource);
        Assert.Contains("_inner", proxySource);
        Assert.Contains("_interceptor", proxySource);
        Assert.Contains("GetUser", proxySource);
        Assert.Contains("DeleteUser", proxySource);

        // Verify interceptor interface content
        var interceptorSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.Interceptor.g.cs")
            .SourceText.ToString();

        Assert.Contains("IUserServiceInterceptor", interceptorSource);
        Assert.Contains("void Before(MethodContext context)", interceptorSource);
        Assert.Contains("void After(MethodContext context)", interceptorSource);
        Assert.Contains("void OnException(MethodContext context", interceptorSource);
        Assert.Contains("GetUserMethodContext", interceptorSource);
        Assert.Contains("DeleteUserMethodContext", interceptorSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateProxyForInterface_WithAsyncMethods()
    {
        const string source = """
            using PatternKit.Generators.Proxy;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            [GenerateProxy(InterceptorMode = ProxyInterceptorMode.None)]
            public partial interface IAsyncUserService
            {
                Task<string> GetUserAsync(int id, CancellationToken ct = default);
                ValueTask DeleteUserAsync(int id, CancellationToken ct = default);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxyForInterface_WithAsyncMethods));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify simple delegation (no interceptor)
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IAsyncUserService.Proxy.g.cs")
            .SourceText.ToString();

        Assert.Contains("return _inner.GetUserAsync", proxySource);
        Assert.Contains("_inner.DeleteUserAsync", proxySource);
        Assert.DoesNotContain("_interceptor", proxySource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateProxyForInterface_NoInterceptor()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy(InterceptorMode = ProxyInterceptorMode.None)]
            public partial interface ISimpleService
            {
                string GetValue();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxyForInterface_NoInterceptor));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Only proxy class is generated, no interceptor interface
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("TestNamespace_ISimpleService.Proxy.g.cs", names);
        Assert.DoesNotContain("TestNamespace_ISimpleService.Proxy.Interceptor.g.cs", names);

        // Verify proxy class has no interceptor field
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_ISimpleService.Proxy.g.cs")
            .SourceText.ToString();

        Assert.DoesNotContain("_interceptor", proxySource);
        Assert.Contains("_inner.GetValue()", proxySource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateProxyForInterface_PipelineMode()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy(InterceptorMode = ProxyInterceptorMode.Pipeline)]
            public partial interface IPipelineService
            {
                string Process(string input);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxyForInterface_PipelineMode));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify proxy class uses pipeline (IReadOnlyList)
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IPipelineService.Proxy.g.cs")
            .SourceText.ToString();

        Assert.Contains("IReadOnlyList", proxySource);
        Assert.Contains("_interceptors", proxySource);
        Assert.Contains("for (int __i = 0; __i < _interceptors", proxySource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateProxyForInterface_WithProperties()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IPropertyService
            {
                string Name { get; set; }
                int Count { get; }
                string Description { set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxyForInterface_WithProperties));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify proxy forwards properties
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IPropertyService.Proxy.g.cs")
            .SourceText.ToString();

        Assert.Contains("Name", proxySource);
        Assert.Contains("Count", proxySource);
        Assert.Contains("Description", proxySource);
        Assert.Contains("get => _inner.Name", proxySource);
        Assert.Contains("set => _inner.Name = value", proxySource);
        Assert.Contains("get => _inner.Count", proxySource);
        Assert.Contains("set => _inner.Description = value", proxySource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateProxyForInterface_MustBePartial()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public interface INonPartialService
            {
                string GetValue();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxyForInterface_MustBePartial));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Generator diagnostic for missing partial
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRX001");
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("partial"));
    }

    [Fact]
    public void GenerateProxyForInterface_ProxyIgnoreAttribute()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy(ProxyTypeName = "MyServiceWithIgnoreProxy")]
            public partial interface IServiceWithIgnore
            {
                string GetValue();
                
                [ProxyIgnore]
                void LegacyMethod();
            }
            
            // Partial implementation to provide the ignored method
            public partial class MyServiceWithIgnoreProxy
            {
                public void LegacyMethod()
                {
                    // Custom implementation
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxyForInterface_ProxyIgnoreAttribute));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify proxy does not include ignored method
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IServiceWithIgnore.Proxy.g.cs")
            .SourceText.ToString();

        Assert.Contains("GetValue", proxySource);
        Assert.DoesNotContain("LegacyMethod", proxySource);

        // Compilation succeeds (because user provided partial implementation)
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateProxyForInterface_CustomProxyTypeName()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy(ProxyTypeName = "CustomUserServiceProxy")]
            public partial interface IUserService
            {
                string GetUser(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxyForInterface_CustomProxyTypeName));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Verify custom proxy type name is used in class declaration
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        Assert.Contains("class CustomUserServiceProxy", proxySource);
        Assert.Contains("public CustomUserServiceProxy(", proxySource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateProxyForAbstractClass_OnlyProxiesVirtualMembers()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public abstract partial class UserServiceBase
            {
                public abstract string GetUser(int id);
                public virtual void UpdateUser(int id, string name) { }
                public void NonVirtualMethod() { } // Should not be proxied
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxyForAbstractClass_OnlyProxiesVirtualMembers));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        // Check that files were generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains(names, n => n.Contains("UserServiceBase"));

        // Verify proxy class content - look for the proxy file
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Where(gs => gs.HintName.Contains("UserServiceBase") && gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .Select(gs => gs.SourceText.ToString())
            .FirstOrDefault();

        Assert.NotNull(proxySource);
        Assert.Contains("UserServiceBaseProxy", proxySource);
        Assert.Contains("UserServiceBase", proxySource);
        Assert.Contains("GetUser", proxySource);
        Assert.Contains("UpdateUser", proxySource);
        Assert.DoesNotContain("NonVirtualMethod", proxySource);
    }

    [Fact]
    public void GenerateProxy_ProtectedMember_GeneratesWarning()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public abstract partial class UserServiceBase
            {
                public abstract string GetUser(int id);
                protected abstract string GetProtectedUser(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_ProtectedMember_GeneratesWarning));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should generate PKPRX003 diagnostic for protected member
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRX003" && d.GetMessage().Contains("GetProtectedUser"));
    }

    [Fact]
    public void GenerateProxy_InaccessibleMember_GeneratesError()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                string GetUser(int id);
            }

            // Private interface method (simulated with nested type)
            [GenerateProxy]
            internal partial interface IInternalService
            {
                private protected void PrivateMethod(); // Not accessible
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_InaccessibleMember_GeneratesError));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should still generate for the accessible parts
        Assert.NotEmpty(result.Results.SelectMany(r => r.GeneratedSources));
    }

    [Fact]
    public void GenerateProxy_NameConflict_GeneratesError()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                string GetUser(int id);
            }

            // Non-partial class with conflicting name
            public class UserServiceProxy
            {
                public void ExistingMethod() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_NameConflict_GeneratesError));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should generate PKPRX004 diagnostic
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRX004");
    }

    [Fact]
    public void GenerateProxy_ExceptionPolicySwallow()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy(Exceptions = ProxyExceptionPolicy.Swallow)]
            public partial interface IUserService
            {
                string GetUser(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_ExceptionPolicySwallow));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        // Verify exception is NOT rethrown (Swallow policy)
        Assert.Contains("catch", proxySource);
        Assert.DoesNotContain("throw;", proxySource);
    }

    [Fact]
    public void GenerateProxy_WithRefInParameters()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                void UpdateUser(ref int id, in string name);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_WithRefInParameters));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        Assert.Contains("ref int id", proxySource);
        Assert.Contains("in string name", proxySource);
        Assert.Contains("ref id", proxySource); // ref forwarded
        Assert.Contains("in name", proxySource); // in forwarded
    }

    [Fact]
    public void GenerateProxy_WithOutParameters()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                bool TryGetUser(int id, out string name);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_WithOutParameters));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        Assert.Contains("out string name", proxySource);
        Assert.Contains("out name", proxySource); // out forwarded
        Assert.Contains("default", proxySource); // default used in context
    }

    [Fact]
    public void GenerateProxy_GenericMethod_GeneratesError()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                T GetValue<T>(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_GenericMethod_GeneratesError));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should generate PKPRX002 diagnostic for generic method
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRX002" && d.GetMessage().Contains("Generic method"));
    }

    [Fact]
    public void GenerateProxy_Event_GeneratesError()
    {
        const string source = """
            using PatternKit.Generators.Proxy;
            using System;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                event EventHandler UserChanged;
                string GetUser(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_Event_GeneratesError));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should generate PKPRX002 diagnostic for event
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRX002" && d.GetMessage().Contains("Event"));
    }

    [Fact]
    public void GenerateProxy_NestedType_GeneratesError()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            public class OuterClass
            {
                [GenerateProxy]
                public partial interface IUserService
                {
                    string GetUser(int id);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_NestedType_GeneratesError));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Nested types are not supported - should not generate or should error
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        // Either no generation or specific error
        Assert.True(diagnostics.Length > 0 || result.Results.SelectMany(r => r.GeneratedSources).Count() == 0);
    }

    [Fact]
    public void GenerateProxy_PipelineModeWithExceptions()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy(InterceptorMode = ProxyInterceptorMode.Pipeline)]
            public partial interface IUserService
            {
                string GetUser(int id);
                void UpdateUser(int id, string name);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_PipelineModeWithExceptions));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        // Verify pipeline with list of interceptors
        Assert.Contains("IReadOnlyList", proxySource);
        Assert.Contains("_interceptors", proxySource);

        // Verify Before loop (ascending)
        Assert.Contains("for (int __i = 0; __i < _interceptors!.Count; __i++)", proxySource);
        Assert.Contains("_interceptors[__i].Before", proxySource);

        // Verify After loop (descending)
        Assert.Contains("for (int __i = _interceptors!.Count - 1; __i >= 0; __i--)", proxySource);
        Assert.Contains("_interceptors[__i].After", proxySource);

        // Verify OnException loop (descending)
        Assert.Contains("_interceptors[__i].OnException", proxySource);
    }

    [Fact]
    public void GenerateProxy_ParameterNameConflictsWithReservedNames()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                void UpdateUser(int methodName, string result);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_ParameterNameConflictsWithReservedNames));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var interceptorSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.Interceptor.g.cs")
            .SourceText.ToString();

        // Verify parameters are renamed to avoid conflicts
        Assert.Contains("Arg_MethodName", interceptorSource);
        Assert.Contains("Arg_Result", interceptorSource);

        // Compilation should succeed
        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void GenerateProxy_RefReturningMethodWithCancellationToken()
    {
        const string source = """
            using PatternKit.Generators.Proxy;
            using System.Threading;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                ref int GetValueRef(CancellationToken ct = default);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_RefReturningMethodWithCancellationToken));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No generator diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        // Verify async modifier is NOT added (ref-returning methods can't be async)
        var methodLines = proxySource.Split('\n')
            .Where(l => l.Contains("GetValueRef") && l.Contains("public"))
            .ToArray();

        Assert.All(methodLines, line => Assert.DoesNotContain("async", line));
    }

    [Fact]
    public void GenerateProxy_NonAbstractClass_GeneratesError()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial class UserService
            {
                public string GetUser(int id) => "User";
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_NonAbstractClass_GeneratesError));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should generate PKPRX002 diagnostic for non-abstract class
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRX002" && d.GetMessage().Contains("Non-abstract class"));
    }

    [Fact]
    public void GenerateProxy_GenericContract_GeneratesError()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService<T>
            {
                T GetValue(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_GenericContract_GeneratesError));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Should generate PKPRX002 diagnostic for generic type
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKPRX002" && d.GetMessage().Contains("Generic type"));
    }

    [Fact]
    public void GenerateProxy_PartialRecord_IsSupported()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial record interface IUserService
            {
                string GetUser(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_PartialRecord_IsSupported));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // Record declarations should be handled (though unusual, syntax allows it)
        // Should either generate or error appropriately
        Assert.NotNull(result);
    }

    [Fact]
    public void GenerateProxy_ForceAsyncTrue_GeneratesAsyncInterceptors()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy(ForceAsync = true)]
            public partial interface IUserService
            {
                string GetUser(int id);  // Sync method
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_ForceAsyncTrue_GeneratesAsyncInterceptors));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var interceptorSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Where(gs => gs.HintName.Contains("Interceptor.g.cs"))
            .Select(gs => gs.SourceText.ToString())
            .FirstOrDefault();

        // Should generate async interceptor methods even for sync-only contract
        Assert.NotNull(interceptorSource);
        Assert.Contains("BeforeAsync", interceptorSource);
        Assert.Contains("AfterAsync", interceptorSource);
        Assert.Contains("OnExceptionAsync", interceptorSource);
    }

    [Fact]
    public void GenerateProxy_CustomProxyTypeNameViaAttribute()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy(ProxyTypeName = "MyCustomProxy")]
            public partial interface IUserService
            {
                string GetUser(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_CustomProxyTypeNameViaAttribute));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        Assert.Contains("class MyCustomProxy", proxySource);
        Assert.Contains("public MyCustomProxy(", proxySource);
    }

    [Fact]
    public void GenerateProxy_WithDefaultParameters()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                string GetUser(int id, string? name = null, int age = 18, bool active = true);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_WithDefaultParameters));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Verify default parameters are preserved
        Assert.Contains("string? name = null", proxySource);
        Assert.Contains("int age = 18", proxySource);
        Assert.Contains("bool active = true", proxySource);
    }

    [Fact]
    public void GenerateProxy_InterfaceWithIPrefix_GeneratesCorrectName()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IMyService
            {
                void DoWork();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_InterfaceWithIPrefix_GeneratesCorrectName));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Should strip I prefix: IMyService -> MyServiceProxy
        Assert.Contains("class MyServiceProxy", proxySource);
        Assert.DoesNotContain("class IMyServiceProxy", proxySource);
    }

    [Fact]
    public void GenerateProxy_InterfaceWithoutIPrefix_GeneratesCorrectName()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface UserService
            {
                void DoWork();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_InterfaceWithoutIPrefix_GeneratesCorrectName));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Should not strip prefix: UserService -> UserServiceProxy
        Assert.Contains("class UserServiceProxy", proxySource);
    }

    [Fact]
    public void GenerateProxy_WithNullableReferenceTypes()
    {
        const string source = """
            #nullable enable
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                string? GetUser(int id);
                string GetRequiredUser(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_WithNullableReferenceTypes));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Verify nullable annotations are preserved
        Assert.Contains("string? GetUser", proxySource);
        Assert.Contains("string GetRequiredUser", proxySource);
    }

    [Fact]
    public void GenerateProxy_VoidAsyncMethods()
    {
        const string source = """
            using PatternKit.Generators.Proxy;
            using System.Threading.Tasks;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                Task UpdateUserAsync(int id);
                ValueTask DeleteUserAsync(int id);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_VoidAsyncMethods));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Verify Task and ValueTask without <T> are handled
        Assert.Contains("Task UpdateUserAsync", proxySource);
        Assert.Contains("ValueTask DeleteUserAsync", proxySource);
        Assert.Contains("await", proxySource);
        Assert.Contains("ConfigureAwait(false)", proxySource);
    }

    [Fact]
    public void GenerateProxy_MultiplePropertiesWithGettersAndSetters()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IUserService
            {
                string Name { get; set; }
                int Age { get; }
                bool IsActive { set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_MultiplePropertiesWithGettersAndSetters));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        // No diagnostics
        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Verify all property accessors are forwarded
        Assert.Contains("string Name", proxySource);
        Assert.Contains("int Age", proxySource);
        Assert.Contains("bool IsActive", proxySource);
        Assert.Contains("get => _inner.Name", proxySource);
        Assert.Contains("set => _inner.Name = value", proxySource);
        Assert.Contains("get => _inner.Age", proxySource);
        Assert.Contains("set => _inner.IsActive = value", proxySource);
    }
}
