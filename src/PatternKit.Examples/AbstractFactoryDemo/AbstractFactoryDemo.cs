using PatternKit.Creational.AbstractFactory;

namespace PatternKit.Examples.AbstractFactoryDemo;

/// <summary>
/// Demonstrates the Abstract Factory pattern for creating families of related UI components.
/// This example shows a cross-platform UI toolkit that creates consistent widget families.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-world scenario:</b> Building a desktop application that needs to support multiple
/// operating systems (Windows, macOS, Linux) with native-looking UI components.
/// </para>
/// <para>
/// <b>Key GoF concepts demonstrated:</b>
/// <list type="bullet">
/// <item>Abstract products (IButton, ITextBox, ICheckBox)</item>
/// <item>Concrete product families (Windows, macOS, Linux widgets)</item>
/// <item>Factory isolation - client code doesn't know which concrete family it's using</item>
/// </list>
/// </para>
/// </remarks>
public static class AbstractFactoryDemo
{
    // ─────────────────────────────────────────────────────────────────────────
    // Abstract Products - Define the interface for each type of UI widget
    // ─────────────────────────────────────────────────────────────────────────

    public interface IButton
    {
        string Render();
        void Click();
    }

    public interface ITextBox
    {
        string Render();
        void SetText(string text);
        string GetText();
    }

    public interface ICheckBox
    {
        string Render();
        void Toggle();
        bool IsChecked { get; }
    }

    public interface IDialog
    {
        string Render();
        void Show(string title, string message);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Windows Product Family
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class WindowsButton : IButton
    {
        public string Render() => "[====== Windows Button ======]";
        public void Click() => Console.WriteLine("Windows button clicked with WM_CLICK message");
    }

    public sealed class WindowsTextBox : ITextBox
    {
        private string _text = string.Empty;
        public string Render() => $"|__ {_text,-20} __|  (Win32 EDIT control)";
        public void SetText(string text) => _text = text;
        public string GetText() => _text;
    }

    public sealed class WindowsCheckBox : ICheckBox
    {
        public bool IsChecked { get; private set; }
        public string Render() => IsChecked ? "[X] Windows Checkbox" : "[ ] Windows Checkbox";
        public void Toggle() => IsChecked = !IsChecked;
    }

    public sealed class WindowsDialog : IDialog
    {
        public string Render() => "+--[ Windows Dialog ]--+";
        public void Show(string title, string message) =>
            Console.WriteLine($"MessageBox.Show(\"{message}\", \"{title}\")");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // macOS Product Family
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class MacButton : IButton
    {
        public string Render() => "( macOS Button )";
        public void Click() => Console.WriteLine("Cocoa NSButton sendAction triggered");
    }

    public sealed class MacTextBox : ITextBox
    {
        private string _text = string.Empty;
        public string Render() => $"╭─ {_text,-20} ─╮  (NSTextField)";
        public void SetText(string text) => _text = text;
        public string GetText() => _text;
    }

    public sealed class MacCheckBox : ICheckBox
    {
        public bool IsChecked { get; private set; }
        public string Render() => IsChecked ? "◉ macOS Checkbox" : "○ macOS Checkbox";
        public void Toggle() => IsChecked = !IsChecked;
    }

    public sealed class MacDialog : IDialog
    {
        public string Render() => "╭──[ macOS Sheet ]──╮";
        public void Show(string title, string message) =>
            Console.WriteLine($"NSAlert.runModal() with '{message}'");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Linux/GTK Product Family
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class LinuxButton : IButton
    {
        public string Render() => "< GTK Button >";
        public void Click() => Console.WriteLine("GTK+ g_signal_emit 'clicked' event");
    }

    public sealed class LinuxTextBox : ITextBox
    {
        private string _text = string.Empty;
        public string Render() => $"[{_text,-20}]  (GtkEntry)";
        public void SetText(string text) => _text = text;
        public string GetText() => _text;
    }

    public sealed class LinuxCheckBox : ICheckBox
    {
        public bool IsChecked { get; private set; }
        public string Render() => IsChecked ? "[✓] GTK Checkbox" : "[_] GTK Checkbox";
        public void Toggle() => IsChecked = !IsChecked;
    }

    public sealed class LinuxDialog : IDialog
    {
        public string Render() => "┌──[ GTK Dialog ]──┐";
        public void Show(string title, string message) =>
            Console.WriteLine($"gtk_message_dialog_new() with '{message}'");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Platform Detection
    // ─────────────────────────────────────────────────────────────────────────

    public enum Platform { Windows, MacOS, Linux }

