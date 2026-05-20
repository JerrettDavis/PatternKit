using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.AbstractFactoryDemo;
using static PatternKit.Examples.AbstractFactoryDemo.AbstractFactoryDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.AbstractFactoryDemoTests;

public sealed class AbstractFactoryDemoTests
{
    [Scenario("CreateUIFactory Creates All Platform Families")]
    [Fact]
    public void CreateUIFactory_Creates_All_Platform_Families()
    {
        var factory = CreateUIFactory();

        ScenarioExpect.True(factory.TryGetFamily(Platform.Windows, out var winFamily));
        ScenarioExpect.True(factory.TryGetFamily(Platform.MacOS, out var macFamily));
        ScenarioExpect.True(factory.TryGetFamily(Platform.Linux, out var linuxFamily));

        ScenarioExpect.NotNull(winFamily);
        ScenarioExpect.NotNull(macFamily);
        ScenarioExpect.NotNull(linuxFamily);
    }

    [Scenario("Generated Factory Creates Platform Widget Families")]
    [Fact]
    public void Generated_Factory_Creates_Platform_Widget_Families()
    {
        var factory = GeneratedPlatformWidgetFactory.Create();

        var windows = factory.GetFamily(Platform.Windows);
        var linux = factory.GetFamily(Platform.Linux);

        ScenarioExpect.Contains("Windows Button", windows.Create<IButton>().Render());
        ScenarioExpect.Contains("GTK Button", linux.Create<IButton>().Render());
    }

    [Scenario("Generated Factory ServiceProvider Overload Creates Platform Widget Families")]
    [Fact]
    public void Generated_Factory_ServiceProvider_Overload_Creates_Platform_Widget_Families()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = CreateUIFactory(services);

        var mac = factory.GetFamily(Platform.MacOS);

