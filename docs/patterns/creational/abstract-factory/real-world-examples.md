# Abstract Factory Pattern Real-World Examples

Production-ready examples demonstrating the Abstract Factory pattern in real-world scenarios.

---

## Example 1: Cross-Platform UI Toolkit

### The Problem

A desktop application needs to run on Windows, macOS, and Linux with native-looking UI components. Each platform has different visual styles, interaction patterns, and underlying APIs.

### The Solution

Use Abstract Factory to create platform-specific widget families that all conform to the same interfaces.

### The Code

```csharp
// Product interfaces
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

public interface IDialog
{
    void Show(string title, string message);
}

public interface ICheckBox
{
    string Render();
    bool IsChecked { get; set; }
}

// Windows family
public sealed class WindowsButton : IButton
{
    public string Render() => "[====== Windows Button ======]";
    public void Click() => Console.WriteLine("WM_CLICK dispatched");
}

public sealed class WindowsTextBox : ITextBox
{
    private string _text = "";
    public string Render() => $"|__ {_text,-20} __| (Win32 EDIT)";
    public void SetText(string text) => _text = text;
    public string GetText() => _text;
}

// macOS family
public sealed class MacButton : IButton
{
    public string Render() => "( macOS Button )";
    public void Click() => Console.WriteLine("NSButton sendAction:");
}

public sealed class MacTextBox : ITextBox
{
    private string _text = "";
    public string Render() => $"╭─ {_text,-20} ─╮ (NSTextField)";
    public void SetText(string text) => _text = text;
    public string GetText() => _text;
}

// Platform detection
public enum Platform { Windows, MacOS, Linux }

public static Platform DetectPlatform() =>
    OperatingSystem.IsWindows() ? Platform.Windows
    : OperatingSystem.IsMacOS() ? Platform.MacOS
    : Platform.Linux;

// Factory configuration
public static AbstractFactory<Platform> CreateUIFactory()
{
    return AbstractFactory<Platform>.Create()
        .Family(Platform.Windows)
            .Product<IButton>(() => new WindowsButton())
            .Product<ITextBox>(() => new WindowsTextBox())
            .Product<IDialog>(() => new WindowsDialog())
            .Product<ICheckBox>(() => new WindowsCheckBox())

        .Family(Platform.MacOS)
            .Product<IButton>(() => new MacButton())
            .Product<ITextBox>(() => new MacTextBox())
            .Product<IDialog>(() => new MacDialog())
            .Product<ICheckBox>(() => new MacCheckBox())

        .Family(Platform.Linux)
            .Product<IButton>(() => new LinuxButton())
            .Product<ITextBox>(() => new LinuxTextBox())
            .Product<IDialog>(() => new LinuxDialog())
            .Product<ICheckBox>(() => new LinuxCheckBox())

        .Build();
}

// Platform-agnostic UI code
public class LoginForm
{
    private readonly AbstractFactory<Platform>.ProductFamily _widgets;

    public LoginForm(AbstractFactory<Platform>.ProductFamily widgets)
    {
        _widgets = widgets;
    }

    public void Render()
    {
        var usernameBox = _widgets.Create<ITextBox>();
        var passwordBox = _widgets.Create<ITextBox>();
        var rememberMe = _widgets.Create<ICheckBox>();
        var loginButton = _widgets.Create<IButton>();

        usernameBox.SetText("user@example.com");

        Console.WriteLine("Username: " + usernameBox.Render());
        Console.WriteLine("Password: " + passwordBox.Render());
        Console.WriteLine(rememberMe.Render());
        Console.WriteLine(loginButton.Render());
    }
}

// Usage
var factory = CreateUIFactory();
var platform = DetectPlatform();
var widgets = factory.GetFamily(platform);
var loginForm = new LoginForm(widgets);
loginForm.Render();
```

### Why This Pattern

- **Native look**: Each platform gets appropriate visual styling
- **Single codebase**: UI logic written once, works everywhere
- **Extensibility**: Add new platforms without changing existing code
- **Consistency**: All widgets in a family are guaranteed compatible

