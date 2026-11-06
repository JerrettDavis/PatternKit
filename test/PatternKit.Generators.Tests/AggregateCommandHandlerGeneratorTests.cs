using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Aggregates;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class AggregateCommandHandlerGeneratorTests
{
    [Scenario("Generates aggregate command handler from decision and applier")]
    [Fact]
    public void Generates_Aggregate_Command_Handler_From_Decision_And_Applier()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Aggregates;

            namespace Demo;

            public sealed class OrderAggregate;
            public sealed class PayOrder;
            public interface IOrderEvent;

            [GenerateAggregateCommandHandler(typeof(OrderAggregate), typeof(PayOrder), typeof(IOrderEvent), FactoryMethodName = "Build", HandlerName = "pay-order")]
            public static partial class OrderHandlers
            {
                [AggregateDecision]
                private static IEnumerable<IOrderEvent> Decide(OrderAggregate aggregate, PayOrder command) => [];

                [AggregateEventApplier]
                private static void Apply(OrderAggregate aggregate, IOrderEvent domainEvent) { }
            }
            """;

        var comp = CreateCompilation(source, nameof(Generates_Aggregate_Command_Handler_From_Decision_And_Applier));
        var gen = new AggregateCommandHandlerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(static result => result.GeneratedSources));
        ScenarioExpect.Equal("OrderHandlers.AggregateCommandHandler.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("AggregateCommandHandler<global::Demo.OrderAggregate, global::Demo.PayOrder, global::Demo.IOrderEvent>", text);
        ScenarioExpect.Contains("\"pay-order\", Decide, Apply", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Generates aggregate command handler from array decision and global struct host")]
    [Fact]
    public void Generates_Aggregate_Command_Handler_From_Array_Decision_And_Global_Struct_Host()
    {
        var source = """
            using PatternKit.Generators.Aggregates;

            public sealed class OrderAggregate;
            public sealed class PayOrder;
            public interface IOrderEvent;

            [GenerateAggregateCommandHandler(typeof(OrderAggregate), typeof(PayOrder), typeof(IOrderEvent))]
            public partial struct OrderHandlers
            {
                [AggregateDecision]
                private static IOrderEvent[] Decide(OrderAggregate aggregate, PayOrder command) => [];

                [AggregateEventApplier]
                private static void Apply(OrderAggregate aggregate, IOrderEvent domainEvent) { }
            }
            """;

        var comp = CreateCompilation(source, nameof(Generates_Aggregate_Command_Handler_From_Array_Decision_And_Global_Struct_Host));
        var gen = new AggregateCommandHandlerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(static result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("partial struct OrderHandlers", text);
        ScenarioExpect.DoesNotContain("namespace ", text);
        ScenarioExpect.Contains("\"generated-aggregate-handler\", Decide, Apply", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Theory]
    [InlineData("""
        using PatternKit.Generators.Aggregates;
        [GenerateAggregateCommandHandler(typeof(object), typeof(string), typeof(int))]
        public static class Host;
        """, "PKAGG001")]
    [InlineData("""
        using PatternKit.Generators.Aggregates;
        [GenerateAggregateCommandHandler(typeof(object), typeof(string), typeof(int))]
        public static partial class Host;
        """, "PKAGG002")]
    [InlineData("""
        using System.Collections.Generic;
        using PatternKit.Generators.Aggregates;
        [GenerateAggregateCommandHandler(typeof(object), typeof(string), typeof(int))]
        public static partial class Host
        {
            [AggregateDecision] private static IEnumerable<int> Decide(object aggregate, string command) => [];
        }
        """, "PKAGG003")]
    [InlineData("""
        using PatternKit.Generators.Aggregates;
        [GenerateAggregateCommandHandler(typeof(object), typeof(string), typeof(int))]
        public static partial class Host
        {
            [AggregateDecision] private static string Decide(object aggregate, string command) => "";
            [AggregateEventApplier] private static void Apply(object aggregate, int domainEvent) { }
        }
        """, "PKAGG004")]
    [InlineData("""
        using System.Collections.Generic;
        using PatternKit.Generators.Aggregates;
        [GenerateAggregateCommandHandler(typeof(object), typeof(string), typeof(int))]
        public static partial class Host
        {
            [AggregateDecision] private static IEnumerable<int> Decide(object aggregate, string command) => [];
            [AggregateEventApplier] private static int Apply(object aggregate, int domainEvent) => 0;
        }
        """, "PKAGG005")]
    public void Reports_Aggregate_Command_Handler_Diagnostics(string source, string expected)
    {
        var diagnostic = RunAndGetSingleDiagnostic(source, expected);

        ScenarioExpect.Equal(expected, diagnostic.Id);
    }

    [Scenario("Reports missing decision when aggregate host has duplicate decisions")]
    [Fact]
    public void Reports_Missing_Decision_When_Aggregate_Host_Has_Duplicate_Decisions()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Aggregates;
            [GenerateAggregateCommandHandler(typeof(object), typeof(string), typeof(int))]
            public static partial class Host
            {
                [AggregateDecision] private static IEnumerable<int> Decide(object aggregate, string command) => [];
                [AggregateDecision] private static IEnumerable<int> DecideAgain(object aggregate, string command) => [];
                [AggregateEventApplier] private static void Apply(object aggregate, int domainEvent) { }
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(Reports_Missing_Decision_When_Aggregate_Host_Has_Duplicate_Decisions));

        ScenarioExpect.Equal("PKAGG002", diagnostic.Id);
    }

    [Scenario("Reports missing applier when aggregate host has duplicate appliers")]
    [Fact]
    public void Reports_Missing_Applier_When_Aggregate_Host_Has_Duplicate_Appliers()
    {
        var source = """
            using System.Collections.Generic;
            using PatternKit.Generators.Aggregates;
            [GenerateAggregateCommandHandler(typeof(object), typeof(string), typeof(int))]
            public static partial class Host
            {
                [AggregateDecision] private static IEnumerable<int> Decide(object aggregate, string command) => [];
                [AggregateEventApplier] private static void Apply(object aggregate, int domainEvent) { }
                [AggregateEventApplier] private static void ApplyAgain(object aggregate, int domainEvent) { }
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(Reports_Missing_Applier_When_Aggregate_Host_Has_Duplicate_Appliers));

        ScenarioExpect.Equal("PKAGG003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Application.Aggregates.AggregateCommandHandler<,,>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(AggregateCommandHandlerGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new AggregateCommandHandlerGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(static result => result.Diagnostics));
    }
}
