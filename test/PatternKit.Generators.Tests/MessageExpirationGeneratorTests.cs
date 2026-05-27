using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Messaging;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class MessageExpirationGeneratorTests
{
    [Scenario("GeneratesMessageExpirationFactory")]
    [Fact]
    public void GeneratesMessageExpirationFactory()
    {
        var source = """
            using PatternKit.Generators.Messaging;
            using PatternKit.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateMessageExpiration(
                typeof(Order),
                FactoryName = "Build",
                PolicyName = "orders",
                HeaderName = "x-expires-at",
                DefaultTtlMilliseconds = 30000,
                PreserveExisting = false,
                ExpiredReason = "too old")]
            public static partial class OrderExpiration;

            public static class Demo
            {
                public static string Run()
                    => OrderExpiration.Build().HeaderName;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesMessageExpirationFactory));
        var gen = new MessageExpirationGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderExpiration.MessageExpiration.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("MessageExpiration<global::MyApp.Order>", text);
        ScenarioExpect.Contains(".Name(@\"orders\")", text);
        ScenarioExpect.Contains(".Header(@\"x-expires-at\")", text);
        ScenarioExpect.Contains(".DefaultTtl(global::System.TimeSpan.FromMilliseconds(30000))", text);
        ScenarioExpect.Contains(".PreserveExisting(false)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("GeneratesDefaultsForStructHost")]
    [Fact]
    public void GeneratesDefaultsForStructHost()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateMessageExpiration(typeof(Order))]
            public readonly partial struct OrderExpiration;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesDefaultsForStructHost));
        var gen = new MessageExpirationGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.Empty(run.Results.SelectMany(result => result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources)).SourceText.ToString();
        ScenarioExpect.Contains("partial struct OrderExpiration", generated);
        ScenarioExpect.Contains(".Header(@\"expires-at\")", generated);
        ScenarioExpect.DoesNotContain("DefaultTtl", generated);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("ReportsDiagnosticForNonPartialExpiration")]
    [Fact]
    public void ReportsDiagnosticForNonPartialExpiration()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateMessageExpiration(typeof(Order))]
            public static class OrderExpiration;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialExpiration));
        var gen = new MessageExpirationGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMEXP001", diagnostic.Id);
    }

    [Scenario("ReportsDiagnosticForInvalidConfiguration")]
    [Fact]
    public void ReportsDiagnosticForInvalidConfiguration()
    {
        var source = """
            using PatternKit.Generators.Messaging;

            namespace MyApp;

            public sealed record Order(string Id);

            [GenerateMessageExpiration(typeof(Order), HeaderName = "")]
            public static partial class OrderExpiration;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidConfiguration));
        var gen = new MessageExpirationGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKMEXP002", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Messaging.Message<>).Assembly.Location));
}