---

## Example 2: Database Provider Abstraction

### The Problem

An enterprise application needs to support multiple database backends (SQL Server, PostgreSQL, SQLite) for different deployment scenarios while keeping data access code database-agnostic.

### The Solution

Create database provider families with matching connection, command, and parameter types.

### The Code

```csharp
// Product interfaces
public interface IDbConnection : IDisposable
{
    void Open();
    void Close();
    string ConnectionString { get; set; }
}

public interface IDbCommand : IDisposable
{
    string CommandText { get; set; }
    IDbConnection Connection { get; set; }
    void AddParameter(IDbParameter parameter);
    int ExecuteNonQuery();
    object ExecuteScalar();
}

public interface IDbParameter
{
    string Name { get; set; }
    object Value { get; set; }
    DbType DbType { get; set; }
}

public interface IDbTransaction : IDisposable
{
    void Commit();
    void Rollback();
}

// Provider enum
public enum DatabaseProvider { SqlServer, PostgreSQL, SQLite }

// Factory configuration
public static AbstractFactory<DatabaseProvider> CreateDatabaseFactory(
    string sqlServerConnStr,
    string postgresConnStr,
    string sqliteConnStr)
{
    return AbstractFactory<DatabaseProvider>.Create()
        .Family(DatabaseProvider.SqlServer)
            .Product<IDbConnection>(() => new SqlServerConnection(sqlServerConnStr))
            .Product<IDbCommand>(() => new SqlServerCommand())
            .Product<IDbParameter>(() => new SqlServerParameter())
            .Product<IDbTransaction>(() => new SqlServerTransaction())

        .Family(DatabaseProvider.PostgreSQL)
            .Product<IDbConnection>(() => new PostgreSqlConnection(postgresConnStr))
            .Product<IDbCommand>(() => new PostgreSqlCommand())
            .Product<IDbParameter>(() => new PostgreSqlParameter())
            .Product<IDbTransaction>(() => new PostgreSqlTransaction())

        .Family(DatabaseProvider.SQLite)
            .Product<IDbConnection>(() => new SQLiteConnection(sqliteConnStr))
            .Product<IDbCommand>(() => new SQLiteCommand())
            .Product<IDbParameter>(() => new SQLiteParameter())
            .Product<IDbTransaction>(() => new SQLiteTransaction())

        .Build();
}

// Database-agnostic repository
public class UserRepository
{
    private readonly AbstractFactory<DatabaseProvider>.ProductFamily _db;

    public UserRepository(AbstractFactory<DatabaseProvider>.ProductFamily db)
    {
        _db = db;
    }

    public User GetById(int id)
    {
        using var connection = _db.Create<IDbConnection>();
        using var command = _db.Create<IDbCommand>();

        var param = _db.Create<IDbParameter>();
        param.Name = "@id";
        param.Value = id;
        param.DbType = DbType.Int32;

        command.Connection = connection;
        command.CommandText = "SELECT * FROM Users WHERE Id = @id";
        command.AddParameter(param);

        connection.Open();
        // Execute and map...
        return MapUser(command.ExecuteScalar());
    }
}

// Usage
var dbFactory = CreateDatabaseFactory(
    "Server=localhost;Database=App;",
    "Host=localhost;Database=App;",
    "Data Source=app.db");

// Select provider based on configuration
var provider = Enum.Parse<DatabaseProvider>(config["DatabaseProvider"]);
var db = dbFactory.GetFamily(provider);
var userRepo = new UserRepository(db);
```

### Why This Pattern

- **Provider independence**: Switch databases without code changes
- **Consistent API**: Same methods work across all providers
- **Deployment flexibility**: Use SQLite for dev, PostgreSQL for production
- **Type compatibility**: Connection, command, and parameters from same family work together

---

## Example 3: Document Export System

### The Problem

A reporting system needs to export documents in multiple formats (PDF, Excel, Word) with format-specific styling, layouts, and rendering capabilities.

### The Solution

Create document format families with matching document, page, and element factories.

### The Code

