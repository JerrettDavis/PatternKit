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

    [Scenario("Formats escaped literal and null family keys")]
    [Fact]
    public void FormatsEscapedLiteralAndNullFamilyKeys()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public interface IButton { }
            public sealed class QuoteButton : IButton { }
            public sealed class NewLineButton : IButton { }
            public sealed class NullButton : IButton { }

            [GenerateAbstractFactory(typeof(string))]
            [AbstractFactoryProduct("win\"dows", typeof(IButton), typeof(QuoteButton))]
            public static partial class StringLiteralFactory;

            [GenerateAbstractFactory(typeof(char))]
            [AbstractFactoryProduct('\n', typeof(IButton), typeof(NewLineButton))]
            public static partial class CharLiteralFactory;

            [GenerateAbstractFactory(typeof(object))]
            [AbstractFactoryProduct(null, typeof(IButton), typeof(NullButton))]
            public static partial class NullFactory;
            """;

        var comp = CreateCompilation(source, nameof(FormatsEscapedLiteralAndNullFamilyKeys));
        var gen = new AbstractFactoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = run.Results.SelectMany(result => result.GeneratedSources).ToArray();
        var stringLiteralFactory = ScenarioExpect.Single(generated.Where(source => source.HintName == "StringLiteralFactory.AbstractFactory.g.cs"));
        var charLiteralFactory = ScenarioExpect.Single(generated.Where(source => source.HintName == "CharLiteralFactory.AbstractFactory.g.cs"));
        var nullFactory = ScenarioExpect.Single(generated.Where(source => source.HintName == "NullFactory.AbstractFactory.g.cs"));

        ScenarioExpect.Contains("builder.Family(\"win\\\"dows\")", stringLiteralFactory.SourceText.ToString());
        ScenarioExpect.Contains("builder.Family('\\n')", charLiteralFactory.SourceText.ToString());
        ScenarioExpect.Contains("builder.Family(null!)", nullFactory.SourceText.ToString());

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for unnamed enum family key")]
    [Fact]
    public void ReportsDiagnosticForUnnamedEnumFamilyKey()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public enum Platform { Windows = 1 }
            public interface IButton { }
            public sealed class WindowsButton : IButton { }

            [GenerateAbstractFactory(typeof(Platform))]
            [AbstractFactoryProduct((Platform)2, typeof(IButton), typeof(WindowsButton))]
            public static partial class WidgetFactory;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForUnnamedEnumFamilyKey));

        ScenarioExpect.Equal("PKAF003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for abstract implementation type")]
    [Fact]
    public void ReportsDiagnosticForAbstractImplementationType()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public interface IButton { }
            public abstract class AbstractButton : IButton { }

            [GenerateAbstractFactory(typeof(string))]
            [AbstractFactoryProduct("windows", typeof(IButton), typeof(AbstractButton))]
            public static partial class WidgetFactory;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForAbstractImplementationType));

        ScenarioExpect.Equal("PKAF003", diagnostic.Id);
    }

    [Scenario("Generates abstract factory for global struct host")]
    [Fact]
    public void GeneratesAbstractFactoryForGlobalStructHost()
    {
        var source = """
            using PatternKit.Generators.Factories;

            public interface IButton { }
            public sealed class WindowsButton : IButton { }

            [GenerateAbstractFactory(typeof(uint))]
            [AbstractFactoryProduct(7u, typeof(IButton), typeof(WindowsButton))]
            public partial struct WidgetFactory;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesAbstractFactoryForGlobalStructHost));
        var gen = new AbstractFactoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();

        ScenarioExpect.Contains("partial struct WidgetFactory", text);
        ScenarioExpect.DoesNotContain("namespace ", text);
        ScenarioExpect.Contains("builder.Family(7u)", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Formats primitive abstract factory family keys")]
    [Fact]
    public void FormatsPrimitiveAbstractFactoryFamilyKeys()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public class Product { }

            [GenerateAbstractFactory(typeof(bool))]
            [AbstractFactoryProduct(true, typeof(Product), typeof(Product))]
            public static partial class BoolFactory;

            [GenerateAbstractFactory(typeof(byte))]
            [AbstractFactoryProduct((byte)1, typeof(Product), typeof(Product))]
            public static partial class ByteFactory;

            [GenerateAbstractFactory(typeof(sbyte))]
            [AbstractFactoryProduct((sbyte)-1, typeof(Product), typeof(Product))]
            public static partial class SByteFactory;

            [GenerateAbstractFactory(typeof(short))]
            [AbstractFactoryProduct((short)-2, typeof(Product), typeof(Product))]
            public static partial class ShortFactory;

            [GenerateAbstractFactory(typeof(ushort))]
            [AbstractFactoryProduct((ushort)2, typeof(Product), typeof(Product))]
            public static partial class UShortFactory;

            [GenerateAbstractFactory(typeof(int))]
            [AbstractFactoryProduct(-3, typeof(Product), typeof(Product))]
            public static partial class IntFactory;

            [GenerateAbstractFactory(typeof(long))]
            [AbstractFactoryProduct(-4L, typeof(Product), typeof(Product))]
            public static partial class LongFactory;

            [GenerateAbstractFactory(typeof(ulong))]
            [AbstractFactoryProduct(5UL, typeof(Product), typeof(Product))]
            public static partial class ULongFactory;
            """;

        var comp = CreateCompilation(source, nameof(FormatsPrimitiveAbstractFactoryFamilyKeys));
        var gen = new AbstractFactoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var text = string.Join("\n", run.Results.SelectMany(result => result.GeneratedSources).Select(source => source.SourceText.ToString()));

        ScenarioExpect.Contains("builder.Family(true)", text);
        ScenarioExpect.Contains("builder.Family(1)", text);
        ScenarioExpect.Contains("builder.Family(-1)", text);
        ScenarioExpect.Contains("builder.Family(-2)", text);
        ScenarioExpect.Contains("builder.Family(2)", text);
        ScenarioExpect.Contains("builder.Family(-3)", text);
        ScenarioExpect.Contains("builder.Family(-4L)", text);
        ScenarioExpect.Contains("builder.Family(5UL)", text);
        ScenarioExpect.Contains("builder.Product<global::Demo.Product>(() => new global::Demo.Product())", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Formats additional char keys and base class products")]
    [Fact]
    public void FormatsAdditionalCharKeysAndBaseClassProducts()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public class Widget { }
            public sealed class DerivedWidget : Widget { }

            [GenerateAbstractFactory(typeof(char), ServiceProviderFactoryMethodName = "   ")]
            [AbstractFactoryProduct('\\', typeof(Widget), typeof(DerivedWidget))]
            [AbstractFactoryProduct('\'', typeof(Widget), typeof(DerivedWidget), IsDefaultFamily = true)]
            [AbstractFactoryProduct('\r', typeof(Widget), typeof(DerivedWidget))]
            [AbstractFactoryProduct('\t', typeof(Widget), typeof(DerivedWidget))]
            [AbstractFactoryProduct('x', typeof(Widget), typeof(DerivedWidget))]
            public static partial class CharFactory;
            """;

        var comp = CreateCompilation(source, nameof(FormatsAdditionalCharKeysAndBaseClassProducts));
        var gen = new AbstractFactoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));

        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();

        ScenarioExpect.Contains("builder.Family('\\\\')", text);
        ScenarioExpect.Contains("builder.Family('\\r')", text);
        ScenarioExpect.Contains("builder.Family('\\t')", text);
        ScenarioExpect.Contains("builder.Family('x')", text);
        ScenarioExpect.Contains("builder.DefaultProduct<global::Demo.Widget>(() => new global::Demo.DerivedWidget())", text);
        ScenarioExpect.Contains("builder.Product<global::Demo.Widget>(() => new global::Demo.DerivedWidget())", text);
        ScenarioExpect.DoesNotContain("IServiceProvider", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostics for incompatible primitive abstract factory keys")]
    [Fact]
    public void ReportsDiagnosticsForIncompatiblePrimitiveAbstractFactoryKeys()
    {
        var source = """
            using PatternKit.Generators.Factories;

            namespace Demo;

            public enum Platform { Windows = 1 }
            public class Product { }

            [GenerateAbstractFactory(null!)]
            public static partial class NullKeyTypeFactory;

            [GenerateAbstractFactory(typeof(string))]
            [AbstractFactoryProduct(1, typeof(Product), typeof(Product))]
            public static partial class StringKeyFactory;

            [GenerateAbstractFactory(typeof(bool))]
            [AbstractFactoryProduct("true", typeof(Product), typeof(Product))]
            public static partial class BoolKeyFactory;

            [GenerateAbstractFactory(typeof(char))]
            [AbstractFactoryProduct("x", typeof(Product), typeof(Product))]
            public static partial class CharKeyFactory;

            [GenerateAbstractFactory(typeof(byte))]
            [AbstractFactoryProduct("1", typeof(Product), typeof(Product))]
            public static partial class ByteKeyFactory;

            [GenerateAbstractFactory(typeof(sbyte))]
            [AbstractFactoryProduct("1", typeof(Product), typeof(Product))]
            public static partial class SByteKeyFactory;

            [GenerateAbstractFactory(typeof(short))]
            [AbstractFactoryProduct("1", typeof(Product), typeof(Product))]
            public static partial class ShortKeyFactory;

            [GenerateAbstractFactory(typeof(ushort))]
            [AbstractFactoryProduct("1", typeof(Product), typeof(Product))]
            public static partial class UShortKeyFactory;

            [GenerateAbstractFactory(typeof(uint))]
            [AbstractFactoryProduct("1", typeof(Product), typeof(Product))]
            public static partial class UIntKeyFactory;

            [GenerateAbstractFactory(typeof(long))]
            [AbstractFactoryProduct("1", typeof(Product), typeof(Product))]
            public static partial class LongKeyFactory;

            [GenerateAbstractFactory(typeof(ulong))]
            [AbstractFactoryProduct("1", typeof(Product), typeof(Product))]
            public static partial class ULongKeyFactory;

            [GenerateAbstractFactory(typeof(Platform))]
            [AbstractFactoryProduct("windows", typeof(Product), typeof(Product))]
            public static partial class EnumKeyFactory;

            [GenerateAbstractFactory(typeof(double))]
            [AbstractFactoryProduct(1.5, typeof(Product), typeof(Product))]
            public static partial class DoubleKeyFactory;
            """;

        var comp = CreateCompilation(source, nameof(ReportsDiagnosticsForIncompatiblePrimitiveAbstractFactoryKeys));
        var gen = new AbstractFactoryGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diagnostics = run.Results.SelectMany(result => result.Diagnostics).ToArray();
        ScenarioExpect.Equal(12, diagnostics.Length);
        ScenarioExpect.All(diagnostics, diagnostic => ScenarioExpect.Equal("PKAF003", diagnostic.Id));
        ScenarioExpect.Empty(run.Results.SelectMany(result => result.GeneratedSources));
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
