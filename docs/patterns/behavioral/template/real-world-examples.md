# Template Method Pattern Real-World Examples

Production-ready examples demonstrating the Template Method pattern in real-world scenarios.

---

## Example 1: Document Export System

### The Problem

A document management system needs to export documents to multiple formats (PDF, Word, HTML) with consistent pre-processing (permission checks, watermarking) and post-processing (audit logging, notification).

### The Solution

Use Template Method to define the export skeleton with format-specific implementations.

### The Code

```csharp
public class ExportRequest
{
    public Guid DocumentId { get; set; }
    public string Format { get; set; }
    public Guid UserId { get; set; }
    public bool AddWatermark { get; set; }
}

public class ExportResult
{
    public byte[] Content { get; set; }
    public string ContentType { get; set; }
    public string FileName { get; set; }
}

public class DocumentExporter
{
    private readonly Dictionary<string, Template<ExportRequest, ExportResult>> _exporters;
    private readonly IDocumentService _documents;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;

    public DocumentExporter(
        IDocumentService documents,
        IPermissionService permissions,
        IAuditService audit,
        INotificationService notifications)
    {
        _documents = documents;
        _permissions = permissions;
        _audit = audit;
        _notifications = notifications;

        _exporters = new Dictionary<string, Template<ExportRequest, ExportResult>>
        {
            ["pdf"] = CreateExporter(ExportToPdf),
            ["docx"] = CreateExporter(ExportToWord),
            ["html"] = CreateExporter(ExportToHtml)
        };
    }

    private Template<ExportRequest, ExportResult> CreateExporter(
        Func<ExportRequest, Document, ExportResult> exportFunc)
    {
        return Template<ExportRequest, ExportResult>
            .Create(req =>
            {
                var doc = _documents.Get(req.DocumentId);
                if (doc == null)
                    throw new DocumentNotFoundException(req.DocumentId);

                if (req.AddWatermark)
                    doc = ApplyWatermark(doc, req.UserId);

                return exportFunc(req, doc);
            })
            .Before(req =>
            {
                // Permission check
                if (!_permissions.CanExport(req.UserId, req.DocumentId))
                    throw new UnauthorizedAccessException("Export not permitted");
            })
            .Before(req =>
            {
                // Rate limiting
                if (_audit.GetExportCount(req.UserId, TimeSpan.FromHours(1)) > 100)
                    throw new RateLimitException("Export limit exceeded");
            })
            .After((req, result) =>
            {
                // Audit logging
                _audit.LogExport(new ExportAuditEntry
                {
                    UserId = req.UserId,
                    DocumentId = req.DocumentId,
                    Format = req.Format,
                    Timestamp = DateTime.UtcNow,
                    FileSize = result.Content.Length
                });
            })
            .After((req, result) =>
            {
                // Notification for large exports
                if (result.Content.Length > 10_000_000)
                    _notifications.NotifyAdmin($"Large export: {result.FileName}");
            })
            .OnError((req, error) =>
            {
                _audit.LogExportFailure(req.DocumentId, req.UserId, error);
            })
            .Build();
    }

    public ExportResult Export(ExportRequest request)
    {
        if (!_exporters.TryGetValue(request.Format.ToLower(), out var exporter))
            throw new UnsupportedFormatException(request.Format);

        return exporter.Execute(request);
    }

    private ExportResult ExportToPdf(ExportRequest req, Document doc)
    {
        var pdf = PdfGenerator.Generate(doc);
        return new ExportResult
        {
            Content = pdf,
            ContentType = "application/pdf",
            FileName = $"{doc.Title}.pdf"
        };
    }

    private ExportResult ExportToWord(ExportRequest req, Document doc)
    {
        var docx = WordGenerator.Generate(doc);
        return new ExportResult
        {
            Content = docx,
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName = $"{doc.Title}.docx"
        };
    }

    private ExportResult ExportToHtml(ExportRequest req, Document doc)
    {
        var html = HtmlGenerator.Generate(doc);
        return new ExportResult
        {
            Content = Encoding.UTF8.GetBytes(html),
            ContentType = "text/html",
            FileName = $"{doc.Title}.html"
        };
    }
}
```

### Why This Pattern