```csharp
// Product interfaces
public interface IDocument
{
    void AddPage(IPage page);
    byte[] Render();
    string MimeType { get; }
}

public interface IPage
{
    void AddElement(IElement element);
    void SetMargins(int top, int right, int bottom, int left);
}

public interface IElement
{
    void SetContent(string content);
    void SetStyle(ElementStyle style);
}

public interface ITable : IElement
{
    void AddRow(params string[] cells);
    void SetColumnWidths(params int[] widths);
}

public interface IChart : IElement
{
    void SetData(ChartData data);
    void SetChartType(ChartType type);
}

// Format enum
public enum DocumentFormat { Pdf, Excel, Word, Html }

// Factory configuration
public static AbstractFactory<DocumentFormat> CreateDocumentFactory()
{
    return AbstractFactory<DocumentFormat>.Create()
        .Family(DocumentFormat.Pdf)
            .Product<IDocument>(() => new PdfDocument())
            .Product<IPage>(() => new PdfPage())
            .Product<IElement>(() => new PdfTextElement())
            .Product<ITable>(() => new PdfTable())
            .Product<IChart>(() => new PdfChart())

        .Family(DocumentFormat.Excel)
            .Product<IDocument>(() => new ExcelWorkbook())
            .Product<IPage>(() => new ExcelWorksheet())
            .Product<IElement>(() => new ExcelCell())
            .Product<ITable>(() => new ExcelRange())
            .Product<IChart>(() => new ExcelChart())

        .Family(DocumentFormat.Word)
            .Product<IDocument>(() => new WordDocument())
            .Product<IPage>(() => new WordSection())
            .Product<IElement>(() => new WordParagraph())
            .Product<ITable>(() => new WordTable())
            .Product<IChart>(() => new WordChart())

        .Family(DocumentFormat.Html)
            .Product<IDocument>(() => new HtmlDocument())
            .Product<IPage>(() => new HtmlSection())
            .Product<IElement>(() => new HtmlElement())
            .Product<ITable>(() => new HtmlTable())
            .Product<IChart>(() => new HtmlChart())

        .Build();
}

// Format-agnostic report generator
public class SalesReportGenerator
{
    private readonly AbstractFactory<DocumentFormat>.ProductFamily _docFactory;

    public SalesReportGenerator(AbstractFactory<DocumentFormat>.ProductFamily docFactory)
    {
        _docFactory = docFactory;
    }

    public byte[] Generate(SalesData data)
    {
        var document = _docFactory.Create<IDocument>();

        // Title page
        var titlePage = _docFactory.Create<IPage>();
        var title = _docFactory.Create<IElement>();
        title.SetContent("Quarterly Sales Report");
        title.SetStyle(new ElementStyle { FontSize = 24, Bold = true });
        titlePage.AddElement(title);
        document.AddPage(titlePage);

        // Data page
        var dataPage = _docFactory.Create<IPage>();

        // Sales table
        var table = _docFactory.Create<ITable>();
        table.AddRow("Region", "Q1", "Q2", "Q3", "Q4", "Total");
        foreach (var region in data.Regions)
        {
            table.AddRow(
                region.Name,
                region.Q1.ToString("C"),
                region.Q2.ToString("C"),
                region.Q3.ToString("C"),
                region.Q4.ToString("C"),
                region.Total.ToString("C"));
        }
        dataPage.AddElement(table);

        // Sales chart
        var chart = _docFactory.Create<IChart>();
        chart.SetChartType(ChartType.Bar);
        chart.SetData(data.ToChartData());
        dataPage.AddElement(chart);

        document.AddPage(dataPage);

        return document.Render();
    }
}

// Usage
var docFactory = CreateDocumentFactory();
var pdfExporter = new SalesReportGenerator(docFactory.GetFamily(DocumentFormat.Pdf));
var excelExporter = new SalesReportGenerator(docFactory.GetFamily(DocumentFormat.Excel));

byte[] pdfReport = pdfExporter.Generate(salesData);
byte[] excelReport = excelExporter.Generate(salesData);
```

