# Facade Pattern Real-World Examples

Production-ready examples demonstrating the Facade pattern in PatternKit.

---

## Example 1: E-Commerce Checkout System

### The Problem

An e-commerce checkout involves coordinating multiple services: cart, pricing, tax, inventory, payment, shipping, and notifications. Exposing all these services to the checkout UI creates tight coupling and complexity.

### The Solution

A checkout facade that coordinates all services behind simple operation names.

### The Code

```csharp
using PatternKit.Structural.Facade;

public record CheckoutRequest(
    string CartId,
    string CustomerId,
    PaymentInfo Payment,
    ShippingAddress Address);

public record CheckoutResult(
    string Status,
    string? OrderId = null,
    decimal? Total = null,
    string? Error = null);

public class CheckoutFacade
{
    private readonly ICartService _cart;
    private readonly IPricingService _pricing;
    private readonly ITaxService _tax;
    private readonly IInventoryService _inventory;
    private readonly IPaymentService _payment;
    private readonly IShippingService _shipping;
    private readonly INotificationService _notification;
    private readonly ILogger<CheckoutFacade> _logger;

    private readonly Facade<CheckoutRequest, CheckoutResult> _facade;

    public CheckoutFacade(
        ICartService cart,
        IPricingService pricing,
        ITaxService tax,
        IInventoryService inventory,
        IPaymentService payment,
        IShippingService shipping,
        INotificationService notification,
        ILogger<CheckoutFacade> logger)
    {
        _cart = cart;
        _pricing = pricing;
        _tax = tax;
        _inventory = inventory;
        _payment = payment;
        _shipping = shipping;
        _notification = notification;
        _logger = logger;

        _facade = Facade<CheckoutRequest, CheckoutResult>.Create()
            .Operation("checkout", ExecuteCheckout)
            .Operation("validate", ValidateCart)
            .Operation("preview", PreviewOrder)
            .Default((in CheckoutRequest _) =>
                new CheckoutResult("Error", Error: "Unknown operation"))
            .Build();
    }

    public CheckoutResult Execute(string operation, CheckoutRequest request)
    {
        _logger.LogInformation("Checkout operation: {Operation} for customer {CustomerId}",
            operation, request.CustomerId);
        return _facade.Execute(operation, request);
    }

    private CheckoutResult ExecuteCheckout(in CheckoutRequest req)
    {
        try
        {
            // 1. Get cart items
            var items = _cart.GetItems(req.CartId);
            if (!items.Any())
                return new CheckoutResult("Error", Error: "Cart is empty");

            // 2. Calculate pricing
            var pricing = _pricing.Calculate(items);

            // 3. Calculate tax
            var tax = _tax.Calculate(pricing.Subtotal, req.Address);

            // 4. Reserve inventory
            var reservation = _inventory.Reserve(items);
            if (!reservation.Success)
            {
                return new CheckoutResult("Error",
                    Error: $"Items unavailable: {string.Join(", ", reservation.UnavailableItems)}");
            }

            // 5. Process payment
            var total = pricing.Subtotal + pricing.Shipping + tax.Amount;
            var paymentResult = _payment.Process(req.Payment, total);
            if (!paymentResult.Success)
            {
                _inventory.Release(reservation.ReservationId);
                return new CheckoutResult("Error", Error: "Payment failed");
            }

            // 6. Create shipment
            var shipment = _shipping.Create(items, req.Address);

            // 7. Create order
            var orderId = Guid.NewGuid().ToString();

            // 8. Clear cart
            _cart.Clear(req.CartId);

            // 9. Send confirmation
            _notification.SendOrderConfirmation(req.CustomerId, orderId, total);

            _logger.LogInformation("Checkout completed: Order {OrderId}", orderId);

            return new CheckoutResult("Success", orderId, total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout failed for customer {CustomerId}", req.CustomerId);
            return new CheckoutResult("Error", Error: "Checkout failed. Please try again.");
        }
    }

    private CheckoutResult ValidateCart(in CheckoutRequest req)
    {
        var items = _cart.GetItems(req.CartId);
        if (!items.Any())
            return new CheckoutResult("Invalid", Error: "Cart is empty");

        var availability = _inventory.CheckAvailability(items);
        if (!availability.AllAvailable)
            return new CheckoutResult("Invalid",
                Error: $"Unavailable: {string.Join(", ", availability.UnavailableItems)}");

        return new CheckoutResult("Valid");
    }

    private CheckoutResult PreviewOrder(in CheckoutRequest req)
    {
        var items = _cart.GetItems(req.CartId);
        var pricing = _pricing.Calculate(items);
        var tax = _tax.Calculate(pricing.Subtotal, req.Address);
        var total = pricing.Subtotal + pricing.Shipping + tax.Amount;

        return new CheckoutResult("Preview", Total: total);
    }
}

// Usage
var facade = new CheckoutFacade(cart, pricing, tax, inventory, payment, shipping, notification, logger);

// Validate before checkout
var validation = facade.Execute("validate", request);
if (validation.Status != "Valid")
{
    Console.WriteLine($"Validation failed: {validation.Error}");
    return;
}

// Preview total
var preview = facade.Execute("preview", request);
Console.WriteLine($"Order total: {preview.Total:C}");

// Complete checkout
var result = facade.Execute("checkout", request);
if (result.Status == "Success")
{
    Console.WriteLine($"Order {result.OrderId} placed successfully!");
}
```

