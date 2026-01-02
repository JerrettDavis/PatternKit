# Builder Pattern Real-World Examples

Production-ready examples demonstrating the Builder pattern in real-world scenarios.

---

## Example 1: HTTP Request Builder

### The Problem

An HTTP client library needs to construct complex requests with headers, query parameters, body content, and authentication.

### The Solution

Use MutableBuilder to fluently construct HTTP requests with validation.

### The Code

```csharp
public class HttpRequestBuilder
{
    private readonly MutableBuilder<HttpRequestMessage> _builder;

    public HttpRequestBuilder(HttpMethod method, string baseUrl)
    {
        _builder = MutableBuilder<HttpRequestMessage>
            .New(() => new HttpRequestMessage(method, baseUrl))
            .Require(r => r.RequestUri != null ? null : "URL is required");
    }

    public HttpRequestBuilder WithHeader(string name, string value)
    {
        _builder.With(r => r.Headers.Add(name, value));
        return this;
    }

    public HttpRequestBuilder WithBearerToken(string token)
    {
        _builder.With(r => r.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token));
        return this;
    }

    public HttpRequestBuilder WithQueryParam(string name, string value)
    {
        _builder.With(r =>
        {
            var uri = r.RequestUri!;
            var separator = uri.Query.Length > 0 ? "&" : "?";
            r.RequestUri = new Uri($"{uri}{separator}{name}={Uri.EscapeDataString(value)}");
        });
        return this;
    }

    public HttpRequestBuilder WithJsonBody<T>(T content)
    {
        _builder.With(r =>
        {
            var json = JsonSerializer.Serialize(content);
            r.Content = new StringContent(json, Encoding.UTF8, "application/json");
        });
        return this;
    }

    public HttpRequestBuilder WithTimeout(TimeSpan timeout)
    {
        _builder.With(r => r.Options.Set(
            new HttpRequestOptionsKey<TimeSpan>("Timeout"), timeout));
        return this;
    }

    public HttpRequestMessage Build() => _builder.Build();
}

// Usage
var request = new HttpRequestBuilder(HttpMethod.Post, "https://api.example.com/orders")
    .WithBearerToken(authToken)
    .WithHeader("X-Request-Id", Guid.NewGuid().ToString())
    .WithQueryParam("version", "2")
    .WithJsonBody(new { customerId = 123, items = new[] { "A", "B" } })
    .WithTimeout(TimeSpan.FromSeconds(30))
    .Build();

var response = await httpClient.SendAsync(request);
```

### Why This Pattern

- **Fluent API**: Readable request construction
- **Validation**: Required fields enforced
- **Extensible**: Easy to add new configuration options

---

## Example 2: SQL Query Builder

### The Problem

An ORM needs to construct SQL queries dynamically with proper escaping, joins, and conditions.

### The Solution

Use builder pattern to safely construct parameterized queries.

### The Code

```csharp
public class QueryBuilder
{
    private readonly MutableBuilder<Query> _builder;

    public QueryBuilder(string tableName)
    {
        _builder = MutableBuilder<Query>
            .New(() => new Query { TableName = tableName })
            .Require(q => !string.IsNullOrEmpty(q.TableName) ? null : "Table name required")
            .Require(q => q.Columns.Count > 0 ? null : "At least one column required");
    }

    public QueryBuilder Select(params string[] columns)
    {
        _builder.With(q => q.Columns.AddRange(columns));
        return this;
    }

    public QueryBuilder Where(string column, string op, object value)
    {
        _builder.With(q =>
        {
            var paramName = $"@p{q.Parameters.Count}";
            q.Conditions.Add($"{column} {op} {paramName}");
            q.Parameters[paramName] = value;
        });
        return this;
    }

    public QueryBuilder WhereIn(string column, IEnumerable<object> values)
    {
        _builder.With(q =>
        {
            var paramNames = new List<string>();
            foreach (var value in values)
            {
                var paramName = $"@p{q.Parameters.Count}";
                paramNames.Add(paramName);
                q.Parameters[paramName] = value;
            }
            q.Conditions.Add($"{column} IN ({string.Join(", ", paramNames)})");
        });
        return this;
    }

    public QueryBuilder OrderBy(string column, bool ascending = true)
    {
        _builder.With(q => q.OrderBy.Add($"{column} {(ascending ? "ASC" : "DESC")}"));
        return this;
    }

    public QueryBuilder Limit(int count)
    {
        _builder.With(q => q.Limit = count);
        return this;
    }

    public Query Build() => _builder.Build();
}

public class Query
{
    public string TableName { get; set; }
    public List<string> Columns { get; } = new();
    public List<string> Conditions { get; } = new();
    public List<string> OrderBy { get; } = new();
    public Dictionary<string, object> Parameters { get; } = new();
    public int? Limit { get; set; }

    public string ToSql()
    {
        var sql = $"SELECT {string.Join(", ", Columns)} FROM {TableName}";

        if (Conditions.Any())
            sql += $" WHERE {string.Join(" AND ", Conditions)}";

        if (OrderBy.Any())
            sql += $" ORDER BY {string.Join(", ", OrderBy)}";

        if (Limit.HasValue)
            sql += $" LIMIT {Limit}";

        return sql;
    }
}

// Usage
var query = new QueryBuilder("orders")
    .Select("id", "customer_id", "total", "created_at")
    .Where("status", "=", "pending")
    .Where("total", ">", 100.00m)
    .WhereIn("customer_id", new object[] { 1, 2, 3 })
    .OrderBy("created_at", ascending: false)
    .Limit(10)
    .Build();

// query.ToSql() = "SELECT id, customer_id, total, created_at FROM orders
//                  WHERE status = @p0 AND total > @p1 AND customer_id IN (@p2, @p3, @p4)
//                  ORDER BY created_at DESC LIMIT 10"
```

