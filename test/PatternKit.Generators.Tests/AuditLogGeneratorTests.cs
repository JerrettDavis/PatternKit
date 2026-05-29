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
            ScenarioExpect.Contains("public static partial class OrderAuditLog", source);
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
    [InlineData("public static partial class OrderAuditLog { [AuditLogKeySelector] private Guid SelectKey(AuditEntry entry) => entry.EntryId; }", "PKAUD003")]
    [InlineData("public static partial class OrderAuditLog { [AuditLogKeySelector] private static Guid SelectKey<T>(AuditEntry entry) => entry.EntryId; }", "PKAUD003")]
    [InlineData("public static partial class OrderAuditLog { [AuditLogKeySelector] private static Guid SelectKey() => Guid.Empty; }", "PKAUD003")]
    [InlineData("public static partial class OrderAuditLog { [AuditLogKeySelector] private static Guid SelectKey(AuditEntry entry, string tenant) => entry.EntryId; }", "PKAUD003")]
    [InlineData("public static partial class OrderAuditLog { [AuditLogKeySelector] private static Guid SelectKey(string entry) => Guid.Parse(entry); }", "PKAUD003")]
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

    [Scenario("Generator emits audit log defaults and type shapes")]
    [Fact]
    public Task Generator_Emits_Audit_Log_Defaults_And_Type_Shapes()
        => Given("audit log declarations using default names and different host shapes", () => Compile("""
            using System;
            using PatternKit.Generators.AuditLog;

            namespace Demo;

            public sealed record AuditEntry(Guid EntryId);

            [GenerateAuditLog(typeof(AuditEntry), typeof(Guid))]
            internal abstract partial class AbstractAuditLog
            {
                [AuditLogKeySelector]
                private static Guid SelectKey(AuditEntry entry) => entry.EntryId;
            }

            [GenerateAuditLog(typeof(AuditEntry), typeof(Guid), LogName = "tenant\\\"audit")]
            public sealed partial class SealedAuditLog
            {
                [AuditLogKeySelector]
                private static Guid SelectKey(AuditEntry entry) => entry.EntryId;
            }

            [GenerateAuditLog(typeof(AuditEntry), typeof(Guid))]
            internal partial struct StructAuditLog
            {
                [AuditLogKeySelector]
                private static Guid SelectKey(AuditEntry entry) => entry.EntryId;
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractAuditLog", combined);
            ScenarioExpect.Contains("Create()", combined);
            ScenarioExpect.Contains("Create(\"audit-log\", SelectKey).Build()", combined);
            ScenarioExpect.Contains("public sealed partial class SealedAuditLog", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"audit\", SelectKey).Build()", combined);
            ScenarioExpect.Contains("internal partial struct StructAuditLog", combined);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator emits nested audit log host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Audit_Log_Host_Wrappers()
        => Given("nested audit log declarations with non-public accessibility", () => Compile("""
            using System;
            using PatternKit.Generators.AuditLog;

            namespace Demo;

            public sealed record AuditEntry(Guid EntryId);

            public partial class AuditContainer
            {
                private partial class PrivateHost
                {
                    [GenerateAuditLog(typeof(AuditEntry), typeof(Guid))]
                    protected partial class ProtectedAuditLog
                    {
                        [AuditLogKeySelector]
                        private static Guid SelectKey(AuditEntry entry) => entry.EntryId;
                    }

                    [GenerateAuditLog(typeof(AuditEntry), typeof(Guid))]
                    private protected partial class PrivateProtectedAuditLog
                    {
                        [AuditLogKeySelector]
                        private static Guid SelectKey(AuditEntry entry) => entry.EntryId;
                    }

                    [GenerateAuditLog(typeof(AuditEntry), typeof(Guid))]
                    protected internal partial class ProtectedInternalAuditLog
                    {
                        [AuditLogKeySelector]
                        private static Guid SelectKey(AuditEntry entry) => entry.EntryId;
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class AuditContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedAuditLog", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedAuditLog", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalAuditLog", combined);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator skips malformed audit log type arguments")]
    [Theory]
    [InlineData("null!", "typeof(Guid)")]
    [InlineData("typeof(AuditEntry)", "null!")]
    public Task Generator_Skips_Malformed_Audit_Log_Type_Arguments(string entryType, string keyType)
        => Given("an audit log declaration with a null type argument", () => Compile($$"""
            using System;
            using PatternKit.Generators.AuditLog;

            public sealed record AuditEntry(Guid EntryId);

            [GenerateAuditLog({{entryType}}, {{keyType}})]
            public static partial class OrderAuditLog
            {
                [AuditLogKeySelector]
                private static Guid SelectKey(AuditEntry entry) => entry.EntryId;
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