### Why This Pattern

The checkout facade hides the complexity of coordinating 7+ services. The UI only needs to know about three operations: validate, preview, and checkout.

---

## Example 2: User Management System (TypedFacade)

### The Problem

A user management system requires multiple operations with different parameter types. String-based operations are error-prone and lack IntelliSense.

### The Solution

A TypedFacade providing compile-time safety for user operations.

### The Code

```csharp
using PatternKit.Structural.Facade;

// Define the contract
public interface IUserManagement
{
    UserResult CreateUser(CreateUserRequest request);
    UserResult GetUser(int userId);
    UserResult UpdateUser(UpdateUserRequest request);
    bool DeleteUser(int userId);
    IEnumerable<UserResult> SearchUsers(string query);
}

public record CreateUserRequest(string Email, string Name, string Role);
public record UpdateUserRequest(int UserId, string? Name, string? Email, string? Role);
public record UserResult(int Id, string Email, string Name, string Role, bool Active);

public class UserManagementFacade
{
    private readonly IUserRepository _repository;
    private readonly IAuthService _auth;
    private readonly IAuditService _audit;
    private readonly INotificationService _notification;

    public IUserManagement Facade { get; }

    public UserManagementFacade(
        IUserRepository repository,
        IAuthService auth,
        IAuditService audit,
        INotificationService notification)
    {
        _repository = repository;
        _auth = auth;
        _audit = audit;
        _notification = notification;

        Facade = TypedFacade<IUserManagement>.Create()
            .Map(x => x.CreateUser, CreateUser)
            .Map(x => x.GetUser, GetUser)
            .Map(x => x.UpdateUser, UpdateUser)
            .Map(x => x.DeleteUser, DeleteUser)
            .Map(x => x.SearchUsers, SearchUsers)
            .Build();
    }

    private UserResult CreateUser(CreateUserRequest request)
    {
        // Validate email
        if (_repository.EmailExists(request.Email))
            throw new DuplicateEmailException(request.Email);

        // Create user
        var user = _repository.Create(new User
        {
            Email = request.Email,
            Name = request.Name,
            Role = request.Role,
            Active = true
        });

        // Create auth credentials
        var tempPassword = _auth.GenerateTemporaryPassword();
        _auth.CreateCredentials(user.Id, tempPassword);

        // Send welcome email
        _notification.SendWelcome(user.Email, tempPassword);

        // Audit
        _audit.LogUserCreated(user.Id, request);

        return MapToResult(user);
    }

    private UserResult GetUser(int userId)
    {
        var user = _repository.GetById(userId)
            ?? throw new UserNotFoundException(userId);
        return MapToResult(user);
    }

    private UserResult UpdateUser(UpdateUserRequest request)
    {
        var user = _repository.GetById(request.UserId)
            ?? throw new UserNotFoundException(request.UserId);

        if (request.Name != null) user.Name = request.Name;
        if (request.Email != null)
        {
            if (_repository.EmailExists(request.Email) && user.Email != request.Email)
                throw new DuplicateEmailException(request.Email);
            user.Email = request.Email;
        }
        if (request.Role != null) user.Role = request.Role;

        _repository.Update(user);
        _audit.LogUserUpdated(user.Id, request);

        return MapToResult(user);
    }

    private bool DeleteUser(int userId)
    {
        var user = _repository.GetById(userId);
        if (user == null) return false;

        // Soft delete
        user.Active = false;
        _repository.Update(user);

        // Revoke auth
        _auth.RevokeCredentials(userId);

        // Audit
        _audit.LogUserDeleted(userId);

        return true;
    }

    private IEnumerable<UserResult> SearchUsers(string query)
    {
        return _repository.Search(query).Select(MapToResult);
    }

    private static UserResult MapToResult(User user) =>
        new(user.Id, user.Email, user.Name, user.Role, user.Active);
}

// Usage - fully type-safe
var facade = new UserManagementFacade(repo, auth, audit, notification);

// Create user - IntelliSense shows parameter types
var newUser = facade.Facade.CreateUser(new CreateUserRequest(
    Email: "alice@example.com",
    Name: "Alice Smith",
    Role: "Admin"
));

// Get user - compile-time parameter checking
var user = facade.Facade.GetUser(newUser.Id);

// Update user
var updated = facade.Facade.UpdateUser(new UpdateUserRequest(
    UserId: user.Id,
    Name: "Alice Johnson",
    Email: null,
    Role: null
));

// Search users
var admins = facade.Facade.SearchUsers("Admin");
```