### Why This Pattern

- **SQL injection safe**: Parameters properly escaped
- **Fluent API**: Natural query construction
- **Validation**: Required elements enforced

---

## Example 3: Email Message Builder

### The Problem

An email service needs to construct complex messages with recipients, attachments, templates, and delivery options.

### The Solution

Use builder pattern for fluent email composition with comprehensive validation.

### The Code

```csharp
public class EmailBuilder
{
    private readonly MutableBuilder<EmailMessage> _builder;

    public EmailBuilder()
    {
        _builder = MutableBuilder<EmailMessage>
            .New(() => new EmailMessage { Id = Guid.NewGuid() })
            .Require(e => e.To.Count > 0 ? null : "At least one recipient required")
            .Require(e => !string.IsNullOrEmpty(e.Subject) ? null : "Subject required")
            .Require(e => !string.IsNullOrEmpty(e.Body) || e.TemplateId != null
                ? null : "Body or template required")
            .Require(e => e.Attachments.Sum(a => a.Size) <= 25_000_000
                ? null : "Total attachments exceed 25MB limit");
    }

    public EmailBuilder To(string email, string name = null)
    {
        _builder.With(e => e.To.Add(new EmailAddress(email, name)));
        return this;
    }

    public EmailBuilder Cc(string email, string name = null)
    {
        _builder.With(e => e.Cc.Add(new EmailAddress(email, name)));
        return this;
    }

    public EmailBuilder Bcc(string email, string name = null)
    {
        _builder.With(e => e.Bcc.Add(new EmailAddress(email, name)));
        return this;
    }

    public EmailBuilder From(string email, string name = null)
    {
        _builder.With(e => e.From = new EmailAddress(email, name));
        return this;
    }

    public EmailBuilder ReplyTo(string email)
    {
        _builder.With(e => e.ReplyTo = email);
        return this;
    }

    public EmailBuilder Subject(string subject)
    {
        _builder.With(e => e.Subject = subject);
        return this;
    }

    public EmailBuilder Body(string body, bool isHtml = false)
    {
        _builder.With(e =>
        {
            e.Body = body;
            e.IsHtml = isHtml;
        });
        return this;
    }

    public EmailBuilder UseTemplate(string templateId, object data)
    {
        _builder.With(e =>
        {
            e.TemplateId = templateId;
            e.TemplateData = data;
        });
        return this;
    }

    public EmailBuilder Attach(string filename, byte[] content, string contentType)
    {
        _builder.With(e => e.Attachments.Add(new EmailAttachment
        {
            Filename = filename,
            Content = content,
            ContentType = contentType,
            Size = content.Length
        }));
        return this;
    }

    public EmailBuilder Priority(EmailPriority priority)
    {
        _builder.With(e => e.Priority = priority);
        return this;
    }

    public EmailBuilder ScheduleFor(DateTime sendAt)
    {
        _builder.With(e => e.ScheduledFor = sendAt);
        return this;
    }

    public EmailMessage Build() => _builder.Build();
}

// Usage
var email = new EmailBuilder()
    .From("noreply@company.com", "Company Name")
    .To("customer@example.com", "John Doe")
    .Cc("manager@company.com")
    .Subject("Order Confirmation #12345")
    .UseTemplate("order-confirmation", new
    {
        OrderId = "12345",
        CustomerName = "John Doe",
        Items = new[] { "Widget", "Gadget" },
        Total = 99.99m
    })
    .Attach("invoice.pdf", invoiceBytes, "application/pdf")
    .Priority(EmailPriority.High)
    .ScheduleFor(DateTime.UtcNow.AddMinutes(5))
    .Build();

await emailService.SendAsync(email);
```

