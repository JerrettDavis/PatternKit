using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Application.UnitOfWork;
using PatternKit.Generators.UnitOfWork;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class UnitOfWorkGeneratorTests
{
    [Scenario("Generates unit of work factory")]
    [Fact]
    public void GeneratesUnitOfWorkFactory()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.UnitOfWork;

            [GenerateUnitOfWork(FactoryName = "Build")]
            public static partial class CheckoutWork
            {
                [UnitOfWorkStep("reserve", 10, RollbackMethodName = nameof(UndoReserve))]
                private static ValueTask Reserve(CancellationToken ct) => default;

                private static ValueTask UndoReserve(CancellationToken ct) => default;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesUnitOfWorkFactory));
        var gen = new UnitOfWorkGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("CheckoutWork.UnitOfWork.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("Build", text);
        ScenarioExpect.Contains("builder.Enlist(\"reserve\", Reserve, UndoReserve);", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Reports diagnostic for non partial unit of work")]
    [Fact]
    public void ReportsDiagnosticForNonPartialUnitOfWork()
    {
        var comp = CreateCompilation("""
            using PatternKit.Generators.UnitOfWork;
            [GenerateUnitOfWork]
            public static class CheckoutWork;
            """, nameof(ReportsDiagnosticForNonPartialUnitOfWork));
        _ = RoslynTestHelpers.Run(comp, new UnitOfWorkGenerator(), out var run, out _);

        ScenarioExpect.Equal("PKUOW001", ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

    [Scenario("Reports diagnostic for missing unit of work steps")]
    [Fact]
    public void ReportsDiagnosticForMissingUnitOfWorkSteps()
    {
        var comp = CreateCompilation("""
            using PatternKit.Generators.UnitOfWork;
            [GenerateUnitOfWork]
            public static partial class CheckoutWork;
            """, nameof(ReportsDiagnosticForMissingUnitOfWorkSteps));
        _ = RoslynTestHelpers.Run(comp, new UnitOfWorkGenerator(), out var run, out _);

        ScenarioExpect.Equal("PKUOW002", ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

    [Scenario("Reports diagnostic for invalid unit of work step")]
    [Fact]
    public void ReportsDiagnosticForInvalidUnitOfWorkStep()
    {
        var comp = CreateCompilation("""
            using PatternKit.Generators.UnitOfWork;
            [GenerateUnitOfWork]
            public static partial class CheckoutWork
            {
                [UnitOfWorkStep("reserve", 10)]
                private static void Reserve() { }
            }
            """, nameof(ReportsDiagnosticForInvalidUnitOfWorkStep));
        _ = RoslynTestHelpers.Run(comp, new UnitOfWorkGenerator(), out var run, out _);

        ScenarioExpect.Equal("PKUOW003", ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics)).Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Application.UnitOfWork.UnitOfWork).Assembly.Location));
}