### Why This Pattern

TypedFacade provides compile-time safety - typos are caught at build time, not runtime. IntelliSense shows available operations and their parameters.

---

## Example 3: Media Transcoding Service

### The Problem

A media transcoding service needs to coordinate multiple subsystems: storage, transcoding engines, CDN, and analytics. Different operations have different complexity levels.

### The Solution

A facade that exposes simple operations while handling complex orchestration internally.

### The Code

```csharp
using PatternKit.Structural.Facade;

public record TranscodeRequest(
    string SourceUrl,
    string[] OutputFormats,
    TranscodeOptions Options);

public record TranscodeOptions(
    int? MaxWidth,
    int? MaxHeight,
    string? WatermarkUrl,
    bool GenerateThumbnails);

public record TranscodeResult(
    string Status,
    string JobId,
    Dictionary<string, string>? OutputUrls = null,
    string[]? ThumbnailUrls = null,
    string? Error = null);

public class MediaTranscodingFacade
{
    private readonly IStorageService _storage;
    private readonly ITranscodeEngine _transcoder;
    private readonly ICdnService _cdn;
    private readonly IAnalyticsService _analytics;
    private readonly IQueueService _queue;

    private readonly Facade<TranscodeRequest, TranscodeResult> _facade;

    public MediaTranscodingFacade(
        IStorageService storage,
        ITranscodeEngine transcoder,
        ICdnService cdn,
        IAnalyticsService analytics,
        IQueueService queue)
    {
        _storage = storage;
        _transcoder = transcoder;
        _cdn = cdn;
        _analytics = analytics;
        _queue = queue;

        _facade = Facade<TranscodeRequest, TranscodeResult>.Create()
            .OperationIgnoreCase("transcode", StartTranscode)
            .OperationIgnoreCase("quick", QuickTranscode)
            .OperationIgnoreCase("status", CheckStatus)
            .OperationIgnoreCase("cancel", CancelJob)
            .Build();
    }

    public TranscodeResult Execute(string operation, TranscodeRequest request)
        => _facade.Execute(operation, request);

    private TranscodeResult StartTranscode(in TranscodeRequest req)
    {
        // 1. Download source to temp storage
        var tempPath = _storage.DownloadToTemp(req.SourceUrl);

        // 2. Validate source file
        var mediaInfo = _transcoder.Analyze(tempPath);
        if (!mediaInfo.IsValidMedia)
            return new TranscodeResult("Error", "", Error: "Invalid media file");

        // 3. Create job
        var jobId = Guid.NewGuid().ToString();

        // 4. Queue transcoding tasks for each format
        foreach (var format in req.OutputFormats)
        {
            _queue.Enqueue(new TranscodeTask
            {
                JobId = jobId,
                SourcePath = tempPath,
                OutputFormat = format,
                Options = req.Options
            });
        }

        // 5. Queue thumbnail generation if requested
        if (req.Options.GenerateThumbnails)
        {
            _queue.Enqueue(new ThumbnailTask
            {
                JobId = jobId,
                SourcePath = tempPath,
                Count = 5
            });
        }

        // 6. Track analytics
        _analytics.TrackJobStarted(jobId, req.OutputFormats.Length);

        return new TranscodeResult("Queued", jobId);
    }

    private TranscodeResult QuickTranscode(in TranscodeRequest req)
    {
        // Synchronous transcoding for small files
        var tempPath = _storage.DownloadToTemp(req.SourceUrl);
        var mediaInfo = _transcoder.Analyze(tempPath);

        if (mediaInfo.Duration > TimeSpan.FromMinutes(5))
            return StartTranscode(in req); // Too long, queue instead

        var jobId = Guid.NewGuid().ToString();
        var outputs = new Dictionary<string, string>();

        foreach (var format in req.OutputFormats)
        {
            var outputPath = _transcoder.TranscodeSync(tempPath, format, req.Options);
            var cdnUrl = _cdn.Upload(outputPath);
            outputs[format] = cdnUrl;
        }

        string[]? thumbnails = null;
        if (req.Options.GenerateThumbnails)
        {
            var thumbPaths = _transcoder.GenerateThumbnails(tempPath, 5);
            thumbnails = thumbPaths.Select(p => _cdn.Upload(p)).ToArray();
        }

        _storage.DeleteTemp(tempPath);
        _analytics.TrackJobCompleted(jobId, outputs.Count);

        return new TranscodeResult("Completed", jobId, outputs, thumbnails);
    }

    private TranscodeResult CheckStatus(in TranscodeRequest req)
    {
        // Use SourceUrl as job ID for status checks
        var jobId = req.SourceUrl;
        var status = _queue.GetJobStatus(jobId);

        if (status.IsComplete)
        {
            var outputs = _cdn.GetOutputUrls(jobId);
            var thumbnails = _cdn.GetThumbnailUrls(jobId);
            return new TranscodeResult("Completed", jobId, outputs, thumbnails);
        }

        return new TranscodeResult(status.State, jobId);
    }

    private TranscodeResult CancelJob(in TranscodeRequest req)
    {
        var jobId = req.SourceUrl;
        _queue.CancelJob(jobId);
        _analytics.TrackJobCancelled(jobId);
        return new TranscodeResult("Cancelled", jobId);
    }
}

// Usage
var facade = new MediaTranscodingFacade(storage, transcoder, cdn, analytics, queue);

// Start async transcoding
var job = facade.Execute("transcode", new TranscodeRequest(
    SourceUrl: "https://storage.example.com/video.mp4",
    OutputFormats: new[] { "720p", "1080p", "4k" },
    Options: new TranscodeOptions(
        MaxWidth: 3840,
        MaxHeight: 2160,
        WatermarkUrl: "https://example.com/watermark.png",
        GenerateThumbnails: true
    )
));

Console.WriteLine($"Job {job.JobId} queued");

// Check status later
var status = facade.Execute("status", new TranscodeRequest(
    SourceUrl: job.JobId, // Job ID passed via SourceUrl
    OutputFormats: Array.Empty<string>(),
    Options: new TranscodeOptions(null, null, null, false)
));

if (status.Status == "Completed")
{
    foreach (var (format, url) in status.OutputUrls!)
    {
        Console.WriteLine($"{format}: {url}");
    }
}
```