- **Consistent cross-cutting**: All formats get permission/audit handling
- **Format-specific core**: Each export format has unique implementation
- **Easy to extend**: Add new format by implementing export function

---

## Example 2: Batch Data Importer

### The Problem

An ETL system needs to import data from various sources (CSV, JSON, XML) with validation, transformation, and error collection.

### The Solution

Use async Template Method for file processing with comprehensive error handling.

### The Code

```csharp
public class ImportResult
{
    public int TotalRows { get; set; }
    public int SuccessfulRows { get; set; }
    public int FailedRows { get; set; }
    public List<ImportError> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class BatchImporter
{
    private readonly AsyncTemplate<string, ImportResult> _template;
    private readonly IDataStore _store;
    private readonly IValidator _validator;
    private readonly INotificationService _notifications;

    public BatchImporter(
        IDataStore store,
        IValidator validator,
        INotificationService notifications,
        ILogger logger)
    {
        _store = store;
        _validator = validator;
        _notifications = notifications;

        _template = AsyncTemplate<string, ImportResult>
            .Create(async (filePath, ct) =>
            {
                var result = new ImportResult();
                var sw = Stopwatch.StartNew();

                var rows = await ParseFileAsync(filePath, ct);
                result.TotalRows = rows.Count;

                foreach (var row in rows)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var validationResult = await _validator.ValidateAsync(row, ct);
                        if (!validationResult.IsValid)
                        {
                            result.FailedRows++;
                            result.Errors.Add(new ImportError
                            {
                                RowNumber = row.RowNumber,
                                Message = string.Join("; ", validationResult.Errors)
                            });
                            continue;
                        }

                        var transformed = Transform(row);
                        await _store.InsertAsync(transformed, ct);
                        result.SuccessfulRows++;
                    }
                    catch (Exception ex)
                    {
                        result.FailedRows++;
                        result.Errors.Add(new ImportError
                        {
                            RowNumber = row.RowNumber,
                            Message = ex.Message
                        });
                    }
                }

                result.Duration = sw.Elapsed;
                return result;
            })
            .Before(async (filePath, ct) =>
            {
                // Validate file exists and is accessible
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Import file not found: {filePath}");

                var info = new FileInfo(filePath);
                if (info.Length > 100_000_000) // 100MB limit
                    throw new FileTooLargeException(filePath, info.Length);

                logger.LogInformation("Starting import: {Path} ({Size} bytes)",
                    filePath, info.Length);
            })
            .Before(async (filePath, ct) =>
            {
                // Create backup
                var backupPath = $"{filePath}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
                File.Copy(filePath, backupPath);
                logger.LogInformation("Backup created: {BackupPath}", backupPath);
            })
            .After(async (filePath, result, ct) =>
            {
                logger.LogInformation(
                    "Import complete: {Success}/{Total} rows in {Duration}",
                    result.SuccessfulRows, result.TotalRows, result.Duration);

                if (result.FailedRows > 0)
                {
                    var errorReport = GenerateErrorReport(result.Errors);
                    await File.WriteAllTextAsync(
                        $"{filePath}.errors.csv",
                        errorReport,
                        ct);
                }
            })
            .After(async (filePath, result, ct) =>
            {
                // Notify on completion
                var successRate = (double)result.SuccessfulRows / result.TotalRows * 100;
                if (successRate < 90)
                {
                    await _notifications.NotifyAsync(
                        "Import completed with issues",
                        $"Success rate: {successRate:F1}%, {result.FailedRows} failures",
                        ct);
                }
            })
            .OnError(async (filePath, error, ct) =>
            {
                logger.LogError("Import failed: {Path} - {Error}", filePath, error);
                await _notifications.NotifyAsync(
                    "Import failed",
                    $"File: {filePath}\nError: {error}",
                    ct);
            })
            .Build();
    }

    public Task<ImportResult> ImportAsync(string filePath, CancellationToken ct = default)
        => _template.ExecuteAsync(filePath, ct).AsTask();
}
```

### Why This Pattern

- **Comprehensive pipeline**: Validation, backup, error reporting built in
- **Progress tracking**: Result accumulates during processing
- **Error isolation**: Individual row failures don't stop import

---

## Example 3: API Request Pipeline

### The Problem

