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
}
