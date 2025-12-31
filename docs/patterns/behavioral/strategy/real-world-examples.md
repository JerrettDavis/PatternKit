# Strategy Pattern Real-World Examples

Production-ready examples demonstrating the Strategy pattern in real-world scenarios.

---

## Example 1: Content Negotiation API

### The Problem

A REST API needs to serialize responses in different formats (JSON, XML, CSV) based on the client's `Accept` header, with intelligent fallback to JSON.

### The Solution

Use Strategy to select the appropriate serializer based on content type.

### The Code

```csharp
public interface ISerializer
{
    string ContentType { get; }
    string Serialize<T>(T obj);
}

public class JsonSerializer : ISerializer
{
    public string ContentType => "application/json";
    public string Serialize<T>(T obj) => System.Text.Json.JsonSerializer.Serialize(obj);
}

public class XmlSerializer : ISerializer
{
    public string ContentType => "application/xml";
    public string Serialize<T>(T obj)
    {
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
        using var writer = new StringWriter();
        serializer.Serialize(writer, obj);
        return writer.ToString();
    }
}

public class CsvSerializer : ISerializer
{
    public string ContentType => "text/csv";
    public string Serialize<T>(T obj) => ConvertToCsv(obj);
}

// Strategy configuration
public static Strategy<string, ISerializer> CreateContentNegotiator()
{
    return Strategy<string, ISerializer>.Create()
        .When(accept => accept.Contains("application/json"))
            .Then(_ => new JsonSerializer())
        .When(accept => accept.Contains("application/xml"))
            .Then(_ => new XmlSerializer())
        .When(accept => accept.Contains("text/xml"))
            .Then(_ => new XmlSerializer())
        .When(accept => accept.Contains("text/csv"))
            .Then(_ => new CsvSerializer())
        .When(accept => accept == "*/*")
            .Then(_ => new JsonSerializer())
        .Default(_ => new JsonSerializer())
        .Build();
}

// Usage in API controller
[ApiController]
public class ProductsController : ControllerBase
{
    private static readonly Strategy<string, ISerializer> _negotiator = CreateContentNegotiator();

    [HttpGet]
    public IActionResult GetProducts()
    {
        var products = _productService.GetAll();

        var accept = Request.Headers["Accept"].ToString();
        var serializer = _negotiator.Execute(accept);

        return Content(serializer.Serialize(products), serializer.ContentType);
    }
}
```

### Why This Pattern

- **Clean selection logic**: No messy if-else chains
- **Extensible**: Add new formats without modifying existing code
- **Default handling**: JSON fallback for unknown types
- **Reusable**: Same negotiator used across all endpoints

---

## Example 2: Dynamic Pricing Engine

### The Problem

An e-commerce platform needs to calculate prices based on multiple factors: sales, membership tiers, bulk discounts, and promotional codes. Rules have priority - more specific discounts should apply first.

### The Solution

Use Strategy with ordered predicates where most specific rules come first.

### The Code

