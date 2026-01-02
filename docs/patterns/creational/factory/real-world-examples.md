# Factory Pattern Real-World Examples

Production-ready examples demonstrating the Factory pattern in real-world scenarios.

---

## Example 1: Payment Gateway Integration

### The Problem

An e-commerce system needs to support multiple payment gateways (Stripe, PayPal, Square) with the ability to add new gateways without modifying existing code.

### The Solution

Use Factory to register payment processors by gateway name.

### The Code

```csharp
public interface IPaymentGateway
{
    Task<PaymentResult> ProcessAsync(PaymentRequest request, CancellationToken ct);
    Task<RefundResult> RefundAsync(string transactionId, decimal amount, CancellationToken ct);
}

public class PaymentGatewayFactory
{
    private readonly Factory<string, IServiceProvider, IPaymentGateway> _factory;

    public PaymentGatewayFactory(IConfiguration config)
    {
        _factory = Factory<string, IServiceProvider, IPaymentGateway>
            .Create(StringComparer.OrdinalIgnoreCase)
            .Map("stripe", (in IServiceProvider sp) => new StripeGateway(
                sp.GetRequiredService<IHttpClientFactory>(),
                config["Stripe:ApiKey"]))
            .Map("paypal", (in IServiceProvider sp) => new PayPalGateway(
                sp.GetRequiredService<IHttpClientFactory>(),
                config["PayPal:ClientId"],
                config["PayPal:Secret"]))
            .Map("square", (in IServiceProvider sp) => new SquareGateway(
                sp.GetRequiredService<IHttpClientFactory>(),
                config["Square:AccessToken"]))
            .Build();
    }

    public IPaymentGateway GetGateway(string gatewayName, IServiceProvider serviceProvider)
    {
        if (!_factory.TryCreate(gatewayName, serviceProvider, out var gateway))
        {
            throw new NotSupportedException($"Payment gateway '{gatewayName}' is not supported");
        }
        return gateway;
    }

    public IEnumerable<string> SupportedGateways => new[] { "stripe", "paypal", "square" };
}

// Usage in controller
[HttpPost("checkout")]
public async Task<IActionResult> Checkout(
    [FromBody] CheckoutRequest request,
    [FromServices] PaymentGatewayFactory gatewayFactory,
    [FromServices] IServiceProvider serviceProvider)
{
    var gateway = gatewayFactory.GetGateway(request.PaymentMethod, serviceProvider);

    var result = await gateway.ProcessAsync(new PaymentRequest
    {
        Amount = request.Amount,
        Currency = request.Currency,
        CardToken = request.CardToken,
        OrderId = request.OrderId
    }, HttpContext.RequestAborted);

    if (result.Success)
        return Ok(new { transactionId = result.TransactionId });

    return BadRequest(new { error = result.ErrorMessage });
}
```

### Why This Pattern

- **Extensible**: Add new gateways without modifying existing code
- **Testable**: Mock individual gateways for testing
- **Configuration-driven**: Gateway selection from request/config

---

## Example 2: Report Generator Factory

### The Problem

A reporting system needs to generate reports in multiple formats (PDF, Excel, CSV, HTML) with format-specific rendering logic.

### The Solution

Use Factory to map format strings to report generators.

### The Code