### Why This Pattern

The facade hides complex coordination between storage, transcoding, CDN, and queue services. Clients only need to know "transcode" to trigger the entire workflow.

---

## Example 4: Financial Reporting System

### The Problem

A financial reporting system needs to aggregate data from multiple sources, apply complex calculations, and generate reports in various formats. Direct service access creates maintenance nightmares.

### The Solution

A facade that exposes report generation as simple operations while handling data aggregation internally.

### The Code

```csharp
using PatternKit.Structural.Facade;

public record ReportRequest(
    string ReportType,
    DateRange Period,
    string[] Departments,
    string OutputFormat);

public record DateRange(DateTime Start, DateTime End);

public record ReportResult(
    string Status,
    byte[]? Content = null,
    string? ContentType = null,
    string? Filename = null,
    ReportMetadata? Metadata = null,
    string? Error = null);

public record ReportMetadata(
    int TotalRecords,
    decimal TotalAmount,
    Dictionary<string, decimal> ByDepartment);

public class FinancialReportingFacade
{
    private readonly ITransactionService _transactions;
    private readonly IBudgetService _budget;
    private readonly IForecastService _forecast;
    private readonly IExportService _export;
    private readonly ICacheService _cache;

    private readonly Facade<ReportRequest, ReportResult> _facade;

    public FinancialReportingFacade(
        ITransactionService transactions,
        IBudgetService budget,
        IForecastService forecast,
        IExportService export,
        ICacheService cache)
    {
        _transactions = transactions;
        _budget = budget;
        _forecast = forecast;
        _export = export;
        _cache = cache;

        _facade = Facade<ReportRequest, ReportResult>.Create()
            .Operation("expense", GenerateExpenseReport)
            .Operation("revenue", GenerateRevenueReport)
            .Operation("budget-variance", GenerateBudgetVarianceReport)
            .Operation("forecast", GenerateForecastReport)
            .Operation("executive-summary", GenerateExecutiveSummary)
            .Default((in ReportRequest req) =>
                new ReportResult("Error", Error: $"Unknown report type: {req.ReportType}"))
            .Build();
    }

    public ReportResult Generate(ReportRequest request)
    {
        // Check cache first
        var cacheKey = GenerateCacheKey(request);
        if (_cache.TryGet<ReportResult>(cacheKey, out var cached))
            return cached;

        var result = _facade.Execute(request.ReportType, request);

        if (result.Status == "Success")
            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));

        return result;
    }

    private ReportResult GenerateExpenseReport(in ReportRequest req)
    {
        // Aggregate expenses from multiple sources
        var expenses = _transactions.GetExpenses(req.Period, req.Departments);
        var byCategory = expenses.GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
        var byDepartment = expenses.GroupBy(e => e.Department)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var metadata = new ReportMetadata(
            TotalRecords: expenses.Count,
            TotalAmount: expenses.Sum(e => e.Amount),
            ByDepartment: byDepartment
        );

        // Generate report in requested format
        var (content, contentType) = req.OutputFormat switch
        {
            "pdf" => (_export.ToPdf(expenses, "Expense Report"), "application/pdf"),
            "excel" => (_export.ToExcel(expenses), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            "csv" => (_export.ToCsv(expenses), "text/csv"),
            _ => throw new ArgumentException($"Unsupported format: {req.OutputFormat}")
        };

        return new ReportResult(
            Status: "Success",
            Content: content,
            ContentType: contentType,
            Filename: $"expense-report-{req.Period.Start:yyyy-MM}.{req.OutputFormat}",
            Metadata: metadata
        );
    }

    private ReportResult GenerateRevenueReport(in ReportRequest req)
    {
        var revenue = _transactions.GetRevenue(req.Period, req.Departments);
        var byProduct = revenue.GroupBy(r => r.Product)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
        var byDepartment = revenue.GroupBy(r => r.Department)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

        var metadata = new ReportMetadata(
            TotalRecords: revenue.Count,
            TotalAmount: revenue.Sum(r => r.Amount),
            ByDepartment: byDepartment
        );

        var (content, contentType) = ExportReport(revenue, "Revenue Report", req.OutputFormat);

        return new ReportResult(
            Status: "Success",
            Content: content,
            ContentType: contentType,
            Filename: $"revenue-report-{req.Period.Start:yyyy-MM}.{req.OutputFormat}",
            Metadata: metadata
        );
    }

    private ReportResult GenerateBudgetVarianceReport(in ReportRequest req)
    {
        // Get actual spend and budget
        var actuals = _transactions.GetExpenses(req.Period, req.Departments);
        var budgets = _budget.GetBudgets(req.Period, req.Departments);

        // Calculate variances
        var variances = budgets.Select(b => new BudgetVariance
        {
            Department = b.Department,
            Category = b.Category,
            Budgeted = b.Amount,
            Actual = actuals.Where(a => a.Department == b.Department && a.Category == b.Category)
                           .Sum(a => a.Amount),
            Variance = b.Amount - actuals.Where(a => a.Department == b.Department && a.Category == b.Category)
                                        .Sum(a => a.Amount)
        }).ToList();

        var overBudget = variances.Where(v => v.Variance < 0);

        var (content, contentType) = ExportReport(variances, "Budget Variance Report", req.OutputFormat);

        return new ReportResult(
            Status: "Success",
            Content: content,
            ContentType: contentType,
            Filename: $"budget-variance-{req.Period.Start:yyyy-MM}.{req.OutputFormat}",
            Metadata: new ReportMetadata(
                TotalRecords: variances.Count,
                TotalAmount: variances.Sum(v => v.Variance),
                ByDepartment: variances.GroupBy(v => v.Department)
                    .ToDictionary(g => g.Key, g => g.Sum(v => v.Variance))
            )
        );
    }

    private ReportResult GenerateForecastReport(in ReportRequest req)
    {
        var historical = _transactions.GetAll(req.Period, req.Departments);
        var forecast = _forecast.Generate(historical, months: 6);

        var (content, contentType) = ExportReport(forecast, "Financial Forecast", req.OutputFormat);

        return new ReportResult(
            Status: "Success",
            Content: content,
            ContentType: contentType,
            Filename: $"forecast-{DateTime.Now:yyyy-MM}.{req.OutputFormat}",
            Metadata: new ReportMetadata(
                TotalRecords: forecast.Count,
                TotalAmount: forecast.Sum(f => f.PredictedAmount),
                ByDepartment: forecast.GroupBy(f => f.Department)
                    .ToDictionary(g => g.Key, g => g.Sum(f => f.PredictedAmount))
            )
        );
    }

    private ReportResult GenerateExecutiveSummary(in ReportRequest req)
    {
        // Aggregate all report types into executive summary
        var expenses = _transactions.GetExpenses(req.Period, req.Departments);
        var revenue = _transactions.GetRevenue(req.Period, req.Departments);
        var budgets = _budget.GetBudgets(req.Period, req.Departments);
        var forecast = _forecast.Generate(
            _transactions.GetAll(req.Period, req.Departments),
            months: 3);

        var summary = new ExecutiveSummary
        {
            Period = req.Period,
            TotalRevenue = revenue.Sum(r => r.Amount),
            TotalExpenses = expenses.Sum(e => e.Amount),
            NetIncome = revenue.Sum(r => r.Amount) - expenses.Sum(e => e.Amount),
            BudgetVariance = budgets.Sum(b => b.Amount) - expenses.Sum(e => e.Amount),
            ForecastedRevenue = forecast.Where(f => f.Type == "Revenue").Sum(f => f.PredictedAmount),
            TopExpenseCategories = expenses.GroupBy(e => e.Category)
                .OrderByDescending(g => g.Sum(e => e.Amount))
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount))
        };

        var (content, contentType) = ExportReport(new[] { summary }, "Executive Summary", req.OutputFormat);

        return new ReportResult(
            Status: "Success",
            Content: content,
            ContentType: contentType,
            Filename: $"executive-summary-{req.Period.Start:yyyy-MM}.{req.OutputFormat}",
            Metadata: new ReportMetadata(
                TotalRecords: 1,
                TotalAmount: summary.NetIncome,
                ByDepartment: new Dictionary<string, decimal>
                {
                    ["Net Income"] = summary.NetIncome
                }
            )
        );
    }

    private (byte[] content, string contentType) ExportReport<T>(
        IEnumerable<T> data, string title, string format)
    {
        return format switch
        {
            "pdf" => (_export.ToPdf(data, title), "application/pdf"),
            "excel" => (_export.ToExcel(data), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            "csv" => (_export.ToCsv(data), "text/csv"),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    private string GenerateCacheKey(ReportRequest req) =>
        $"report:{req.ReportType}:{req.Period.Start:yyyyMMdd}:{req.Period.End:yyyyMMdd}:" +
        $"{string.Join(",", req.Departments)}:{req.OutputFormat}";
}

// Usage
var facade = new FinancialReportingFacade(transactions, budget, forecast, export, cache);

// Generate expense report
var expenseReport = facade.Generate(new ReportRequest(
    ReportType: "expense",
    Period: new DateRange(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)),
    Departments: new[] { "Engineering", "Marketing", "Sales" },
    OutputFormat: "pdf"
));

if (expenseReport.Status == "Success")
{
    File.WriteAllBytes(expenseReport.Filename!, expenseReport.Content!);
    Console.WriteLine($"Generated {expenseReport.Filename}");
    Console.WriteLine($"Total: {expenseReport.Metadata!.TotalAmount:C}");
}

// Generate executive summary
var summary = facade.Generate(new ReportRequest(
    ReportType: "executive-summary",
    Period: new DateRange(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)),
    Departments: new[] { "All" },
    OutputFormat: "pdf"
));
```

### Why This Pattern

The facade hides complex aggregation logic spanning multiple services. Report generation involves transactions, budgets, forecasts, and export formatting - all coordinated seamlessly behind simple operation names.

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
