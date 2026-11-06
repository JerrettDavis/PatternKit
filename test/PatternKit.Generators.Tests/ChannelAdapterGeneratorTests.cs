using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ChannelAdapterGeneratorTests
{
    [Scenario("GeneratesChannelAdapterFactory")]
    [Fact]
    public void GeneratesChannelAdapterFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record ExternalCommand(string Sku);
            public sealed record Command(string Sku);
            [GenerateChannelAdapter(typeof(ExternalCommand), typeof(Command), FactoryName = "Build", AdapterName = "erp")]
            public static partial class ErpAdapter
            {
                [ChannelAdapterInbound]
                private static Message<Command> Inbound(ExternalCommand external, MessageContext context)
                    => Message<Command>.Create(new Command(external.Sku));

                [ChannelAdapterOutbound]
                private static ExternalCommand Outbound(Message<Command> message, MessageContext context)
                    => new ExternalCommand(message.Payload.Sku);
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesChannelAdapterFactory));
        _ = RoslynTestHelpers.Run(comp, new ChannelAdapterGenerator(), out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("ChannelAdapter<global::MyApp.ExternalCommand, global::MyApp.Command>", text);
        ScenarioExpect.Contains(".MapInbound(Inbound)", text);
        ScenarioExpect.Contains(".MapOutbound(Outbound)", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsChannelAdapterDiagnostics")]
    [Theory]
    [InlineData("public static class ErpAdapter { }", "PKCAD001")]
    [InlineData("public static partial class ErpAdapter { }", "PKCAD002")]
    [InlineData("""
        public static partial class ErpAdapter
        {
            [ChannelAdapterInbound]
            private static Message<Command> Inbound(ExternalCommand external, MessageContext context)
                => Message<Command>.Create(new Command(external.Sku));
        }
        """, "PKCAD003")]
    public void ReportsChannelAdapterDiagnostics(string declaration, string expected)
    {
        var source = $$"""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record ExternalCommand(string Sku);
            public sealed record Command(string Sku);
            [GenerateChannelAdapter(typeof(ExternalCommand), typeof(Command))]
            {{declaration}}
            """;

        var comp = CreateCompilation(source, nameof(ReportsChannelAdapterDiagnostics) + expected);
        _ = RoslynTestHelpers.Run(comp, new ChannelAdapterGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("ReportsInvalidChannelAdapterTranslators")]
    [Theory]
    [InlineData("private static string Inbound(ExternalCommand external, MessageContext context) => \"bad\";", "PKCAD004")]
    [InlineData("private static string Outbound(Message<Command> message, MessageContext context) => \"bad\";", "PKCAD005")]
    public void ReportsInvalidChannelAdapterTranslators(string invalidMethod, string expected)
    {
        var inbound = expected == "PKCAD004"
            ? invalidMethod
            : "private static Message<Command> Inbound(ExternalCommand external, MessageContext context) => Message<Command>.Create(new Command(external.Sku));";
        var outbound = expected == "PKCAD005"
            ? invalidMethod
            : "private static ExternalCommand Outbound(Message<Command> message, MessageContext context) => new ExternalCommand(message.Payload.Sku);";
        var source = $$"""
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;
            namespace MyApp;
            public sealed record ExternalCommand(string Sku);
            public sealed record Command(string Sku);
            [GenerateChannelAdapter(typeof(ExternalCommand), typeof(Command))]
            public static partial class ErpAdapter
            {
                [ChannelAdapterInbound]
                {{inbound}}

                [ChannelAdapterOutbound]
                {{outbound}}
            }
            """;

        var comp = CreateCompilation(source, nameof(ReportsInvalidChannelAdapterTranslators) + expected);
        _ = RoslynTestHelpers.Run(comp, new ChannelAdapterGenerator(), out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(global::PatternKit.Messaging.Adapters.ChannelAdapter<,>).Assembly.Location));
}