```csharp
public interface IReportGenerator
{
    byte[] Generate(ReportData data);
    string ContentType { get; }
    string FileExtension { get; }
}

public class ReportGeneratorFactory
{
    private readonly Factory<string, ReportOptions, IReportGenerator> _factory;

    public ReportGeneratorFactory()
    {
        _factory = Factory<string, ReportOptions, IReportGenerator>
            .Create(StringComparer.OrdinalIgnoreCase)
            .Map("pdf", (in ReportOptions opts) => new PdfReportGenerator(opts))
            .Map("excel", (in ReportOptions opts) => new ExcelReportGenerator(opts))
            .Map("xlsx", (in ReportOptions opts) => new ExcelReportGenerator(opts))
            .Map("csv", (in ReportOptions opts) => new CsvReportGenerator(opts))
            .Map("html", (in ReportOptions opts) => new HtmlReportGenerator(opts))
            .Default((in ReportOptions opts) => new PdfReportGenerator(opts))
            .Build();
    }

    public IReportGenerator GetGenerator(string format, ReportOptions options = null) =>
        _factory.Create(format, options ?? new ReportOptions());

    public bool IsFormatSupported(string format) =>
        _factory.TryCreate(format, new ReportOptions(), out _);
}

public class PdfReportGenerator : IReportGenerator
{
    private readonly ReportOptions _options;

    public PdfReportGenerator(ReportOptions options) => _options = options;

    public string ContentType => "application/pdf";
    public string FileExtension => ".pdf";

    public byte[] Generate(ReportData data)
    {
        using var doc = new PdfDocument();
        var page = doc.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        // Render header
        gfx.DrawString(data.Title, _options.TitleFont, XBrushes.Black, 50, 50);

        // Render data table
        var y = 100;
        foreach (var row in data.Rows)
        {
            var x = 50;
            foreach (var cell in row)
            {
                gfx.DrawString(cell.ToString(), _options.BodyFont, XBrushes.Black, x, y);
                x += 100;
            }
            y += 20;
        }

        using var stream = new MemoryStream();
        doc.Save(stream);
        return stream.ToArray();
    }
}

// Usage
var factory = new ReportGeneratorFactory();
var generator = factory.GetGenerator(request.Format, new ReportOptions
{
    PageSize = PageSize.A4,
    Orientation = Orientation.Landscape
});

var reportBytes = generator.Generate(reportData);

return File(reportBytes, generator.ContentType,
    $"report-{DateTime.UtcNow:yyyyMMdd}{generator.FileExtension}");
```

### Why This Pattern

- **Format flexibility**: Easy to add new formats
- **Content negotiation**: Select format based on request
- **Encapsulated logic**: Format-specific code isolated

---

## Example 3: Database Provider Factory

### The Problem

A multi-tenant application needs to support multiple database providers (SQL Server, PostgreSQL, MySQL, SQLite) per tenant configuration.

### The Solution

Use Factory to create database connections based on provider type.

### The Code

```csharp
public interface IDatabaseProvider
{
    IDbConnection CreateConnection(string connectionString);
    string QuoteIdentifier(string identifier);
    string GetLastInsertIdQuery();
}

public class DatabaseProviderFactory
{
    private readonly Factory<string, IDatabaseProvider> _factory;

    public DatabaseProviderFactory()
    {
        _factory = Factory<string, IDatabaseProvider>
            .Create(StringComparer.OrdinalIgnoreCase)
            .Map("sqlserver", static () => new SqlServerProvider())
            .Map("mssql", static () => new SqlServerProvider())
            .Map("postgresql", static () => new PostgreSqlProvider())
            .Map("postgres", static () => new PostgreSqlProvider())
            .Map("mysql", static () => new MySqlProvider())
            .Map("mariadb", static () => new MySqlProvider())
            .Map("sqlite", static () => new SqliteProvider())
            .Build();
    }

    public IDatabaseProvider GetProvider(string providerName)
    {
        if (!_factory.TryCreate(providerName, out var provider))
        {
            throw new NotSupportedException(
                $"Database provider '{providerName}' is not supported. " +
                "Supported providers: sqlserver, postgresql, mysql, sqlite");
        }
        return provider;
    }
}

public class SqlServerProvider : IDatabaseProvider
{
    public IDbConnection CreateConnection(string connectionString) =>
        new SqlConnection(connectionString);

    public string QuoteIdentifier(string identifier) => $"[{identifier}]";

    public string GetLastInsertIdQuery() => "SELECT SCOPE_IDENTITY()";
}

public class PostgreSqlProvider : IDatabaseProvider
{
    public IDbConnection CreateConnection(string connectionString) =>
        new NpgsqlConnection(connectionString);

    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    public string GetLastInsertIdQuery() => "RETURNING id";
}

// Multi-tenant usage
public class TenantDatabaseService
{
    private readonly DatabaseProviderFactory _providerFactory;
    private readonly ITenantConfigService _tenantConfig;

    public TenantDatabaseService(
        DatabaseProviderFactory providerFactory,
        ITenantConfigService tenantConfig)
    {
        _providerFactory = providerFactory;
        _tenantConfig = tenantConfig;
    }

    public IDbConnection GetConnection(string tenantId)
    {
        var config = _tenantConfig.GetConfiguration(tenantId);
        var provider = _providerFactory.GetProvider(config.DatabaseProvider);
        return provider.CreateConnection(config.ConnectionString);
    }
}
```

