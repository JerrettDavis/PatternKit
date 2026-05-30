using Microsoft.CodeAnalysis;
using PatternKit.Application.TransactionScript;
using PatternKit.Generators.TransactionScript;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Transaction Script generator")]
public sealed partial class TransactionScriptGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits transaction script factory")]
    [Fact]
    public Task Generator_Emits_Transaction_Script_Factory()
        => Given("a valid transaction script declaration", () => Compile("""
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Application.TransactionScript;
            using PatternKit.Generators.TransactionScript;
            namespace Demo;
            public sealed record SubmitOrder(string OrderId);
            public sealed record OrderReceipt(string OrderId);
            [GenerateTransactionScript(typeof(SubmitOrder), typeof(OrderReceipt), FactoryName = "Build", ScriptName = "submit-order")]
            public static partial class SubmitOrderScript
            {
                [TransactionScriptValidator]
                private static IEnumerable<TransactionScriptError> Validate(SubmitOrder request) => [];
                [TransactionScriptHandler]
                private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId));
            }
            """))
        .Then("generated source creates the script with validator and handler", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("Create(\"submit-order\")", source);
            ScenarioExpect.Contains(".Validate(Validate)", source);
            ScenarioExpect.Contains(".Execute(Handle)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator reports invalid transaction script declarations")]
    [Theory]
    [InlineData("public static class SubmitOrderScript { [TransactionScriptHandler] private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS001")]
    [InlineData("public static partial class SubmitOrderScript;", "PKTS002")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptHandler] private static ValueTask<OrderReceipt> One(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); [TransactionScriptHandler] private static ValueTask<OrderReceipt> Two(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS002")]
    [InlineData("public partial class SubmitOrderScript { [TransactionScriptHandler] private ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS003")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptHandler] private static ValueTask<OrderReceipt> Handle<T>(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS003")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptHandler] private static OrderReceipt Handle(SubmitOrder request, CancellationToken cancellationToken) => new(request.OrderId); }", "PKTS003")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptHandler] private static ValueTask<OrderReceipt> Handle() => new(new OrderReceipt(string.Empty)); }", "PKTS003")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptHandler] private static ValueTask<OrderReceipt> Handle(string request, CancellationToken cancellationToken) => new(new OrderReceipt(request)); }", "PKTS003")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptHandler] private static ValueTask<OrderReceipt> Handle(SubmitOrder request, string cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS003")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptValidator] private static IEnumerable<TransactionScriptError> One(SubmitOrder request) => []; [TransactionScriptValidator] private static IEnumerable<TransactionScriptError> Two(SubmitOrder request) => []; [TransactionScriptHandler] private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS004")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptValidator] private static IEnumerable<TransactionScriptError> Validate<T>(SubmitOrder request) => []; [TransactionScriptHandler] private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS004")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptValidator] private static IEnumerable<TransactionScriptError> Validate(string request) => []; [TransactionScriptHandler] private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS004")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptValidator] private static string Validate(SubmitOrder request) => \"invalid\"; [TransactionScriptHandler] private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS004")]
    public Task Generator_Reports_Invalid_Transaction_Script_Declarations(string declaration, string diagnosticId)
        => Given("an invalid transaction script declaration", () => Compile($$"""
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Application.TransactionScript;
            using PatternKit.Generators.TransactionScript;
            public sealed record SubmitOrder(string OrderId);
            public sealed record OrderReceipt(string OrderId);
            [GenerateTransactionScript(typeof(SubmitOrder), typeof(OrderReceipt))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generator emits transaction script defaults and host shapes")]
    [Fact]
    public Task Generator_Emits_Transaction_Script_Defaults_And_Host_Shapes()
        => Given("transaction script declarations with default names and different host shapes", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.TransactionScript;
            namespace Demo;
            public sealed record SubmitOrder(string OrderId);
            public sealed record OrderReceipt(string OrderId);

            [GenerateTransactionScript(typeof(SubmitOrder), typeof(OrderReceipt))]
            internal abstract partial class AbstractScript
            {
                [TransactionScriptHandler]
                private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId));
            }

            [GenerateTransactionScript(typeof(SubmitOrder), typeof(OrderReceipt), ScriptName = "tenant\\\"script")]
            public sealed partial class SealedScript
            {
                [TransactionScriptHandler]
                private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId));
            }

            [GenerateTransactionScript(typeof(SubmitOrder), typeof(OrderReceipt))]
            internal partial struct StructScript
            {
                [TransactionScriptHandler]
                private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId));
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractScript", combined);
            ScenarioExpect.Contains("public sealed partial class SealedScript", combined);
            ScenarioExpect.Contains("internal partial struct StructScript", combined);
            ScenarioExpect.Contains("Create(\"AbstractScript\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"script\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator emits nested transaction script host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Transaction_Script_Host_Wrappers()
        => Given("nested transaction script declarations", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.TransactionScript;
            namespace Demo;
            public sealed record SubmitOrder(string OrderId);
            public sealed record OrderReceipt(string OrderId);

            public partial class ScriptContainer
            {
                private partial class PrivateHost
                {
                    [GenerateTransactionScript(typeof(SubmitOrder), typeof(OrderReceipt))]
                    protected partial class ProtectedScript
                    {
                        [TransactionScriptHandler]
                        private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId));
                    }

                    [GenerateTransactionScript(typeof(SubmitOrder), typeof(OrderReceipt))]
                    private protected partial class PrivateProtectedScript
                    {
                        [TransactionScriptHandler]
                        private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId));
                    }

                    [GenerateTransactionScript(typeof(SubmitOrder), typeof(OrderReceipt))]
                    protected internal partial class ProtectedInternalScript
                    {
                        [TransactionScriptHandler]
                        private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId));
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class ScriptContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedScript", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedScript", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalScript", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator skips malformed transaction script type arguments")]
    [Theory]
    [InlineData("null!", "typeof(OrderReceipt)")]
    [InlineData("typeof(SubmitOrder)", "null!")]
    public Task Generator_Skips_Malformed_Transaction_Script_Type_Arguments(string requestType, string responseType)
        => Given("a transaction script declaration with a null type argument", () => Compile($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.TransactionScript;
            public sealed record SubmitOrder(string OrderId);
            public sealed record OrderReceipt(string OrderId);
            [GenerateTransactionScript({{requestType}}, {{responseType}})]
            public static partial class SubmitOrderScript
            {
                [TransactionScriptHandler]
                private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId));
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "TransactionScriptGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(TransactionScript<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new TransactionScriptGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);
}