```csharp
public record PricingContext(
    Item Item,
    Customer Customer,
    int Quantity,
    string? PromoCode);

public static Strategy<PricingContext, decimal> CreatePricingEngine()
{
    return Strategy<PricingContext, decimal>.Create()
        // Priority 1: Promotional codes (most specific)
        .When(ctx => ctx.PromoCode == "FLASH50")
            .Then(ctx => ctx.Item.BasePrice * 0.50m * ctx.Quantity)

        .When(ctx => ctx.PromoCode == "SUMMER20")
            .Then(ctx => ctx.Item.BasePrice * 0.80m * ctx.Quantity)

        // Priority 2: Bulk discounts
        .When(ctx => ctx.Quantity >= 100)
            .Then(ctx => ctx.Item.BasePrice * 0.70m * ctx.Quantity)

        .When(ctx => ctx.Quantity >= 50)
            .Then(ctx => ctx.Item.BasePrice * 0.80m * ctx.Quantity)

        .When(ctx => ctx.Quantity >= 20)
            .Then(ctx => ctx.Item.BasePrice * 0.90m * ctx.Quantity)

        // Priority 3: Membership tiers
        .When(ctx => ctx.Customer.Tier == CustomerTier.Platinum)
            .Then(ctx => ctx.Item.BasePrice * 0.85m * ctx.Quantity)

        .When(ctx => ctx.Customer.Tier == CustomerTier.Gold)
            .Then(ctx => ctx.Item.BasePrice * 0.90m * ctx.Quantity)

        .When(ctx => ctx.Customer.Tier == CustomerTier.Silver)
            .Then(ctx => ctx.Item.BasePrice * 0.95m * ctx.Quantity)

        // Priority 4: Item-level sales
        .When(ctx => ctx.Item.IsOnClearance)
            .Then(ctx => ctx.Item.ClearancePrice * ctx.Quantity)

        .When(ctx => ctx.Item.IsOnSale)
            .Then(ctx => ctx.Item.SalePrice * ctx.Quantity)

        // Default: Regular price
        .Default(ctx => ctx.Item.BasePrice * ctx.Quantity)
        .Build();
}

// Usage
var pricingEngine = CreatePricingEngine();

var context = new PricingContext(
    Item: laptop,
    Customer: platinumMember,
    Quantity: 5,
    PromoCode: null);

var totalPrice = pricingEngine.Execute(context);
```

### Why This Pattern

- **Priority handling**: First match wins ensures correct precedence
- **Composable rules**: Each discount is a separate, testable condition
- **Business clarity**: Rules read like requirements
- **Easy updates**: Add/modify rules without restructuring code

---

## Example 3: Multi-Format Date Parser

### The Problem

An import system receives dates in various formats from different sources. It needs to parse dates flexibly, trying multiple formats until one succeeds.

### The Solution

Use TryStrategy to attempt parsing with different formats, falling back gracefully.

### The Code

```csharp
public static TryStrategy<string, DateTime> CreateDateParser()
{
    var culture = CultureInfo.InvariantCulture;
    var style = DateTimeStyles.None;

    return TryStrategy<string, DateTime>.Create()
        // ISO 8601 (most preferred)
        .Always((in string s, out DateTime d) =>
            DateTime.TryParseExact(s, "yyyy-MM-dd", culture, style, out d))

        .Always((in string s, out DateTime d) =>
            DateTime.TryParseExact(s, "yyyy-MM-ddTHH:mm:ss", culture, style, out d))

        .Always((in string s, out DateTime d) =>
            DateTime.TryParseExact(s, "yyyy-MM-ddTHH:mm:ssZ", culture, style, out d))

        // US formats
        .Always((in string s, out DateTime d) =>
            DateTime.TryParseExact(s, "MM/dd/yyyy", culture, style, out d))

        .Always((in string s, out DateTime d) =>
            DateTime.TryParseExact(s, "M/d/yyyy", culture, style, out d))

        // European formats
        .Always((in string s, out DateTime d) =>
            DateTime.TryParseExact(s, "dd/MM/yyyy", culture, style, out d))

        .Always((in string s, out DateTime d) =>
            DateTime.TryParseExact(s, "dd.MM.yyyy", culture, style, out d))

        // Text formats
        .Always((in string s, out DateTime d) =>
            DateTime.TryParseExact(s, "MMMM d, yyyy", culture, style, out d))

        .Always((in string s, out DateTime d) =>
            DateTime.TryParseExact(s, "MMM d, yyyy", culture, style, out d))

        // Generic fallback
        .Always((in string s, out DateTime d) =>
            DateTime.TryParse(s, out d))

        .Build();
}

// Usage in data import
public class DataImporter
{
    private static readonly TryStrategy<string, DateTime> _dateParser = CreateDateParser();

    public ImportResult Import(DataRow row)
    {
        var dateField = row["date"].ToString();

        if (_dateParser.Execute(dateField, out var parsedDate))
        {
            return new ImportResult(true, parsedDate);
        }

        return new ImportResult(false, default, $"Could not parse date: {dateField}");
    }
}
```