### Why This Pattern

- **Multi-provider support**: Tenants can use different databases
- **Abstracted differences**: Provider-specific SQL handled internally
- **Easy testing**: Mock providers for unit tests

---

## Example 4: Notification Channel Factory

### The Problem

A notification system needs to send alerts through multiple channels (Email, SMS, Slack, Push) with channel-specific formatting and delivery logic.

### The Solution

Use Factory to create notification senders by channel type.

### The Code

```csharp
public interface INotificationChannel
{
    Task SendAsync(Notification notification, CancellationToken ct);
    bool CanHandle(NotificationType type);
}

public class NotificationChannelFactory
{
    private readonly Factory<string, IServiceProvider, INotificationChannel> _factory;

    public NotificationChannelFactory()
    {
        _factory = Factory<string, IServiceProvider, INotificationChannel>
            .Create(StringComparer.OrdinalIgnoreCase)
            .Map("email", (in IServiceProvider sp) => new EmailChannel(
                sp.GetRequiredService<IEmailService>(),
                sp.GetRequiredService<ITemplateEngine>()))
            .Map("sms", (in IServiceProvider sp) => new SmsChannel(
                sp.GetRequiredService<ISmsService>()))
            .Map("slack", (in IServiceProvider sp) => new SlackChannel(
                sp.GetRequiredService<ISlackClient>()))
            .Map("push", (in IServiceProvider sp) => new PushChannel(
                sp.GetRequiredService<IPushNotificationService>()))
            .Map("webhook", (in IServiceProvider sp) => new WebhookChannel(
                sp.GetRequiredService<IHttpClientFactory>()))
            .Build();
    }

    public INotificationChannel GetChannel(string channelName, IServiceProvider sp)
    {
        if (!_factory.TryCreate(channelName, sp, out var channel))
        {
            throw new ArgumentException($"Unknown notification channel: {channelName}");
        }
        return channel;
    }

    public IEnumerable<INotificationChannel> GetChannelsForUser(
        UserPreferences preferences,
        IServiceProvider sp)
    {
        foreach (var channelName in preferences.EnabledChannels)
        {
            if (_factory.TryCreate(channelName, sp, out var channel))
            {
                yield return channel;
            }
        }
    }
}

// Notification dispatcher
public class NotificationDispatcher
{
    private readonly NotificationChannelFactory _channelFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserPreferencesService _preferences;

    public async Task DispatchAsync(Notification notification, CancellationToken ct)
    {
        var userPrefs = await _preferences.GetAsync(notification.UserId, ct);
        var channels = _channelFactory.GetChannelsForUser(userPrefs, _serviceProvider);

        var tasks = channels
            .Where(c => c.CanHandle(notification.Type))
            .Select(c => c.SendAsync(notification, ct));

        await Task.WhenAll(tasks);
    }
}

// Usage
await dispatcher.DispatchAsync(new Notification
{
    UserId = userId,
    Type = NotificationType.Alert,
    Title = "Server Alert",
    Message = "CPU usage exceeded 90%",
    Priority = Priority.High
}, cancellationToken);
```

### Why This Pattern

- **Channel flexibility**: Easy to add new channels
- **User preferences**: Per-user channel selection
- **Type filtering**: Channels can filter by notification type

---

## Key Takeaways

1. **Keyed registration**: Map identifiers to implementations
2. **Custom comparers**: Control key matching (case-sensitivity)
3. **Default fallback**: Graceful handling of unknown keys
4. **TryCreate for probing**: When missing keys are expected
5. **Immutable after build**: Thread-safe lookup

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
