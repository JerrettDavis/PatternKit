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

    [Scenario("Reports diagnostic for abstract factory without products")]
    [Fact]
    public void ReportsDiagnosticForAbstractFactoryWithoutProducts()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            [GenerateAbstractFactory(typeof(string))]
            public static partial class WidgetFactory;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForAbstractFactoryWithoutProducts));

        ScenarioExpect.Equal("PKAF002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for duplicate abstract factory products")]
    [Fact]
    public void ReportsDiagnosticForDuplicateAbstractFactoryProducts()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public interface IButton { }
            public sealed class WindowsButton : IButton { }
            public sealed class AlternateWindowsButton : IButton { }

            [GenerateAbstractFactory(typeof(string))]
            [AbstractFactoryProduct("windows", typeof(IButton), typeof(WindowsButton))]
            [AbstractFactoryProduct("windows", typeof(IButton), typeof(AlternateWindowsButton))]
            public static partial class WidgetFactory;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForDuplicateAbstractFactoryProducts));

        ScenarioExpect.Equal("PKAF004", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for incompatible abstract factory key")]
    [Fact]
    public void ReportsDiagnosticForIncompatibleAbstractFactoryKey()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public interface IButton { }
            public sealed class WindowsButton : IButton { }

            [GenerateAbstractFactory(typeof(int))]
            [AbstractFactoryProduct("windows", typeof(IButton), typeof(WindowsButton))]
            public static partial class WidgetFactory;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForIncompatibleAbstractFactoryKey));

        ScenarioExpect.Equal("PKAF003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for abstract factory product without public parameterless constructor")]
    [Fact]
    public void ReportsDiagnosticForProductWithoutPublicParameterlessConstructor()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public interface IButton { }
            public sealed class WindowsButton : IButton
            {
                public WindowsButton(string label) { }
            }

            [GenerateAbstractFactory(typeof(string))]
            [AbstractFactoryProduct("windows", typeof(IButton), typeof(WindowsButton))]
            public static partial class WidgetFactory;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForProductWithoutPublicParameterlessConstructor));

        ScenarioExpect.Equal("PKAF003", diagnostic.Id);
    }

    [Scenario("Generates default products and omits service provider overload when not requested")]
    [Fact]
    public void GeneratesDefaultProductsAndOmitsServiceProviderOverloadWhenNotRequested()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public interface IButton { }
            public class ButtonBase : IButton { }
            public sealed class DefaultButton : ButtonBase { }

            [GenerateAbstractFactory(typeof(string), FactoryMethodName = "Build")]
            [AbstractFactoryProduct("fallback", typeof(ButtonBase), typeof(DefaultButton), IsDefaultFamily = true)]
            public static partial class WidgetFactory;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesDefaultProductsAndOmitsServiceProviderOverloadWhenNotRequested));
        var gen = new AbstractFactoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();

        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("builder.DefaultProduct<global::Demo.ButtonBase>(() => new global::Demo.DefaultButton())", text);
        ScenarioExpect.DoesNotContain("IServiceProvider", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
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

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new AbstractFactoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