    public static Platform DetectPlatform()
    {
        // In real app: use RuntimeInformation.IsOSPlatform()
        // For demo: simulate detection
        return OperatingSystem.IsWindows() ? Platform.Windows
             : OperatingSystem.IsMacOS() ? Platform.MacOS
             : Platform.Linux;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Factory Configuration using PatternKit
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a platform-specific UI factory using PatternKit's AbstractFactory pattern.
    /// Each platform (Windows, macOS, Linux) is a separate product family.
    /// </summary>
    public static AbstractFactory<Platform> CreateUIFactory()
    {
        return AbstractFactory<Platform>.Create()
            // Windows family
            .Family(Platform.Windows)
                .Product<IButton>(() => new WindowsButton())
                .Product<ITextBox>(() => new WindowsTextBox())
                .Product<ICheckBox>(() => new WindowsCheckBox())
                .Product<IDialog>(() => new WindowsDialog())

            // macOS family
            .Family(Platform.MacOS)
                .Product<IButton>(() => new MacButton())
                .Product<ITextBox>(() => new MacTextBox())
                .Product<ICheckBox>(() => new MacCheckBox())
                .Product<IDialog>(() => new MacDialog())

            // Linux family
            .Family(Platform.Linux)
                .Product<IButton>(() => new LinuxButton())
                .Product<ITextBox>(() => new LinuxTextBox())
                .Product<ICheckBox>(() => new LinuxCheckBox())
                .Product<IDialog>(() => new LinuxDialog())

            .Build();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Client Code - Platform Agnostic
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Demonstrates platform-agnostic UI construction.
    /// The client code doesn't know which platform's widgets it's using.
    /// </summary>
    public static void RenderLoginForm(AbstractFactory<Platform>.ProductFamily widgets)
    {
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("         LOGIN FORM (Platform Agnostic)        ");
        Console.WriteLine("═══════════════════════════════════════════════");

        // Create widgets without knowing the concrete platform
        var usernameBox = widgets.Create<ITextBox>();
        var passwordBox = widgets.Create<ITextBox>();
        var rememberMe = widgets.Create<ICheckBox>();
        var loginButton = widgets.Create<IButton>();
        var dialog = widgets.Create<IDialog>();

        // Configure widgets
        usernameBox.SetText("user@example.com");
        passwordBox.SetText("••••••••");
        rememberMe.Toggle(); // Check "remember me"

        // Render the form
        Console.WriteLine("\n  Username:");
        Console.WriteLine($"    {usernameBox.Render()}");
        Console.WriteLine("\n  Password:");
        Console.WriteLine($"    {passwordBox.Render()}");
        Console.WriteLine($"\n  {rememberMe.Render()}");
        Console.WriteLine($"\n  {loginButton.Render()}");

        // Simulate interaction
        Console.WriteLine("\n--- User clicks Login ---");
        loginButton.Click();
        dialog.Show("Success", "Welcome back!");
        Console.WriteLine();
    }

    /// <summary>
    /// Runs the complete Abstract Factory demonstration.
    /// </summary>
    public static void Run()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        ABSTRACT FACTORY PATTERN DEMONSTRATION                 ║");
        Console.WriteLine("║   Cross-Platform UI Toolkit with Native Look and Feel        ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

        // Create the factory with all platform families registered
        var factory = CreateUIFactory();

        // Demonstrate each platform
        foreach (var platform in new[] { Platform.Windows, Platform.MacOS, Platform.Linux })
        {
            Console.WriteLine($"\n▶ Platform: {platform}");
            Console.WriteLine(new string('─', 50));

            var widgets = factory.GetFamily(platform);
            RenderLoginForm(widgets);
        }

        // Demonstrate runtime platform detection
        Console.WriteLine("\n▶ Auto-detected Platform:");
        Console.WriteLine(new string('─', 50));
        var currentPlatform = DetectPlatform();
        Console.WriteLine($"  Detected: {currentPlatform}");
        var autoWidgets = factory.GetFamily(currentPlatform);
        RenderLoginForm(autoWidgets);

        // Demonstrate TryGetFamily
        Console.WriteLine("\n▶ Safe Family Access:");
        Console.WriteLine(new string('─', 50));
        if (factory.TryGetFamily(Platform.Windows, out var winFamily))
        {
            Console.WriteLine("  Windows family found:");
            Console.WriteLine($"    Can create IButton: {winFamily.CanCreate<IButton>()}");
            Console.WriteLine($"    Can create IDialog: {winFamily.CanCreate<IDialog>()}");
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
        Console.WriteLine("Pattern Benefits Demonstrated:");
        Console.WriteLine("  • Client code is completely platform-agnostic");
        Console.WriteLine("  • Adding a new platform requires no client code changes");
        Console.WriteLine("  • Products within a family are guaranteed to be compatible");
        Console.WriteLine("  • Runtime platform switching is trivial");
        Console.WriteLine("  • Type-safe product creation via generics");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }
}
