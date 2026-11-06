using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Application.PortsAndAdapters;
using PatternKit.Generators.PortsAndAdapters;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Ports and Adapters generator")]
public sealed partial class PortsAndAdaptersGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits ports and adapters pipeline factory")]
    [Fact]
    public Task Generator_Emits_Ports_And_Adapters_Pipeline_Factory()
        => Given("a configured ports and adapters declaration", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.PortsAndAdapters;
            namespace Demo;

            public sealed record Inbound(string Id);
            public sealed record Command(string Id);
            public sealed record Result(string Id);
            public sealed record Outbound(string Id);

            [GeneratePortsAndAdapters(typeof(Inbound), typeof(Command), typeof(Result), typeof(Outbound), FactoryName = "Build", PipelineName = "order-entry")]
            public static partial class OrderEntry
            {
                [InboundAdapter]
                private static Command AdaptInbound(Inbound inbound) => new(inbound.Id);
                [ApplicationPort]
                private static ValueTask<Result> Handle(Command command, CancellationToken cancellationToken) => new(new Result(command.Id));
                [OutboundAdapter]
                private static Outbound AdaptOutbound(Result result) => new(result.Id);
            }
            """))
        .Then("generated source creates the configured pipeline", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source.HintName + source.Source);
            ScenarioExpect.Contains("Create(\"order-entry\")", source.Source);
            ScenarioExpect.Contains(".AdaptInboundWith(AdaptInbound)", source.Source);
            ScenarioExpect.Contains(".HandleWith(Handle)", source.Source);
            ScenarioExpect.Contains(".AdaptOutboundWith(AdaptOutbound)", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator emits nested ports and adapters host wrappers and safe defaults")]
    [Fact]
    public Task Generator_Emits_Nested_Ports_And_Adapters_Host_Wrappers_And_Safe_Defaults()
        => Given("a nested ports and adapters declaration with a blank pipeline name", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.PortsAndAdapters;
            namespace Demo;

            public sealed record Inbound(string Id);
            public sealed record Command(string Id);
            public sealed record Result(string Id);
            public sealed record Outbound(string Id);

            public partial class OrderModule
            {
                [GeneratePortsAndAdapters(typeof(Inbound), typeof(Command), typeof(Result), typeof(Outbound), PipelineName = " ")]
                internal static partial class OrderEntry
                {
                    [InboundAdapter]
                    private static Command AdaptInbound(Inbound inbound) => new(inbound.Id);
                    [ApplicationPort]
                    private static ValueTask<Result> Handle(Command command, CancellationToken cancellationToken) => new(new Result(command.Id));
                    [OutboundAdapter]
                    private static Outbound AdaptOutbound(Result result) => new(result.Id);
                }
            }
            """))
        .Then("generated source wraps containing types and defaults the pipeline name", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public partial class OrderModule", source.Source);
            ScenarioExpect.Contains("internal static partial class OrderEntry", source.Source);
            ScenarioExpect.Contains("Create(\"ports-and-adapters\")", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator reports invalid ports and adapters declarations")]
    [Theory]
    [InlineData("public static class OrderEntry { [InboundAdapter] private static Command AdaptInbound(Inbound inbound) => new(inbound.Id); [ApplicationPort] private static ValueTask<Result> Handle(Command command, CancellationToken cancellationToken) => new(new Result(command.Id)); [OutboundAdapter] private static Outbound AdaptOutbound(Result result) => new(result.Id); }", "PKPA001")]
    [InlineData("public static partial class OrderEntry { [ApplicationPort] private static ValueTask<Result> Handle(Command command, CancellationToken cancellationToken) => new(new Result(command.Id)); [OutboundAdapter] private static Outbound AdaptOutbound(Result result) => new(result.Id); }", "PKPA002")]
    [InlineData("public static partial class OrderEntry { [InboundAdapter] private static string AdaptInbound(Inbound inbound) => inbound.Id; [ApplicationPort] private static ValueTask<Result> Handle(Command command, CancellationToken cancellationToken) => new(new Result(command.Id)); [OutboundAdapter] private static Outbound AdaptOutbound(Result result) => new(result.Id); }", "PKPA003")]
    [InlineData("public static partial class OrderEntry { [InboundAdapter] private static Command AdaptInbound(Inbound inbound) => new(inbound.Id); [ApplicationPort] private static Result Handle(Command command) => new(command.Id); [OutboundAdapter] private static Outbound AdaptOutbound(Result result) => new(result.Id); }", "PKPA003")]
    [InlineData("public static partial class OrderEntry { [InboundAdapter] private static Command AdaptInbound(Inbound inbound) => new(inbound.Id); [ApplicationPort] private static ValueTask<Result> Handle(Command command, CancellationToken cancellationToken) => new(new Result(command.Id)); [OutboundAdapter] private static string AdaptOutbound(Result result) => result.Id; }", "PKPA003")]
    public Task Generator_Reports_Invalid_Ports_And_Adapters_Declarations(string declaration, string diagnosticId)
        => Given("an invalid ports and adapters declaration", () => Compile($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.PortsAndAdapters;
            public sealed record Inbound(string Id);
            public sealed record Command(string Id);
            public sealed record Result(string Id);
            public sealed record Outbound(string Id);
            [GeneratePortsAndAdapters(typeof(Inbound), typeof(Command), typeof(Result), typeof(Outbound))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Ports and adapters attribute exposes generator configuration")]
    [Fact]
    public void Ports_And_Adapters_Attribute_Exposes_Generator_Configuration()
    {
        var attribute = new GeneratePortsAndAdaptersAttribute(typeof(string), typeof(int), typeof(long), typeof(decimal))
        {
            FactoryName = "Build",
            PipelineName = "orders"
        };

        ScenarioExpect.Equal(typeof(string), attribute.InboundType);
        ScenarioExpect.Equal(typeof(int), attribute.CommandType);
        ScenarioExpect.Equal(typeof(long), attribute.ResultType);
        ScenarioExpect.Equal(typeof(decimal), attribute.OutboundType);
        ScenarioExpect.Equal("Build", attribute.FactoryName);
        ScenarioExpect.Equal("orders", attribute.PipelineName);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GeneratePortsAndAdaptersAttribute(null!, typeof(int), typeof(long), typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GeneratePortsAndAdaptersAttribute(typeof(string), null!, typeof(long), typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GeneratePortsAndAdaptersAttribute(typeof(string), typeof(int), null!, typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GeneratePortsAndAdaptersAttribute(typeof(string), typeof(int), typeof(long), null!));
    }

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "PortsAndAdaptersGeneratorTests",
            extra:
            [
                MetadataReference.CreateFromFile(typeof(PortsAndAdaptersPipeline<,,,>).Assembly.Location),
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath())
            ]);
        _ = RoslynTestHelpers.Run(compilation, new PortsAndAdaptersGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => new GeneratedSource(source.HintName, source.SourceText.ToString())).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(PortsAndAdaptersGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
