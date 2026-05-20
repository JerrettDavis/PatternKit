using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Factories;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class AbstractFactoryGeneratorTests
{
    [Scenario("Generates abstract factory families and service provider overload")]
    [Fact]
    public void GeneratesAbstractFactoryFamiliesAndServiceProviderOverload()
    {
        var source = """
            using System;
            using PatternKit.Generators.Factories;

            namespace Microsoft.Extensions.DependencyInjection
            {
                public static class ActivatorUtilities
                {
                    public static T CreateInstance<T>(IServiceProvider services) where T : new() => new T();
                }
            }

            namespace Demo
            {
                public enum Platform { Windows, Linux }
                public interface IButton { string Render(); }
                public interface ITextBox { string Render(); }
                public sealed class WindowsButton : IButton { public string Render() => "windows-button"; }
                public sealed class WindowsTextBox : ITextBox { public string Render() => "windows-text"; }
                public sealed class LinuxButton : IButton { public string Render() => "linux-button"; }
                public sealed class LinuxTextBox : ITextBox { public string Render() => "linux-text"; }

                [GenerateAbstractFactory(typeof(Platform), FactoryMethodName = "Build", ServiceProviderFactoryMethodName = "BuildFromServices")]
                [AbstractFactoryProduct(Platform.Windows, typeof(IButton), typeof(WindowsButton))]
                [AbstractFactoryProduct(Platform.Windows, typeof(ITextBox), typeof(WindowsTextBox))]
                [AbstractFactoryProduct(Platform.Linux, typeof(IButton), typeof(LinuxButton))]
                [AbstractFactoryProduct(Platform.Linux, typeof(ITextBox), typeof(LinuxTextBox))]
                public static partial class WidgetFactory;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesAbstractFactoryFamiliesAndServiceProviderOverload));
        var gen = new AbstractFactoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        ScenarioExpect.Equal("WidgetFactory.AbstractFactory.g.cs", generated.HintName);
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("BuildFromServices(global::System.IServiceProvider services)", text);
        ScenarioExpect.Contains("builder.Family(global::Demo.Platform.Windows)", text);
        ScenarioExpect.Contains("builder.Product<global::Demo.IButton>(() => new global::Demo.WindowsButton())", text);
        ScenarioExpect.Contains("ActivatorUtilities.CreateInstance<global::Demo.WindowsButton>(services)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial abstract factory host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialAbstractFactoryHost()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            [GenerateAbstractFactory(typeof(string))]
            public static class WidgetFactory;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForNonPartialAbstractFactoryHost));
        var gen = new AbstractFactoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKAF001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid abstract factory product")]
    [Fact]
    public void ReportsDiagnosticForInvalidAbstractFactoryProduct()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public interface IButton { }
            public sealed class TextBox { }

            [GenerateAbstractFactory(typeof(string))]
            [AbstractFactoryProduct("windows", typeof(IButton), typeof(TextBox))]
            public static partial class WidgetFactory;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticForInvalidAbstractFactoryProduct));
        var gen = new AbstractFactoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostic = ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
        ScenarioExpect.Equal("PKAF003", diagnostic.Id);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(typeof(PatternKit.Creational.AbstractFactory.AbstractFactory<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceProvider).Assembly.Location)
            ]);
}