### Why This Pattern

- **Graceful fallback**: Tries multiple formats without exceptions
- **Ordered attempts**: Most common formats first for performance
- **Non-throwing**: No exception overhead for parse failures
- **Extensible**: Easy to add new date formats

---

## Example 4: Feature Flag Router

### The Problem

A SaaS application needs to show different features to users based on their subscription tier, A/B test groups, and feature flags. The routing logic is complex with multiple overlapping conditions.

### The Solution

Use AsyncStrategy to determine which feature implementation to serve.

### The Code

```csharp
public record FeatureContext(
    User User,
    string FeatureName,
    Dictionary<string, bool> Flags,
    string? AbTestGroup);

public interface IFeatureHandler
{
    Task<FeatureResult> HandleAsync(FeatureContext ctx, CancellationToken ct);
}

public static AsyncStrategy<FeatureContext, IFeatureHandler> CreateFeatureRouter(
    IServiceProvider services)
{
    return AsyncStrategy<FeatureContext, IFeatureHandler>.Create()
        // Check for specific feature flag overrides first
        .When(ctx => ctx.Flags.GetValueOrDefault($"{ctx.FeatureName}_v2_enabled"))
            .Then(async (ctx, ct) => services.GetRequiredService<V2FeatureHandler>())

        // A/B test routing
        .When(ctx => ctx.AbTestGroup == "experiment_new_checkout")
            .Then(async (ctx, ct) => services.GetRequiredService<NewCheckoutHandler>())

        // Subscription tier features
        .When(ctx => ctx.User.SubscriptionTier == SubscriptionTier.Enterprise)
            .Then(async (ctx, ct) => services.GetRequiredService<EnterpriseFeatureHandler>())

        .When(ctx => ctx.User.SubscriptionTier == SubscriptionTier.Pro)
            .Then(async (ctx, ct) => services.GetRequiredService<ProFeatureHandler>())

        // Beta users get experimental features
        .When(ctx => ctx.User.IsBetaTester && ctx.Flags.GetValueOrDefault("beta_features"))
            .Then(async (ctx, ct) => services.GetRequiredService<BetaFeatureHandler>())

        // Default handler
        .Default(async (ctx, ct) => services.GetRequiredService<DefaultFeatureHandler>())
        .Build();
}

// Usage
public class FeatureService
{
    private readonly AsyncStrategy<FeatureContext, IFeatureHandler> _router;

    public FeatureService(AsyncStrategy<FeatureContext, IFeatureHandler> router)
    {
        _router = router;
    }

    public async Task<FeatureResult> ExecuteFeatureAsync(
        User user,
        string featureName,
        CancellationToken ct)
    {
        var context = new FeatureContext(
            User: user,
            FeatureName: featureName,
            Flags: await GetFeatureFlags(user.Id),
            AbTestGroup: await GetAbTestGroup(user.Id));

        var handler = await _router.ExecuteAsync(context, ct);
        return await handler.HandleAsync(context, ct);
    }
}
```

### Why This Pattern

- **Complex condition handling**: Multiple overlapping rules handled cleanly
- **Priority-based**: More specific conditions take precedence
- **Async support**: Works with async flag/test group lookups
- **Testable**: Each condition can be tested independently

---

## Key Takeaways

1. **First-match-wins**: Order predicates from most specific to least
2. **Use TryStrategy for parsing**: When failure is expected
3. **Async for I/O**: Use async variants when handlers need external calls
4. **Provide defaults**: Avoid unexpected exceptions
5. **Keep predicates pure**: No side effects in conditions

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
- [Strategy.md](./strategy.md) - Original Strategy documentation
- [TryStrategy.md](./trystrategy.md) - Original TryStrategy documentation