A web API needs consistent request handling with authentication, validation, rate limiting, and response formatting.

### The Solution

Use Template Method to create a reusable request pipeline.

### The Code

```csharp
public class ApiPipeline<TRequest, TResponse>
    where TRequest : IApiRequest
    where TResponse : class
{
    private readonly Template<ApiContext<TRequest>, ApiResult<TResponse>> _template;

    public ApiPipeline(
        Func<TRequest, TResponse> handler,
        IAuthService auth,
        IRateLimiter rateLimiter,
        IMetrics metrics,
        ILogger logger)
    {
        _template = Template<ApiContext<TRequest>, ApiResult<TResponse>>
            .Create(ctx =>
            {
                var result = handler(ctx.Request);
                return ApiResult<TResponse>.Success(result);
            })
            // Authentication
            .Before(ctx =>
            {
                if (ctx.Request.RequiresAuth)
                {
                    var user = auth.ValidateToken(ctx.AuthToken);
                    if (user == null)
                        throw new UnauthorizedException();
                    ctx.User = user;
                }
            })
            // Authorization
            .Before(ctx =>
            {
                if (ctx.Request.RequiredPermission != null)
                {
                    if (!ctx.User.HasPermission(ctx.Request.RequiredPermission))
                        throw new ForbiddenException();
                }
            })
            // Rate limiting
            .Before(ctx =>
            {
                var key = ctx.User?.Id ?? ctx.ClientIp;
                if (!rateLimiter.TryAcquire(key))
                    throw new RateLimitException();
            })
            // Request validation
            .Before(ctx =>
            {
                var errors = ctx.Request.Validate();
                if (errors.Any())
                    throw new ValidationException(errors);
            })
            // Metrics
            .Before(ctx =>
            {
                ctx.StartTime = DateTime.UtcNow;
                metrics.Increment($"api.{typeof(TRequest).Name}.started");
            })
            .After((ctx, result) =>
            {
                var duration = DateTime.UtcNow - ctx.StartTime;
                metrics.Timing($"api.{typeof(TRequest).Name}.duration", duration);
                metrics.Increment($"api.{typeof(TRequest).Name}.success");
            })
            // Logging
            .After((ctx, result) =>
            {
                logger.LogInformation(
                    "{Request} completed in {Duration}ms for {User}",
                    typeof(TRequest).Name,
                    (DateTime.UtcNow - ctx.StartTime).TotalMilliseconds,
                    ctx.User?.Id ?? "anonymous");
            })
            .OnError((ctx, error) =>
            {
                metrics.Increment($"api.{typeof(TRequest).Name}.error");
                logger.LogError(
                    "Request {Request} failed: {Error}",
                    typeof(TRequest).Name, error);
            })
            .Build();
    }

    public ApiResult<TResponse> Execute(ApiContext<TRequest> context)
    {
        try
        {
            return _template.Execute(context);
        }
        catch (UnauthorizedException)
        {
            return ApiResult<TResponse>.Unauthorized();
        }
        catch (ForbiddenException)
        {
            return ApiResult<TResponse>.Forbidden();
        }
        catch (ValidationException ex)
        {
            return ApiResult<TResponse>.BadRequest(ex.Errors);
        }
        catch (RateLimitException)
        {
            return ApiResult<TResponse>.TooManyRequests();
        }
        catch (Exception ex)
        {
            return ApiResult<TResponse>.InternalError(ex.Message);
        }
    }
}

// Usage
var getUserPipeline = new ApiPipeline<GetUserRequest, UserDto>(
    req => userService.GetUser(req.UserId),
    authService, rateLimiter, metrics, logger);

var result = getUserPipeline.Execute(new ApiContext<GetUserRequest>
{
    Request = new GetUserRequest { UserId = 123 },
    AuthToken = "Bearer ...",
    ClientIp = "192.168.1.1"
});
```

### Why This Pattern

- **Reusable pipeline**: Same security/logging for all endpoints
- **Consistent error handling**: All errors map to proper responses
- **Observable**: Metrics and logging built in

---

## Example 4: Report Generation System

### The Problem

A business intelligence system generates reports with common setup (connection, security context), format-specific generation, and common cleanup (email, archive).

### The Solution

Use inheritance-based Template Method for report variations.