### Why This Pattern

- **Format independence**: Same report logic produces PDF, Excel, or Word
- **Consistent structure**: Documents have same logical structure across formats
- **Easy format switching**: User selects format, same code runs
- **Format-specific rendering**: Each format optimizes for its capabilities

---

## Example 4: Game Skin System

### The Problem

A mobile game needs multiple visual themes (default, seasonal, premium) where all game elements must match the selected theme.

### The Solution

Create theme families with matching sprites, sounds, and effects.

### The Code

```csharp
// Product interfaces
public interface ISprite
{
    string AssetPath { get; }
    void Render(int x, int y);
}

public interface ISoundEffect
{
    string AssetPath { get; }
    void Play();
}

public interface IParticleEffect
{
    void Emit(int x, int y, int count);
}

public interface IBackground
{
    void Render();
    Color DominantColor { get; }
}

// Theme enum
public enum GameTheme { Default, Halloween, Christmas, Premium }

// Factory configuration
public static AbstractFactory<GameTheme> CreateThemeFactory()
{
    return AbstractFactory<GameTheme>.Create()
        .Family(GameTheme.Default)
            .Product<ISprite>(() => new DefaultHeroSprite())
            .Product<ISoundEffect>(() => new DefaultSounds())
            .Product<IParticleEffect>(() => new DefaultParticles())
            .Product<IBackground>(() => new DefaultBackground())

        .Family(GameTheme.Halloween)
            .Product<ISprite>(() => new HalloweenHeroSprite())
            .Product<ISoundEffect>(() => new SpookySounds())
            .Product<IParticleEffect>(() => new BatParticles())
            .Product<IBackground>(() => new HauntedBackground())

        .Family(GameTheme.Christmas)
            .Product<ISprite>(() => new ChristmasHeroSprite())
            .Product<ISoundEffect>(() => new JingleSounds())
            .Product<IParticleEffect>(() => new SnowflakeParticles())
            .Product<IBackground>(() => new WinterWonderlandBackground())

        .Family(GameTheme.Premium)
            .Product<ISprite>(() => new GoldenHeroSprite())
            .Product<ISoundEffect>(() => new OrchestraSounds())
            .Product<IParticleEffect>(() => new SparkleParticles())
            .Product<IBackground>(() => new EpicBackground())

        .Build();
}

// Theme-agnostic game renderer
public class GameRenderer
{
    private AbstractFactory<GameTheme>.ProductFamily _theme;

    public void SetTheme(AbstractFactory<GameTheme>.ProductFamily theme)
    {
        _theme = theme;
    }

    public void RenderFrame()
    {
        // All elements automatically match the selected theme
        var background = _theme.Create<IBackground>();
        var hero = _theme.Create<ISprite>();
        var effects = _theme.Create<IParticleEffect>();

        background.Render();
        hero.Render(100, 200);
        effects.Emit(100, 200, 10);
    }

    public void PlayHitSound()
    {
        var sound = _theme.Create<ISoundEffect>();
        sound.Play();
    }
}

// Usage
var themeFactory = CreateThemeFactory();
var renderer = new GameRenderer();

// Set theme based on season or user purchase
var theme = GetCurrentTheme(); // Halloween during October
renderer.SetTheme(themeFactory.GetFamily(theme));
```

### Why This Pattern

- **Visual consistency**: All elements match the selected theme
- **Easy theme switching**: Change one setting, entire game transforms
- **Monetization**: Premium themes as in-app purchases
- **Seasonal content**: Easy deployment of holiday themes

---

## Key Takeaways

1. **Family consistency**: Products in a family are designed to work together

2. **Runtime flexibility**: Select families at runtime based on configuration or context

3. **Interface-driven**: Products are accessed through interfaces, hiding concrete types

4. **Single responsibility**: Factory handles creation, products handle behavior

5. **Extension-friendly**: Add new families without modifying existing code

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
- [AbstractFactoryDemo.cs](/src/PatternKit.Examples/AbstractFactoryDemo/AbstractFactoryDemo.cs) - Cross-platform UI example
