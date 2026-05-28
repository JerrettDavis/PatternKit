using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.ValueObjects;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ValueObjectGeneratorTests
{
    [Scenario("Generates value object equality and factory")]
    [Fact]
    public void Generates_Value_Object_Equality_And_Factory()
    {
        var source = """
            using PatternKit.Generators.ValueObjects;

            namespace Demo;

            [GenerateValueObject(FactoryMethodName = "From")]
            public sealed partial class Money
            {
                private Money(decimal amount, string currency)
                {
                    Amount = amount;
                    Currency = currency;
                }

                [ValueObjectComponent]
                public decimal Amount { get; }

                [ValueObjectComponent]
                public string Currency { get; }
            }
            """;

        var comp = CreateCompilation(source, nameof(Generates_Value_Object_Equality_And_Factory));
        var gen = new ValueObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(static result => result.GeneratedSources));
        ScenarioExpect.Equal("Money.ValueObject.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("public static Money From(decimal amount, string currency)", text);
        ScenarioExpect.Contains("global::System.Collections.Generic.EqualityComparer<decimal>.Default.Equals(Amount, other.Amount)", text);
        ScenarioExpect.Contains("public override int GetHashCode()", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial value object")]
    [Fact]
    public void Reports_Diagnostic_For_Non_Partial_Value_Object()
    {
        var source = """
            using PatternKit.Generators.ValueObjects;

            [GenerateValueObject]
            public sealed class Money
            {
                [ValueObjectComponent]
                public decimal Amount { get; }
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(Reports_Diagnostic_For_Non_Partial_Value_Object));

        ScenarioExpect.Equal("PKVO001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for struct value object")]
    [Fact]
    public void Reports_Diagnostic_For_Struct_Value_Object()
    {
        var source = """
            using PatternKit.Generators.ValueObjects;

            [GenerateValueObject]
            public readonly partial struct Money
            {
                [ValueObjectComponent]
                public decimal Amount { get; }
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(Reports_Diagnostic_For_Struct_Value_Object));

        ScenarioExpect.Equal("PKVO002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for value object without components")]
    [Fact]
    public void Reports_Diagnostic_For_Value_Object_Without_Components()
    {
        var source = """
            using PatternKit.Generators.ValueObjects;

            [GenerateValueObject]
            public sealed partial class Money;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(Reports_Diagnostic_For_Value_Object_Without_Components));

        ScenarioExpect.Equal("PKVO003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath())
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(ValueObjectGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new ValueObjectGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(static result => result.Diagnostics));
    }
}
