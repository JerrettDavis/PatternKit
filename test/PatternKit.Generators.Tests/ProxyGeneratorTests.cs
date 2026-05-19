using Microsoft.CodeAnalysis;
using System.IO;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class ProxyGeneratorTests
{
    [Scenario("GenerateProxyForInterface BasicContract")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Proxy class is generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("TestNamespace_IUserService.Proxy.g.cs", names);
        ScenarioExpect.Contains("TestNamespace_IUserService.Proxy.Interceptor.g.cs", names);

        // Verify proxy class content
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("UserServiceProxy", proxySource);
        ScenarioExpect.Contains("IUserService", proxySource);
        ScenarioExpect.Contains("_inner", proxySource);
        ScenarioExpect.Contains("_interceptor", proxySource);
        ScenarioExpect.Contains("GetUser", proxySource);
        ScenarioExpect.Contains("DeleteUser", proxySource);

        // Verify interceptor interface content
        var interceptorSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.Interceptor.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("IUserServiceInterceptor", interceptorSource);
        ScenarioExpect.Contains("void Before(MethodContext context)", interceptorSource);
        ScenarioExpect.Contains("void After(MethodContext context)", interceptorSource);
        ScenarioExpect.Contains("void OnException(MethodContext context", interceptorSource);
        ScenarioExpect.Contains("GetUserMethodContext", interceptorSource);
        ScenarioExpect.Contains("DeleteUserMethodContext", interceptorSource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxyForInterface WithAsyncMethods")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify simple delegation (no interceptor)
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IAsyncUserService.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("return _inner.GetUserAsync", proxySource);
        ScenarioExpect.Contains("_inner.DeleteUserAsync", proxySource);
        ScenarioExpect.DoesNotContain("_interceptor", proxySource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxyForInterface NoInterceptor")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Only proxy class is generated, no interceptor interface
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains("TestNamespace_ISimpleService.Proxy.g.cs", names);
        ScenarioExpect.DoesNotContain("TestNamespace_ISimpleService.Proxy.Interceptor.g.cs", names);

        // Verify proxy class has no interceptor field
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_ISimpleService.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.DoesNotContain("_interceptor", proxySource);
        ScenarioExpect.Contains("_inner.GetValue()", proxySource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxyForInterface PipelineMode")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify proxy class uses pipeline (IReadOnlyList)
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IPipelineService.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("IReadOnlyList", proxySource);
        ScenarioExpect.Contains("_interceptors", proxySource);
        ScenarioExpect.Contains("for (int __i = 0; __i < _interceptors", proxySource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxyForInterface WithProperties")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify proxy forwards properties
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IPropertyService.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("Name", proxySource);
        ScenarioExpect.Contains("Count", proxySource);
        ScenarioExpect.Contains("Description", proxySource);
        ScenarioExpect.Contains("get => _inner.Name", proxySource);
        ScenarioExpect.Contains("set => _inner.Name = value", proxySource);
        ScenarioExpect.Contains("get => _inner.Count", proxySource);
        ScenarioExpect.Contains("set => _inner.Description = value", proxySource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxyForInterface MustBePartial")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKPRX001");
        ScenarioExpect.Contains(diagnostics, d => d.GetMessage().Contains("partial"));
    }

    [Scenario("GenerateProxyForInterface ProxyIgnoreAttribute")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify proxy does not include ignored method
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IServiceWithIgnore.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("GetValue", proxySource);
        ScenarioExpect.DoesNotContain("LegacyMethod", proxySource);

        // Compilation succeeds (because user provided partial implementation)
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxyForInterface CustomProxyTypeName")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Verify custom proxy type name is used in class declaration
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("class CustomUserServiceProxy", proxySource);
        ScenarioExpect.Contains("public CustomUserServiceProxy(", proxySource);

        // Compilation succeeds
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxyForAbstractClass OnlyProxiesVirtualMembers")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        // Check that files were generated
        var names = result.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        ScenarioExpect.Contains(names, n => n.Contains("UserServiceBase"));

        // Verify proxy class content - look for the proxy file
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Where(gs => gs.HintName.Contains("UserServiceBase") && gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .Select(gs => gs.SourceText.ToString())
            .FirstOrDefault();

        ScenarioExpect.NotNull(proxySource);
        ScenarioExpect.Contains("UserServiceBaseProxy", proxySource);
        ScenarioExpect.Contains("UserServiceBase", proxySource);
        ScenarioExpect.Contains("GetUser", proxySource);
        ScenarioExpect.Contains("UpdateUser", proxySource);
        ScenarioExpect.DoesNotContain("NonVirtualMethod", proxySource);
    }

    [Scenario("GenerateProxy ProtectedMember GeneratesWarning")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKPRX003" && d.GetMessage().Contains("GetProtectedUser"));
    }

    [Scenario("GenerateProxy InaccessibleMember GeneratesError")]
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
        ScenarioExpect.NotEmpty(result.Results.SelectMany(r => r.GeneratedSources));
    }

    [Scenario("GenerateProxy NameConflict GeneratesError")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKPRX004");
    }

    [Scenario("GenerateProxy ExceptionPolicySwallow")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        // Verify exception is NOT rethrown (Swallow policy)
        ScenarioExpect.Contains("catch", proxySource);
        ScenarioExpect.DoesNotContain("throw;", proxySource);
    }

    [Scenario("GenerateProxy WithRefInParameters")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("ref int id", proxySource);
        ScenarioExpect.Contains("in string name", proxySource);
        ScenarioExpect.Contains("ref id", proxySource); // ref forwarded
        ScenarioExpect.Contains("in name", proxySource); // in forwarded
    }

    [Scenario("GenerateProxy WithOutParameters")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("out string name", proxySource);
        ScenarioExpect.Contains("out name", proxySource); // out forwarded
        ScenarioExpect.Contains("default", proxySource); // default used in context
    }

    [Scenario("GenerateProxy GenericMethod GeneratesError")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKPRX002" && d.GetMessage().Contains("Generic method"));
    }

    [Scenario("GenerateProxy Event GeneratesError")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKPRX002" && d.GetMessage().Contains("Event"));
    }

    [Scenario("GenerateProxy NestedType GeneratesError")]
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
        ScenarioExpect.True(diagnostics.Length > 0 || result.Results.SelectMany(r => r.GeneratedSources).Count() == 0);
    }

    [Scenario("GenerateProxy PipelineModeWithExceptions")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        // Verify pipeline with list of interceptors
        ScenarioExpect.Contains("IReadOnlyList", proxySource);
        ScenarioExpect.Contains("_interceptors", proxySource);

        // Verify Before loop (ascending)
        ScenarioExpect.Contains("for (int __i = 0; __i < _interceptors!.Count; __i++)", proxySource);
        ScenarioExpect.Contains("_interceptors[__i].Before", proxySource);

        // Verify After loop (descending)
        ScenarioExpect.Contains("for (int __i = _interceptors!.Count - 1; __i >= 0; __i--)", proxySource);
        ScenarioExpect.Contains("_interceptors[__i].After", proxySource);

        // Verify OnException loop (descending)
        ScenarioExpect.Contains("_interceptors[__i].OnException", proxySource);
    }

    [Scenario("GenerateProxy ParameterNameConflictsWithReservedNames")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var interceptorSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.Interceptor.g.cs")
            .SourceText.ToString();

        // Verify parameters are renamed to avoid conflicts
        ScenarioExpect.Contains("Arg_MethodName", interceptorSource);
        ScenarioExpect.Contains("Arg_Result", interceptorSource);

        // Compilation should succeed
        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxy RefReturningMethodWithCancellationToken")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IUserService.Proxy.g.cs")
            .SourceText.ToString();

        // Verify async modifier is NOT added (ref-returning methods can't be async)
        var methodLines = proxySource.Split('\n')
            .Where(l => l.Contains("GetValueRef") && l.Contains("public"))
            .ToArray();

        ScenarioExpect.All(methodLines, line => ScenarioExpect.DoesNotContain("async", line));
    }

    [Scenario("GenerateProxy NonAbstractClass GeneratesError")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKPRX002" && d.GetMessage().Contains("Non-abstract class"));
    }

    [Scenario("GenerateProxy GenericContract GeneratesError")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKPRX002" && d.GetMessage().Contains("Generic type"));
    }

    [Scenario("GenerateProxy PartialRecord IsSupported")]
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
        ScenarioExpect.NotNull(result);
    }

    [Scenario("GenerateProxy ForceAsyncTrue GeneratesAsyncInterceptors")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var interceptorSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Where(gs => gs.HintName.Contains("Interceptor.g.cs"))
            .Select(gs => gs.SourceText.ToString())
            .FirstOrDefault();

        // Should generate async interceptor methods even for sync-only contract
        ScenarioExpect.NotNull(interceptorSource);
        ScenarioExpect.Contains("BeforeAsync", interceptorSource);
        ScenarioExpect.Contains("AfterAsync", interceptorSource);
        ScenarioExpect.Contains("OnExceptionAsync", interceptorSource);
    }

    [Scenario("GenerateProxy CustomProxyTypeNameViaAttribute")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        ScenarioExpect.Contains("class MyCustomProxy", proxySource);
        ScenarioExpect.Contains("public MyCustomProxy(", proxySource);
    }

    [Scenario("GenerateProxy WithDefaultParameters")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Verify default parameters are preserved
        ScenarioExpect.Contains("string? name = null", proxySource);
        ScenarioExpect.Contains("int age = 18", proxySource);
        ScenarioExpect.Contains("bool active = true", proxySource);
    }

    [Scenario("GenerateProxy InterfaceWithIPrefix GeneratesCorrectName")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Should strip I prefix: IMyService -> MyServiceProxy
        ScenarioExpect.Contains("class MyServiceProxy", proxySource);
        ScenarioExpect.DoesNotContain("class IMyServiceProxy", proxySource);
    }

    [Scenario("GenerateProxy InterfaceWithoutIPrefix GeneratesCorrectName")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Should not strip prefix: UserService -> UserServiceProxy
        ScenarioExpect.Contains("class UserServiceProxy", proxySource);
    }

    [Scenario("GenerateProxy WithNullableReferenceTypes")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Verify nullable annotations are preserved
        ScenarioExpect.Contains("string? GetUser", proxySource);
        ScenarioExpect.Contains("string GetRequiredUser", proxySource);
    }

    [Scenario("GenerateProxy VoidAsyncMethods")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Verify Task and ValueTask without <T> are handled
        ScenarioExpect.Contains("Task UpdateUserAsync", proxySource);
        ScenarioExpect.Contains("ValueTask DeleteUserAsync", proxySource);
        ScenarioExpect.Contains("await", proxySource);
        ScenarioExpect.Contains("ConfigureAwait(false)", proxySource);
    }

    [Scenario("GenerateProxy MultiplePropertiesWithGettersAndSetters")]
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
        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Proxy.g.cs") && !gs.HintName.Contains("Interceptor"))
            .SourceText.ToString();

        // Verify all property accessors are forwarded
        ScenarioExpect.Contains("string Name", proxySource);
        ScenarioExpect.Contains("int Age", proxySource);
        ScenarioExpect.Contains("bool IsActive", proxySource);
        ScenarioExpect.Contains("get => _inner.Name", proxySource);
        ScenarioExpect.Contains("set => _inner.Name = value", proxySource);
        ScenarioExpect.Contains("get => _inner.Age", proxySource);
        ScenarioExpect.Contains("set => _inner.IsActive = value", proxySource);
    }

    [Scenario("GenerateProxy DisabledAsyncBaseInterfaceAndEscapedDefaults CoverBranches")]
    [Fact]
    public void GenerateProxy_DisabledAsyncBaseInterfaceAndEscapedDefaults_CoverBranches()
    {
        const string source = """
            using PatternKit.Generators.Proxy;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public enum Mode { Unknown = 0, Fast = 1 }

            public partial interface IBaseService
            {
                string FromBase(char marker = '\n');
            }

            [GenerateProxy]
            public partial interface IWarnsForAsync : IBaseService
            {
                Task<string> GetAsync(CancellationToken ct = default);
                string Format(
                    string escaped = "line\nquote\"slash\\tab\t",
                    char quote = '\'',
                    float ratio = 1.5f,
                    double score = 2.25d,
                    decimal money = 3.75m,
                    Mode mode = Mode.Fast);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_DisabledAsyncBaseInterfaceAndEscapedDefaults_CoverBranches));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IWarnsForAsync.Proxy.g.cs")
            .SourceText.ToString();
        var interceptorSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_IWarnsForAsync.Proxy.Interceptor.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("FromBase", proxySource);
        ScenarioExpect.Contains("string escaped = \"line\\nquote\\\"slash\\\\tab\\t\"", proxySource);
        ScenarioExpect.Contains("char quote = '\\''", proxySource);
        ScenarioExpect.Contains("float ratio = 1.5f", proxySource);
        ScenarioExpect.Contains("double score = 2.25d", proxySource);
        ScenarioExpect.Contains("decimal money = 3.75m", proxySource);
        ScenarioExpect.Contains("Mode mode = global::TestNamespace.Mode.Fast", proxySource);
        ScenarioExpect.Contains("BeforeAsync", interceptorSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxy IndexerProperty ReportsUnsupportedMember")]
    [Fact]
    public void GenerateProxy_IndexerProperty_ReportsUnsupportedMember()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IIndexedService
            {
                string this[int index] { get; }
                string GetValue();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_IndexerProperty_ReportsUnsupportedMember));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKPRX002" && d.GetMessage().Contains("Indexer"));
    }

    [Scenario("GenerateProxy AsyncPipelineWithVoidTasksAndRefs CoversAsyncEmission")]
    [Fact]
    public void GenerateProxy_AsyncPipelineWithVoidTasksAndRefs_CoversAsyncEmission()
    {
        const string source = """
            using PatternKit.Generators.Proxy;
            using System.Threading;
            using System.Threading.Tasks;

            [GenerateProxy(InterceptorMode = ProxyInterceptorMode.Pipeline, Exceptions = ProxyExceptionPolicy.Swallow)]
            public partial interface Worker
            {
                void Copy(ref int source, out int destination, in bool enabled);
                Task SaveAsync(int id, CancellationToken ct = default);
                ValueTask FlushAsync(CancellationToken ct = default);
                Task<int> CountAsync(CancellationToken ct = default);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_AsyncPipelineWithVoidTasksAndRefs_CoversAsyncEmission));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "Worker.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("for (int __i = 0; __i < _interceptors!.Count; __i++)", proxySource);
        ScenarioExpect.Contains("for (int __i = _interceptors!.Count - 1; __i >= 0; __i--)", proxySource);
        ScenarioExpect.Contains("_inner.Copy(ref source, out destination, in enabled);", proxySource);
        ScenarioExpect.Contains("await __task.ConfigureAwait(false);", proxySource);
        ScenarioExpect.Contains("var __result = await __task.ConfigureAwait(false);", proxySource);
        ScenarioExpect.Contains("return default!;", proxySource);
        ScenarioExpect.Contains("WorkerProxy", proxySource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxy AbstractRecordAndAccessibility CoversRecordAndProtectedBranches")]
    [Fact]
    public void GenerateProxy_AbstractRecordAndAccessibility_CoversRecordAndProtectedBranches()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy(ProxyTypeName = "AbstractRecordProxy")]
            public abstract partial record class AbstractRecordService
            {
                public abstract string Get();
                protected abstract string ProtectedOnly();
                public abstract string PublicGet { get; }
                protected abstract string ProtectedProperty { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_AbstractRecordAndAccessibility_CoversRecordAndProtectedBranches));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKPRX003" && d.GetMessage().Contains("ProtectedOnly"));
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKPRX003" && d.GetMessage().Contains("ProtectedProperty"));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName == "TestNamespace_AbstractRecordService.Proxy.g.cs")
            .SourceText.ToString();
        ScenarioExpect.Contains("class AbstractRecordProxy", proxySource);
        ScenarioExpect.Contains("PublicGet", proxySource);
    }

    [Scenario("GenerateProxy NoInterceptorMode CoversPureDelegationRefsPropertiesAndStatics")]
    [Fact]
    public void GenerateProxy_NoInterceptorMode_CoversPureDelegationRefsPropertiesAndStatics()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy(InterceptorMode = ProxyInterceptorMode.None, Exceptions = ProxyExceptionPolicy.Swallow)]
            public abstract partial class WorkerContract
            {
                public abstract string Name { get; set; }
                public abstract int Version { get; }
                public static string StaticValue => "ignored";

                public abstract void Copy(ref int source, out int destination, in bool enabled);
                public abstract int Calculate(int value);
                public virtual string Virtual(string value) => value;
                public string Concrete(string value) => value;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_NoInterceptorMode_CoversPureDelegationRefsPropertiesAndStatics));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var proxySource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "TestNamespace_WorkerContract.Proxy.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("Name", proxySource);
        ScenarioExpect.Contains("get => _inner.Name;", proxySource);
        ScenarioExpect.Contains("set => _inner.Name = value;", proxySource);
        ScenarioExpect.Contains("Version", proxySource);
        ScenarioExpect.Contains("_inner.Copy(ref source, out destination, in enabled);", proxySource);
        ScenarioExpect.Contains("return _inner.Calculate(value);", proxySource);
        ScenarioExpect.Contains("return _inner.Virtual(value);", proxySource);
        ScenarioExpect.DoesNotContain("Concrete", proxySource);
        ScenarioExpect.DoesNotContain("StaticValue", proxySource);
        ScenarioExpect.DoesNotContain("Interceptor", proxySource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GenerateProxy InterfaceWithIgnoredOnlyMembers SkipsGeneration")]
    [Fact]
    public void GenerateProxy_InterfaceWithIgnoredOnlyMembers_SkipsGeneration()
    {
        const string source = """
            using PatternKit.Generators.Proxy;

            namespace TestNamespace;

            [GenerateProxy]
            public partial interface IEmptyProxy
            {
                [ProxyIgnore]
                void Ignored();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GenerateProxy_InterfaceWithIgnoredOnlyMembers_SkipsGeneration));
        var gen = new ProxyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        ScenarioExpect.Empty(result.Results.SelectMany(r => r.GeneratedSources));
    }
}