### The Code

```csharp
public abstract class ReportGeneratorBase : TemplateMethod<ReportRequest, GeneratedReport>
{
    protected readonly IDbConnection Connection;
    protected readonly IEmailService Email;
    protected readonly IStorageService Storage;
    protected readonly ILogger Logger;

    protected ReportGeneratorBase(
        IDbConnection connection,
        IEmailService email,
        IStorageService storage,
        ILogger logger)
    {
        Connection = connection;
        Email = email;
        Storage = storage;
        Logger = logger;
    }

    protected override void Before(ReportRequest request)
    {
        Logger.LogInformation(
            "Generating {ReportType} report for period {From} to {To}",
            GetReportType(), request.FromDate, request.ToDate);

        // Set security context
        Connection.SetUserContext(request.UserId);
    }

    // Subclasses implement this
    protected abstract override GeneratedReport Step(ReportRequest request);

    protected abstract string GetReportType();

    protected override void After(ReportRequest request, GeneratedReport result)
    {
        // Archive report
        var archivePath = $"reports/{GetReportType()}/{DateTime.UtcNow:yyyy/MM}/{result.FileName}";
        Storage.Upload(archivePath, result.Content);
        Logger.LogInformation("Report archived to {Path}", archivePath);

        // Email if requested
        if (request.EmailTo != null)
        {
            Email.SendWithAttachment(
                request.EmailTo,
                $"{GetReportType()} Report",
                $"Please find the {GetReportType()} report attached.",
                result.FileName,
                result.Content);
        }
    }

    protected override void OnError(ReportRequest request, Exception exception)
    {
        Logger.LogError(exception,
            "Failed to generate {ReportType} report for {User}",
            GetReportType(), request.UserId);

        // Notify admin
        Email.Send(
            "admin@company.com",
            $"Report Generation Failed: {GetReportType()}",
            $"Error: {exception.Message}\nUser: {request.UserId}");
    }
}

public class SalesReport : ReportGeneratorBase
{
    public SalesReport(IDbConnection connection, IEmailService email,
        IStorageService storage, ILogger logger)
        : base(connection, email, storage, logger) { }

    protected override string GetReportType() => "Sales";

    protected override GeneratedReport Step(ReportRequest request)
    {
        var data = Connection.Query<SalesData>(
            "SELECT * FROM Sales WHERE Date BETWEEN @From AND @To",
            new { From = request.FromDate, To = request.ToDate });

        var excel = ExcelBuilder.Create()
            .AddSheet("Sales Summary", CreateSummary(data))
            .AddSheet("Sales Details", data)
            .AddChart("Sales Trend", CreateTrendChart(data))
            .Build();

        return new GeneratedReport
        {
            FileName = $"Sales_{request.FromDate:yyyyMMdd}_{request.ToDate:yyyyMMdd}.xlsx",
            Content = excel,
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }
}

public class InventoryReport : ReportGeneratorBase
{
    public InventoryReport(IDbConnection connection, IEmailService email,
        IStorageService storage, ILogger logger)
        : base(connection, email, storage, logger) { }

    protected override string GetReportType() => "Inventory";

    protected override GeneratedReport Step(ReportRequest request)
    {
        var data = Connection.Query<InventoryItem>("SELECT * FROM Inventory");

        var pdf = PdfBuilder.Create()
            .AddTitle("Inventory Report")
            .AddTable(data)
            .AddFooter($"Generated: {DateTime.UtcNow}")
            .Build();

        return new GeneratedReport
        {
            FileName = $"Inventory_{DateTime.UtcNow:yyyyMMdd}.pdf",
            Content = pdf,
            ContentType = "application/pdf"
        };
    }
}
```

### Why This Pattern

- **Inheritance-based**: Natural for report family
- **Shared infrastructure**: Connection, email, storage in base
- **Format flexibility**: Each report type chooses output format

---

## Key Takeaways

1. **Hooks for cross-cutting**: Auth, logging, metrics in Before/After
2. **Core step is the variable**: Only the essential logic changes
3. **Error hooks for recovery**: Logging, notification, cleanup
4. **Sync vs Async**: Use AsyncTemplate for I/O operations
5. **Inheritance when appropriate**: For families of related operations

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
