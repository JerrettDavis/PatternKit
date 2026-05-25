using BenchmarkDotNet.Attributes;
using PatternKit.Creational.AbstractFactory;
using PatternKit.Examples.AbstractFactoryDemo;
using static PatternKit.Examples.AbstractFactoryDemo.AbstractFactoryDemo;

namespace PatternKit.Benchmarks.Creational;

[BenchmarkCategory("Creational", "GoF", "AbstractFactory")]
public class AbstractFactoryBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create abstract factory")]
    [BenchmarkCategory("Fluent", "Construction")]
    public AbstractFactory<Platform> Fluent_CreateAbstractFactory()
        => AbstractFactory<Platform>.Create()
            .Family(Platform.Windows)
            .Product<IButton>(() => new WindowsButton())
            .Product<ITextBox>(() => new WindowsTextBox())
            .Product<ICheckBox>(() => new WindowsCheckBox())
            .Product<IDialog>(() => new WindowsDialog())
            .Family(Platform.MacOS)
            .Product<IButton>(() => new MacButton())
            .Product<ITextBox>(() => new MacTextBox())
            .Product<ICheckBox>(() => new MacCheckBox())
            .Product<IDialog>(() => new MacDialog())
            .Family(Platform.Linux)
            .Product<IButton>(() => new LinuxButton())
            .Product<ITextBox>(() => new LinuxTextBox())
            .Product<ICheckBox>(() => new LinuxCheckBox())
            .Product<IDialog>(() => new LinuxDialog())
            .Build();

    [Benchmark(Description = "Generated: create abstract factory")]
    [BenchmarkCategory("Generated", "Construction")]
    public AbstractFactory<Platform> Generated_CreateAbstractFactory()
        => GeneratedPlatformWidgetFactory.Create();

    [Benchmark(Description = "Fluent: create login widgets")]
    [BenchmarkCategory("Fluent", "Execution")]
    public WidgetRenderSummary Fluent_CreateLoginWidgets()
        => RenderWidgets(Fluent_CreateAbstractFactory().GetFamily(Platform.Linux));

    [Benchmark(Description = "Generated: create login widgets")]
    [BenchmarkCategory("Generated", "Execution")]
    public WidgetRenderSummary Generated_CreateLoginWidgets()
        => RenderWidgets(GeneratedPlatformWidgetFactory.Create().GetFamily(Platform.Linux));

    private static WidgetRenderSummary RenderWidgets(AbstractFactory<Platform>.ProductFamily family)
    {
        var username = family.Create<ITextBox>();
        var rememberMe = family.Create<ICheckBox>();
        var button = family.Create<IButton>();

        username.SetText("user@example.com");
        rememberMe.Toggle();

        return new WidgetRenderSummary(username.Render(), rememberMe.Render(), button.Render());
    }
}

public sealed record WidgetRenderSummary(string UserName, string RememberMe, string Submit);