### Why This Pattern

- **Complex construction**: Many optional fields
- **Validation**: Business rules enforced (attachments size, required fields)
- **Readable**: Intent clear from method names

---

## Example 4: Report Configuration Builder

### The Problem

A reporting system needs to configure complex reports with columns, filters, groupings, and export options.

### The Solution

Use builder pattern with nested builders for complex report configuration.

### The Code

```csharp
public class ReportBuilder
{
    private readonly MutableBuilder<ReportConfig> _builder;

    public ReportBuilder(string name)
    {
        _builder = MutableBuilder<ReportConfig>
            .New(() => new ReportConfig { Name = name, CreatedAt = DateTime.UtcNow })
            .Require(r => r.Columns.Count > 0 ? null : "At least one column required")
            .Require(r => !string.IsNullOrEmpty(r.DataSource) ? null : "Data source required");
    }

    public ReportBuilder FromDataSource(string source)
    {
        _builder.With(r => r.DataSource = source);
        return this;
    }

    public ReportBuilder AddColumn(Action<ColumnBuilder> configure)
    {
        var columnBuilder = new ColumnBuilder();
        configure(columnBuilder);
        _builder.With(r => r.Columns.Add(columnBuilder.Build()));
        return this;
    }

    public ReportBuilder Filter(string field, FilterOperator op, object value)
    {
        _builder.With(r => r.Filters.Add(new ReportFilter
        {
            Field = field,
            Operator = op,
            Value = value
        }));
        return this;
    }

    public ReportBuilder GroupBy(params string[] fields)
    {
        _builder.With(r => r.GroupByFields.AddRange(fields));
        return this;
    }

    public ReportBuilder SortBy(string field, bool descending = false)
    {
        _builder.With(r => r.SortFields.Add((field, descending)));
        return this;
    }

    public ReportBuilder Paginate(int pageSize)
    {
        _builder.With(r => r.PageSize = pageSize);
        return this;
    }

    public ReportBuilder ExportAs(params ExportFormat[] formats)
    {
        _builder.With(r => r.ExportFormats.AddRange(formats));
        return this;
    }

    public ReportConfig Build() => _builder.Build();
}

public class ColumnBuilder
{
    private readonly MutableBuilder<ReportColumn> _builder;

    public ColumnBuilder()
    {
        _builder = MutableBuilder<ReportColumn>
            .New(() => new ReportColumn())
            .Require(c => !string.IsNullOrEmpty(c.Field) ? null : "Field required");
    }

    public ColumnBuilder Field(string field)
    {
        _builder.With(c => c.Field = field);
        return this;
    }

    public ColumnBuilder Title(string title)
    {
        _builder.With(c => c.Title = title);
        return this;
    }

    public ColumnBuilder Width(int width)
    {
        _builder.With(c => c.Width = width);
        return this;
    }

    public ColumnBuilder Format(string format)
    {
        _builder.With(c => c.Format = format);
        return this;
    }

    public ColumnBuilder Aggregate(AggregateFunction func)
    {
        _builder.With(c => c.Aggregate = func);
        return this;
    }

    public ReportColumn Build() => _builder.Build();
}

// Usage
var report = new ReportBuilder("Monthly Sales Report")
    .FromDataSource("sales_transactions")
    .AddColumn(c => c.Field("date").Title("Date").Format("yyyy-MM-dd"))
    .AddColumn(c => c.Field("product").Title("Product").Width(200))
    .AddColumn(c => c.Field("quantity").Title("Qty").Aggregate(AggregateFunction.Sum))
    .AddColumn(c => c.Field("amount").Title("Amount").Format("C").Aggregate(AggregateFunction.Sum))
    .Filter("date", FilterOperator.GreaterThanOrEqual, DateTime.Today.AddMonths(-1))
    .Filter("status", FilterOperator.Equals, "completed")
    .GroupBy("product", "date")
    .SortBy("date", descending: true)
    .Paginate(50)
    .ExportAs(ExportFormat.Pdf, ExportFormat.Excel, ExportFormat.Csv)
    .Build();

var data = await reportService.GenerateAsync(report);
```

### Why This Pattern

- **Nested builders**: Complex objects within objects
- **Fluent DSL**: Domain-specific language for reports
- **Validation**: Required fields and business rules

---

## Key Takeaways

1. **Fluent API**: Method chaining for readable configuration
2. **Validation integration**: Require() for business rules
3. **Builder reuse**: Same builder can produce multiple instances
4. **Nested builders**: For complex hierarchical objects
5. **Static lambdas**: Avoid closure allocations

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
