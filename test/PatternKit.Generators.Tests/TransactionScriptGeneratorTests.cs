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
        })
        .AssertPassed();

    [Scenario("Generator reports invalid transaction script declarations")]
    [Theory]
    [InlineData("public static class SubmitOrderScript { [TransactionScriptHandler] private static ValueTask<OrderReceipt> Handle(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS001")]
    [InlineData("public static partial class SubmitOrderScript;", "PKTS002")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptHandler] private static ValueTask<OrderReceipt> One(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); [TransactionScriptHandler] private static ValueTask<OrderReceipt> Two(SubmitOrder request, CancellationToken cancellationToken) => new(new OrderReceipt(request.OrderId)); }", "PKTS002")]
    [InlineData("public static partial class SubmitOrderScript { [TransactionScriptHandler] private static OrderReceipt Handle(SubmitOrder request) => new(request.OrderId); }", "PKTS003")]
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

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "TransactionScriptGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(TransactionScript<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new TransactionScriptGenerator(), out var run, out _);
        var result = run.Results.Single();
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources);
}
