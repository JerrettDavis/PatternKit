using Microsoft.CodeAnalysis;
using PatternKit.Generators.NullObject;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class NullObjectGeneratorTests
{
    [Scenario("Null Object generator attributes expose configured values")]
    [Fact]
    public void Null_Object_Generator_Attributes_Expose_Configured_Values()
    {
        var marker = new GenerateNullObjectAttribute { TypeName = "NullNotifier" };
        var text = new NullObjectDefaultAttribute("suppressed");
        var flag = new NullObjectDefaultAttribute(true);
        var integer = new NullObjectDefaultAttribute(42);
        var longInteger = new NullObjectDefaultAttribute(42L);
        var number = new NullObjectDefaultAttribute(1.5d);

        ScenarioExpect.Equal("NullNotifier", marker.TypeName);
        ScenarioExpect.Equal("suppressed", text.Value);
        ScenarioExpect.Equal(true, flag.Value);
        ScenarioExpect.Equal(42, integer.Value);
        ScenarioExpect.Equal(42L, longInteger.Value);
        ScenarioExpect.Equal(1.5d, number.Value);
    }

    [Scenario("Generate Null Object For Interface Contract")]
    [Fact]
    public void Generate_Null_Object_For_Interface_Contract()
    {
        const string source = """
            using System.Threading.Tasks;
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            [GenerateNullObject(TypeName = "NullNotifier")]
            public interface INotifier
            {
                string Channel { get; }

                [NullObjectDefault(true)]
                bool IsAvailable { get; }

                void Notify(string recipient, string body);

                [NullObjectDefault("suppressed")]
                string Describe(string recipient);

                Task<string> RenderAsync(string template);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_For_Interface_Contract));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "TestNamespace_INotifier.NullObject.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("public sealed class NullNotifier : global::TestNamespace.INotifier", generatedSource);
        ScenarioExpect.Contains("public static NullNotifier Instance { get; } = new();", generatedSource);
        ScenarioExpect.Contains("public string Channel => string.Empty;", generatedSource);
        ScenarioExpect.Contains("public bool IsAvailable => true;", generatedSource);
        ScenarioExpect.Contains("public void Notify(string recipient, string body)", generatedSource);
        ScenarioExpect.Contains("public string Describe(string recipient) => @\"suppressed\";", generatedSource);
        ScenarioExpect.Contains("global::System.Threading.Tasks.Task.FromResult<string>(string.Empty)", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generate Null Object Supports Complete Interface Shapes")]
    [Fact]
    public void Generate_Null_Object_Supports_Complete_Interface_Shapes()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            public sealed record NotificationStatus(string Value);

            public interface IBaseNotifier
            {
                string BaseName { get; }
            }

            [GenerateNullObject(TypeName = "NullAdvancedNotifier")]
            internal interface IAdvancedNotifier : IBaseNotifier
            {
                event EventHandler? Changed;

                new string BaseName { get; }

                string @class { get; }

                string this[int index] { get; set; }

                string MutableName { get; set; }

                string InitOnlyName { get; init; }

                T Resolve<T>() where T : class;

                void Observe(ref readonly NotificationStatus status);

                void Send(string @event);

                bool TryGet(string key, out NotificationStatus status);

                Task<NotificationStatus> LoadAsync();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Supports_Complete_Interface_Shapes));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generatedSource = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(gs => gs.HintName == "TestNamespace_IAdvancedNotifier.NullObject.g.cs")
            .SourceText.ToString();

        ScenarioExpect.Contains("internal sealed class NullAdvancedNotifier", generatedSource);
        ScenarioExpect.Contains("public string BaseName => string.Empty;", generatedSource);
        ScenarioExpect.Equal(2, generatedSource.Split("BaseName").Length);
        ScenarioExpect.Contains("public string @class => string.Empty;", generatedSource);
        ScenarioExpect.Contains("public event global::System.EventHandler? Changed", generatedSource);
        ScenarioExpect.Contains("public string this[int index]", generatedSource);
        ScenarioExpect.Contains("set { }", generatedSource);
        ScenarioExpect.Contains("public string InitOnlyName", generatedSource);
        ScenarioExpect.Contains("init { }", generatedSource);
        ScenarioExpect.Contains("public T Resolve<T>() where T : class => default!;", generatedSource);
        ScenarioExpect.Contains("public void Observe(ref readonly global::TestNamespace.NotificationStatus status)", generatedSource);
        ScenarioExpect.Contains("public void Send(string @event)", generatedSource);
        ScenarioExpect.Contains("status = default!;", generatedSource);
        ScenarioExpect.Contains("return false;", generatedSource);
        ScenarioExpect.Contains("Task.FromResult<global::TestNamespace.NotificationStatus>(default!)", generatedSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generate Null Object Covers Default Names And Return Shapes")]
    [Fact]
    public void Generate_Null_Object_Covers_Default_Names_And_Return_Shapes()
    {
        const string source = """
            using System.Threading.Tasks;
            using PatternKit.Generators.NullObject;

            [GenerateNullObject]
            public interface AuditSink
            {
                int Count { get; }

                long BigCount { get; }

                double Ratio { get; }

                decimal Total { get; }

                string[] Tags { get; }

                int WriteOnly { set; }

                Task FlushAsync();

                ValueTask PingAsync();

                ValueTask<int> LoadAsync();

                T Create<T>() where T : notnull, new();

                [NullObjectDefault(42)]
                int ConfiguredInt();

                [NullObjectDefault(42L)]
                long ConfiguredLong();

                [NullObjectDefault(1.5d)]
                double ConfiguredDouble();

                [NullObjectDefault(1.5d)]
                float ConfiguredFloat();

                [NullObjectDefault(1.5d)]
                decimal ConfiguredDecimal();

                [NullObjectDefault(double.NaN)]
                double ConfiguredNaN();

                [NullObjectDefault(double.PositiveInfinity)]
                float ConfiguredInfinity();

                [NullObjectDefault("suppressed")]
                Task<string> ConfiguredTaskAsync();

                [NullObjectDefault("suppressed")]
                ValueTask<string> ConfiguredValueTaskAsync();
            }

            [GenerateNullObject]
            public interface IDefaultNotifier
            {
                bool Enabled { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Covers_Default_Names_And_Return_Shapes));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generatedSources = result.Results
            .SelectMany(static r => r.GeneratedSources)
            .ToDictionary(static gs => gs.HintName, static gs => gs.SourceText.ToString());
        var auditSink = generatedSources["AuditSink.NullObject.g.cs"];
        var notifier = generatedSources["IDefaultNotifier.NullObject.g.cs"];

        ScenarioExpect.Contains("public sealed class NullAuditSink : global::AuditSink", auditSink);
        ScenarioExpect.Contains("public int Count => 0;", auditSink);
        ScenarioExpect.Contains("public long BigCount => 0;", auditSink);
        ScenarioExpect.Contains("public double Ratio => 0;", auditSink);
        ScenarioExpect.Contains("public decimal Total => 0;", auditSink);
        ScenarioExpect.Contains("global::System.Array.Empty<string>()", auditSink);
        ScenarioExpect.Contains("public int WriteOnly", auditSink);
        ScenarioExpect.Contains("global::System.Threading.Tasks.Task.CompletedTask", auditSink);
        ScenarioExpect.Contains("public global::System.Threading.Tasks.ValueTask PingAsync() => default;", auditSink);
        ScenarioExpect.Contains("new global::System.Threading.Tasks.ValueTask<int>(0)", auditSink);
        ScenarioExpect.Contains("public T Create<T>() where T : notnull, new() => default!;", auditSink);
        ScenarioExpect.Contains("public int ConfiguredInt() => 42;", auditSink);
        ScenarioExpect.Contains("public long ConfiguredLong() => 42;", auditSink);
        ScenarioExpect.Contains("public double ConfiguredDouble() => 1.5;", auditSink);
        ScenarioExpect.Contains("public float ConfiguredFloat() => 1.5F;", auditSink);
        ScenarioExpect.Contains("public decimal ConfiguredDecimal() => 1.5M;", auditSink);
        ScenarioExpect.Contains("public double ConfiguredNaN() => double.NaN;", auditSink);
        ScenarioExpect.Contains("public float ConfiguredInfinity() => float.PositiveInfinity;", auditSink);
        ScenarioExpect.Contains("Task.FromResult<string>(@\"suppressed\")", auditSink);
        ScenarioExpect.Contains("new global::System.Threading.Tasks.ValueTask<string>(@\"suppressed\")", auditSink);
        ScenarioExpect.Contains("public sealed class NullDefaultNotifier", notifier);
        ScenarioExpect.Contains("public bool Enabled => false;", notifier);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generate Null Object Reports Invalid Type Name")]
    [Fact]
    public void Generate_Null_Object_Reports_Invalid_Type_Name()
    {
        const string source = """
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            [GenerateNullObject(TypeName = "123Nope")]
            public interface INotifier
            {
                string Channel { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Reports_Invalid_Type_Name));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKNO003");
    }

    [Scenario("Generate Null Object Reports Keyword Type Name")]
    [Fact]
    public void Generate_Null_Object_Reports_Keyword_Type_Name()
    {
        const string source = """
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            [GenerateNullObject(TypeName = "class")]
            public interface INotifier
            {
                string Channel { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Reports_Keyword_Type_Name));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKNO003");
    }

    [Scenario("Generate Null Object Rejects Generic Contracts")]
    [Fact]
    public void Generate_Null_Object_Rejects_Generic_Contracts()
    {
        const string source = """
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            [GenerateNullObject]
            public interface ILookup<T>
            {
                T Find(string key);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Rejects_Generic_Contracts));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKNO002");
    }

    [Scenario("Generate Null Object Rejects Unsupported Static Abstract Members")]
    [Fact]
    public void Generate_Null_Object_Rejects_Unsupported_Static_Abstract_Members()
    {
        const string source = """
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            [GenerateNullObject]
            public interface IFactory
            {
                static abstract string Create();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Rejects_Unsupported_Static_Abstract_Members));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKNO004");
    }

    [Scenario("Generate Null Object Rejects Nested Contracts")]
    [Fact]
    public void Generate_Null_Object_Rejects_Nested_Contracts()
    {
        const string source = """
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            public static class Contracts
            {
                [GenerateNullObject]
                public interface INotifier
                {
                    string Name { get; }
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Rejects_Nested_Contracts));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKNO004");
    }

    [Scenario("Generate Null Object Rejects Unsupported By Ref Returns")]
    [Fact]
    public void Generate_Null_Object_Rejects_Unsupported_By_Ref_Returns()
    {
        const string source = """
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            [GenerateNullObject]
            public interface ICursor
            {
                ref int Current();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Rejects_Unsupported_By_Ref_Returns));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKNO004");
    }

    [Scenario("Generate Null Object Rejects Unsupported By Ref Properties")]
    [Fact]
    public void Generate_Null_Object_Rejects_Unsupported_By_Ref_Properties()
    {
        const string source = """
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            [GenerateNullObject]
            public interface ICursor
            {
                ref int Current { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Rejects_Unsupported_By_Ref_Properties));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKNO004");
    }

    [Scenario("Generate Null Object Reports Type Name Conflicts")]
    [Fact]
    public void Generate_Null_Object_Reports_Type_Name_Conflicts()
    {
        const string source = """
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            public sealed class NullNotifier
            {
            }

            [GenerateNullObject]
            public interface INotifier
            {
                string Name { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Reports_Type_Name_Conflicts));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKNO005");
    }

    [Scenario("Generate Null Object Rejects Conflicting Hidden Members")]
    [Fact]
    public void Generate_Null_Object_Rejects_Conflicting_Hidden_Members()
    {
        const string source = """
            using PatternKit.Generators.NullObject;

            namespace TestNamespace;

            public interface IBaseNotifier
            {
                string Name { get; }
            }

            [GenerateNullObject]
            public interface INotifier : IBaseNotifier
            {
                new int Name { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generate_Null_Object_Rejects_Conflicting_Hidden_Members));
        var gen = new NullObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKNO004");
    }
}
