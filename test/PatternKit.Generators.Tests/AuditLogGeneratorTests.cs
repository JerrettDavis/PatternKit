using Microsoft.CodeAnalysis;
using PatternKit.Application.AuditLog;
using PatternKit.Generators.AuditLog;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Audit Log generator")]
public sealed partial class AuditLogGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits audit log factory")]
    [Fact]
    public Task Generator_Emits_Audit_Log_Factory()
        => Given("a valid audit log declaration", () => Compile("""
            using System;
            using PatternKit.Generators.AuditLog;
            namespace Demo;
            public sealed record AuditEntry(Guid EntryId);
            [GenerateAuditLog(typeof(AuditEntry), typeof(Guid), FactoryName = "Build", LogName = "order-audit")]
            public static partial class OrderAuditLog
            {
                [AuditLogKeySelector]
                private static Guid SelectKey(AuditEntry entry) => entry.EntryId;
            }
            """))
        .Then("generated source creates the audit log", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("InMemoryAuditLog<global::Demo.AuditEntry, global::System.Guid>.Create(\"order-audit\", SelectKey).Build()", source);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator reports invalid audit log declarations")]
    [Theory]
    [InlineData("public static class OrderAuditLog { [AuditLogKeySelector] private static Guid SelectKey(AuditEntry entry) => entry.EntryId; }", "PKAUD001")]
    [InlineData("public static partial class OrderAuditLog;", "PKAUD002")]
    [InlineData("public static partial class OrderAuditLog { [AuditLogKeySelector] private static Guid One(AuditEntry entry) => entry.EntryId; [AuditLogKeySelector] private static Guid Two(AuditEntry entry) => entry.EntryId; }", "PKAUD002")]
    [InlineData("public static partial class OrderAuditLog { [AuditLogKeySelector] private static string SelectKey(AuditEntry entry) => entry.EntryId.ToString(); }", "PKAUD003")]
    public Task Generator_Reports_Invalid_Audit_Log_Declarations(string declaration, string diagnosticId)
        => Given("an invalid audit log declaration", () => Compile($$"""
            using System;
            using PatternKit.Generators.AuditLog;
            public sealed record AuditEntry(Guid EntryId);
            [GenerateAuditLog(typeof(AuditEntry), typeof(Guid))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "AuditLogGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(InMemoryAuditLog<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new AuditLogGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            string.Join("\n", emit.Diagnostics));
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        string EmitDiagnostics);
}
