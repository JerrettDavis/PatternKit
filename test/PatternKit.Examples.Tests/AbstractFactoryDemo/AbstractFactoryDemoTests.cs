using PatternKit.Examples.AbstractFactoryDemo;
using static PatternKit.Examples.AbstractFactoryDemo.AbstractFactoryDemo;

namespace PatternKit.Examples.Tests.AbstractFactoryDemoTests;

public sealed class AbstractFactoryDemoTests
{
    [Fact]
    public void CreateUIFactory_Creates_All_Platform_Families()
    {
        var factory = CreateUIFactory();

        Assert.True(factory.TryGetFamily(Platform.Windows, out var winFamily));
        Assert.True(factory.TryGetFamily(Platform.MacOS, out var macFamily));
        Assert.True(factory.TryGetFamily(Platform.Linux, out var linuxFamily));

        Assert.NotNull(winFamily);
        Assert.NotNull(macFamily);
        Assert.NotNull(linuxFamily);
    }

    [Fact]
    public void Windows_Family_Creates_All_Widget_Types()
    {
        var factory = CreateUIFactory();
        var family = factory.GetFamily(Platform.Windows);

        Assert.True(family.CanCreate<IButton>());
        Assert.True(family.CanCreate<ITextBox>());
        Assert.True(family.CanCreate<ICheckBox>());
        Assert.True(family.CanCreate<IDialog>());
    }

    [Fact]
    public void MacOS_Family_Creates_All_Widget_Types()
    {
        var factory = CreateUIFactory();
        var family = factory.GetFamily(Platform.MacOS);

        Assert.True(family.CanCreate<IButton>());
        Assert.True(family.CanCreate<ITextBox>());
        Assert.True(family.CanCreate<ICheckBox>());
        Assert.True(family.CanCreate<IDialog>());
    }

    [Fact]
    public void Linux_Family_Creates_All_Widget_Types()
    {
        var factory = CreateUIFactory();
        var family = factory.GetFamily(Platform.Linux);

        Assert.True(family.CanCreate<IButton>());
        Assert.True(family.CanCreate<ITextBox>());
        Assert.True(family.CanCreate<ICheckBox>());
        Assert.True(family.CanCreate<IDialog>());
    }

    [Fact]
    public void WindowsButton_Renders_And_Clicks()
    {
        var button = new WindowsButton();

        var render = button.Render();

        Assert.Contains("Windows Button", render);
    }

    [Fact]
    public void WindowsTextBox_SetText_And_GetText()
    {
        var textBox = new WindowsTextBox();

        textBox.SetText("Hello");
        var text = textBox.GetText();
        var render = textBox.Render();

        Assert.Equal("Hello", text);
        Assert.Contains("Hello", render);
    }

    [Fact]
    public void WindowsCheckBox_Toggle_Changes_State()
    {
        var checkBox = new WindowsCheckBox();

        Assert.False(checkBox.IsChecked);
        checkBox.Toggle();
        Assert.True(checkBox.IsChecked);
        Assert.Contains("[X]", checkBox.Render());
    }

    [Fact]
    public void MacButton_Renders_And_Clicks()
    {
        var button = new MacButton();

        var render = button.Render();

        Assert.Contains("macOS Button", render);
    }

    [Fact]
    public void MacTextBox_SetText_And_GetText()
    {
        var textBox = new MacTextBox();

        textBox.SetText("World");
        var render = textBox.Render();

        Assert.Contains("World", render);
        Assert.Contains("NSTextField", render);
    }

    [Fact]
    public void MacCheckBox_Toggle_Changes_State()
    {
        var checkBox = new MacCheckBox();

        Assert.False(checkBox.IsChecked);
        Assert.Contains("○", checkBox.Render());

        checkBox.Toggle();
        Assert.True(checkBox.IsChecked);
        Assert.Contains("◉", checkBox.Render());
    }

    [Fact]
    public void LinuxButton_Renders_And_Clicks()
    {
        var button = new LinuxButton();

        var render = button.Render();

        Assert.Contains("GTK Button", render);
    }

    [Fact]
    public void LinuxTextBox_SetText_And_GetText()
    {
        var textBox = new LinuxTextBox();

        textBox.SetText("GTK Text");
        var render = textBox.Render();

        Assert.Contains("GTK Text", render);
        Assert.Contains("GtkEntry", render);
    }

    [Fact]
    public void LinuxCheckBox_Toggle_Changes_State()
    {
        var checkBox = new LinuxCheckBox();

        Assert.False(checkBox.IsChecked);
        checkBox.Toggle();
        Assert.True(checkBox.IsChecked);
        Assert.Contains("[✓]", checkBox.Render());
    }

    [Fact]
    public void WindowsDialog_Renders()
    {
        var dialog = new WindowsDialog();

        var render = dialog.Render();

        Assert.Contains("Windows Dialog", render);
    }

    [Fact]
    public void MacDialog_Renders()
    {
        var dialog = new MacDialog();

        var render = dialog.Render();

        Assert.Contains("macOS Sheet", render);
    }

    [Fact]
    public void LinuxDialog_Renders()
    {
        var dialog = new LinuxDialog();

        var render = dialog.Render();

        Assert.Contains("GTK Dialog", render);
    }

    [Fact]
    public void DetectPlatform_Returns_Valid_Platform()
    {
        var platform = DetectPlatform();

        Assert.True(platform == Platform.Windows ||
                    platform == Platform.MacOS ||
                    platform == Platform.Linux);
    }

    [Fact]
    public void RenderLoginForm_Does_Not_Throw()
    {
        var factory = CreateUIFactory();
        var widgets = factory.GetFamily(Platform.Windows);

        // Just verify it runs without throwing
        RenderLoginForm(widgets);
    }

    [Fact]
    public void Run_Executes_Without_Errors()
    {
        // Just verify the demo runs without throwing
        PatternKit.Examples.AbstractFactoryDemo.AbstractFactoryDemo.Run();
    }
}