        ScenarioExpect.Contains("macOS Button", mac.Create<IButton>().Render());
    }

    [Scenario("Windows Family Creates All Widget Types")]
    [Fact]
    public void Windows_Family_Creates_All_Widget_Types()
    {
        var factory = CreateUIFactory();
        var family = factory.GetFamily(Platform.Windows);

        ScenarioExpect.True(family.CanCreate<IButton>());
        ScenarioExpect.True(family.CanCreate<ITextBox>());
        ScenarioExpect.True(family.CanCreate<ICheckBox>());
        ScenarioExpect.True(family.CanCreate<IDialog>());
    }

    [Scenario("MacOS Family Creates All Widget Types")]
    [Fact]
    public void MacOS_Family_Creates_All_Widget_Types()
    {
        var factory = CreateUIFactory();
        var family = factory.GetFamily(Platform.MacOS);

        ScenarioExpect.True(family.CanCreate<IButton>());
        ScenarioExpect.True(family.CanCreate<ITextBox>());
        ScenarioExpect.True(family.CanCreate<ICheckBox>());
        ScenarioExpect.True(family.CanCreate<IDialog>());
    }

    [Scenario("Linux Family Creates All Widget Types")]
    [Fact]
    public void Linux_Family_Creates_All_Widget_Types()
    {
        var factory = CreateUIFactory();
        var family = factory.GetFamily(Platform.Linux);

        ScenarioExpect.True(family.CanCreate<IButton>());
        ScenarioExpect.True(family.CanCreate<ITextBox>());
        ScenarioExpect.True(family.CanCreate<ICheckBox>());
        ScenarioExpect.True(family.CanCreate<IDialog>());
    }

    [Scenario("WindowsButton Renders And Clicks")]
    [Fact]
    public void WindowsButton_Renders_And_Clicks()
    {
        var button = new WindowsButton();

        var render = button.Render();

        ScenarioExpect.Contains("Windows Button", render);
    }

    [Scenario("WindowsTextBox SetText And GetText")]
    [Fact]
    public void WindowsTextBox_SetText_And_GetText()
    {
        var textBox = new WindowsTextBox();

        textBox.SetText("Hello");
        var text = textBox.GetText();
        var render = textBox.Render();

        ScenarioExpect.Equal("Hello", text);
        ScenarioExpect.Contains("Hello", render);
    }

    [Scenario("WindowsCheckBox Toggle Changes State")]
    [Fact]
    public void WindowsCheckBox_Toggle_Changes_State()
    {
        var checkBox = new WindowsCheckBox();

        ScenarioExpect.False(checkBox.IsChecked);
        checkBox.Toggle();
        ScenarioExpect.True(checkBox.IsChecked);
        ScenarioExpect.Contains("[X]", checkBox.Render());
    }

    [Scenario("MacButton Renders And Clicks")]
    [Fact]
    public void MacButton_Renders_And_Clicks()
    {
        var button = new MacButton();

        var render = button.Render();

        ScenarioExpect.Contains("macOS Button", render);
    }

    [Scenario("MacTextBox SetText And GetText")]
    [Fact]
    public void MacTextBox_SetText_And_GetText()
    {
        var textBox = new MacTextBox();

        textBox.SetText("World");
        var render = textBox.Render();

        ScenarioExpect.Contains("World", render);
        ScenarioExpect.Contains("NSTextField", render);
    }

    [Scenario("MacCheckBox Toggle Changes State")]
    [Fact]
    public void MacCheckBox_Toggle_Changes_State()
    {
        var checkBox = new MacCheckBox();

        ScenarioExpect.False(checkBox.IsChecked);
        ScenarioExpect.Contains("○", checkBox.Render());

        checkBox.Toggle();
        ScenarioExpect.True(checkBox.IsChecked);
        ScenarioExpect.Contains("◉", checkBox.Render());
    }

    [Scenario("LinuxButton Renders And Clicks")]
    [Fact]
    public void LinuxButton_Renders_And_Clicks()
    {
        var button = new LinuxButton();

        var render = button.Render();

        ScenarioExpect.Contains("GTK Button", render);
    }

    [Scenario("LinuxTextBox SetText And GetText")]
    [Fact]
    public void LinuxTextBox_SetText_And_GetText()
    {
        var textBox = new LinuxTextBox();

        textBox.SetText("GTK Text");
        var render = textBox.Render();

        ScenarioExpect.Contains("GTK Text", render);
        ScenarioExpect.Contains("GtkEntry", render);
    }

    [Scenario("LinuxCheckBox Toggle Changes State")]
    [Fact]
    public void LinuxCheckBox_Toggle_Changes_State()
    {
        var checkBox = new LinuxCheckBox();

        ScenarioExpect.False(checkBox.IsChecked);
        checkBox.Toggle();
        ScenarioExpect.True(checkBox.IsChecked);
        ScenarioExpect.Contains("[✓]", checkBox.Render());
    }

    [Scenario("WindowsDialog Renders")]
    [Fact]
    public void WindowsDialog_Renders()
    {
        var dialog = new WindowsDialog();

        var render = dialog.Render();

        ScenarioExpect.Contains("Windows Dialog", render);
    }

    [Scenario("MacDialog Renders")]
    [Fact]
    public void MacDialog_Renders()
    {
        var dialog = new MacDialog();

        var render = dialog.Render();

        ScenarioExpect.Contains("macOS Sheet", render);
    }

    [Scenario("LinuxDialog Renders")]
    [Fact]
    public void LinuxDialog_Renders()
    {
        var dialog = new LinuxDialog();

        var render = dialog.Render();

        ScenarioExpect.Contains("GTK Dialog", render);
    }

    [Scenario("DetectPlatform Returns Valid Platform")]
    [Fact]
    public void DetectPlatform_Returns_Valid_Platform()
    {
        var platform = DetectPlatform();

        ScenarioExpect.True(platform == Platform.Windows ||
                    platform == Platform.MacOS ||
                    platform == Platform.Linux);
    }

    [Scenario("RenderLoginForm Does Not Throw")]
    [Fact]
    public void RenderLoginForm_Does_Not_Throw()
    {
        var factory = CreateUIFactory();
        var widgets = factory.GetFamily(Platform.Windows);

        // Just verify it runs without throwing
        RenderLoginForm(widgets);
    }

    [Scenario("Run Executes Without Errors")]
    [Fact]
    public void Run_Executes_Without_Errors()
    {
        // Just verify the demo runs without throwing
        PatternKit.Examples.AbstractFactoryDemo.AbstractFactoryDemo.Run();
    }
}
