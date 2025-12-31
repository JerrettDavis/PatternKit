# Decorator Pattern Real-World Examples

Production-ready examples demonstrating the Decorator pattern in real-world scenarios.

---

## Example 1: HTTP Client with Resilience

### The Problem

An HTTP client needs logging, metrics, caching, retry with exponential backoff, and circuit breaker patterns without cluttering the core HTTP logic.

### The Solution

Use Decorator to layer resilience patterns around the HTTP operation.

### The Code

```csharp
public class ResilientHttpClient
{
    private readonly Decorator<HttpRequest, HttpResponse> _client;
    private readonly HttpClient _httpClient;

    public ResilientHttpClient(
        HttpClient httpClient,
        ICache cache,
        ILogger logger,
        IMetrics metrics)
    {
        _httpClient = httpClient;

        _client = Decorator<HttpRequest, HttpResponse>
            .Create(ExecuteRequest)
            // Validate request
            .Before(request =>
            {
                if (string.IsNullOrEmpty(request.Url))
                    throw new ArgumentException("URL is required");
                if (request.Timeout <= TimeSpan.Zero)
                    request = request with { Timeout = TimeSpan.FromSeconds(30) };
                return request;
            })
            // Add request ID and logging
            .Around((request, next) =>
            {
                var requestId = Guid.NewGuid().ToString("N")[..8];
                logger.LogInformation("[{RequestId}] {Method} {Url}",
                    requestId, request.Method, request.Url);

                try
                {
                    var response = next(request with { RequestId = requestId });
                    logger.LogInformation("[{RequestId}] {StatusCode} in {Duration}ms",
                        requestId, response.StatusCode, response.Duration.TotalMilliseconds);
                    return response;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[{RequestId}] Request failed", requestId);
                    throw;
                }
            })
            // Caching for GET requests
            .Around((request, next) =>
            {
                if (request.Method != "GET" || !request.CacheEnabled)
                    return next(request);

                var cacheKey = $"http:{request.Url}:{request.Headers.GetHashCode()}";
                if (cache.TryGet<HttpResponse>(cacheKey, out var cached))
                {
                    logger.LogDebug("Cache hit for {Url}", request.Url);
                    return cached with { FromCache = true };
                }

                var response = next(request);
                if (response.IsSuccess)
                {
                    cache.Set(cacheKey, response, request.CacheDuration);
                }
                return response;
            })
            // Retry with exponential backoff
            .Around((request, next) =>
            {
                var maxRetries = request.MaxRetries > 0 ? request.MaxRetries : 3;
                Exception? lastException = null;

                for (int attempt = 0; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        var response = next(request);
                        if (response.IsSuccess || !response.IsRetryable)
                            return response;

                        lastException = new HttpRequestException(
                            $"HTTP {response.StatusCode}: {response.ReasonPhrase}");
                    }
                    catch (Exception ex) when (IsRetryable(ex) && attempt < maxRetries)
                    {
                        lastException = ex;
                    }

                    if (attempt < maxRetries)
                    {
                        var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
                        logger.LogWarning("Retry {Attempt}/{MaxRetries} after {Delay}ms",
                            attempt + 1, maxRetries, delay.TotalMilliseconds);
                        Thread.Sleep(delay);
                    }
                }

                throw new HttpRequestException($"Failed after {maxRetries + 1} attempts", lastException);
            })
            // Circuit breaker
            .Around(CreateCircuitBreaker(5, TimeSpan.FromSeconds(30), logger))
            // Metrics
            .Around((request, next) =>
            {
                var sw = Stopwatch.StartNew();
                var tags = new[] { $"method:{request.Method}", $"host:{new Uri(request.Url).Host}" };

                try
                {
                    var response = next(request);
                    sw.Stop();

                    metrics.Histogram("http.request.duration", sw.ElapsedMilliseconds, tags);
                    metrics.Increment($"http.request.status.{response.StatusCode}", tags);

                    return response;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    metrics.Histogram("http.request.duration", sw.ElapsedMilliseconds, tags);
                    metrics.Increment($"http.request.error.{ex.GetType().Name}", tags);
                    throw;
                }
            })
            .Build();
    }

    private HttpResponse ExecuteRequest(HttpRequest request)
    {
        var sw = Stopwatch.StartNew();
        var httpRequest = new HttpRequestMessage(
            new HttpMethod(request.Method),
            request.Url);

        foreach (var header in request.Headers)
            httpRequest.Headers.Add(header.Key, header.Value);

        if (request.Body != null)
            httpRequest.Content = new StringContent(request.Body, Encoding.UTF8, request.ContentType);

        using var cts = new CancellationTokenSource(request.Timeout);
        var response = _httpClient.Send(httpRequest, cts.Token);

        return new HttpResponse
        {
            StatusCode = (int)response.StatusCode,
            ReasonPhrase = response.ReasonPhrase,
            Body = response.Content.ReadAsStringAsync().Result,
            Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
            Duration = sw.Elapsed
        };
    }

    public HttpResponse Send(HttpRequest request) => _client.Execute(request);
}
```

### Why This Pattern

- **Separation of concerns**: Each layer handles one aspect
- **Configurable**: Enable/disable features per request
- **Observable**: Metrics and logging built in
- **Resilient**: Retry and circuit breaker protect downstream

---

## Example 2: Database Query Pipeline

### The Problem

A database access layer needs query validation, parameterization, connection pooling, caching, slow query logging, and transaction support.

### The Solution

Use Decorator to build a query pipeline with layered concerns.

### The Code

```csharp
public class DatabaseService
{
    private readonly Decorator<DbQuery, DbResult> _queryExecutor;

    public DatabaseService(
        IConnectionPool connectionPool,
        IQueryCache cache,
        ILogger logger,
        IMetrics metrics)
    {
        _queryExecutor = Decorator<DbQuery, DbResult>
            .Create(query => ExecuteQueryCore(query, connectionPool))
            // Validate and sanitize
            .Before(query =>
            {
                if (string.IsNullOrWhiteSpace(query.Sql))
                    throw new ArgumentException("SQL is required");

                // Prevent SQL injection
                if (ContainsSqlInjection(query.Sql))
                    throw new SecurityException("Potential SQL injection detected");

                return query;
            })
            // Add query caching
            .Around((query, next) =>
            {
                if (!query.CacheEnabled || query.IsWriteOperation)
                    return next(query);

                var cacheKey = ComputeCacheKey(query);
                if (cache.TryGet(cacheKey, out var cached))
                {
                    return new DbResult
                    {
                        Rows = cached,
                        FromCache = true,
                        RowCount = cached.Count
                    };
                }

                var result = next(query);
                if (result.Success && result.Rows != null)
                {
                    cache.Set(cacheKey, result.Rows, query.CacheDuration);
                }
                return result;
            })
            // Transaction management
            .Around((query, next) =>
            {
                if (!query.RequiresTransaction)
                    return next(query);

                using var transaction = connectionPool.BeginTransaction(query.IsolationLevel);
                try
                {
                    var result = next(query with { Transaction = transaction });
                    transaction.Commit();
                    return result;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            })
            // Slow query logging
            .Around((query, next) =>
            {
                var sw = Stopwatch.StartNew();
                var result = next(query);
                sw.Stop();

                if (sw.Elapsed > query.SlowQueryThreshold)
                {
                    logger.LogWarning("Slow query ({Duration}ms): {Sql}",
                        sw.ElapsedMilliseconds, query.Sql);
                }

                result.Duration = sw.Elapsed;
                return result;
            })
            // Metrics
            .Around((query, next) =>
            {
                var tags = new[] { $"operation:{query.OperationType}" };

                try
                {
                    var result = next(query);
                    metrics.Increment("db.query.success", tags);
                    metrics.Histogram("db.query.rows", result.RowCount, tags);
                    return result;
                }
                catch (Exception ex)
                {
                    metrics.Increment($"db.query.error.{ex.GetType().Name}", tags);
                    throw;
                }
            })
            // Connection error handling
            .Around((query, next) =>
            {
                try
                {
                    return next(query);
                }
                catch (DbException ex) when (IsConnectionError(ex))
                {
                    logger.LogError(ex, "Database connection error, refreshing pool");
                    connectionPool.Refresh();
                    return next(query); // Retry once
                }
            })
            .Build();
    }

    public DbResult Query(string sql, object? parameters = null, QueryOptions? options = null)
    {
        return _queryExecutor.Execute(new DbQuery
        {
            Sql = sql,
            Parameters = parameters,
            CacheEnabled = options?.CacheEnabled ?? false,
            CacheDuration = options?.CacheDuration ?? TimeSpan.FromMinutes(5),
            RequiresTransaction = options?.RequiresTransaction ?? false,
            IsolationLevel = options?.IsolationLevel ?? IsolationLevel.ReadCommitted,
            SlowQueryThreshold = options?.SlowQueryThreshold ?? TimeSpan.FromSeconds(1)
        });
    }

    public T QuerySingle<T>(string sql, object? parameters = null) where T : class
    {
        var result = Query(sql, parameters);
        return result.Rows?.FirstOrDefault() as T
            ?? throw new InvalidOperationException("No results returned");
    }
}
```

### Why This Pattern

- **Query pipeline**: Each decorator handles one concern
- **Flexible caching**: Cache control per query
- **Transaction support**: Automatic commit/rollback
- **Observable**: Slow query logging, metrics

---

## Example 3: File Upload Processor

### The Problem

A file upload service needs validation, virus scanning, image resizing, watermarking, and cloud storage upload with progress tracking.

### The Solution

Use Decorator to build a file processing pipeline.

### The Code

```csharp
public class FileUploadService
{
    private readonly Decorator<UploadRequest, UploadResult> _uploader;

    public FileUploadService(
        IVirusScanner virusScanner,
        IImageProcessor imageProcessor,
        ICloudStorage cloudStorage,
        ILogger logger)
    {
        _uploader = Decorator<UploadRequest, UploadResult>
            .Create(request => cloudStorage.Upload(request.ProcessedFile, request.DestinationPath))
            // Validate file
            .Before(request =>
            {
                if (request.File == null || request.File.Length == 0)
                    throw new ValidationException("File is required");

                if (request.File.Length > request.MaxFileSize)
                    throw new ValidationException($"File exceeds maximum size of {request.MaxFileSize / 1_000_000}MB");

                var extension = Path.GetExtension(request.FileName)?.ToLowerInvariant();
                if (!request.AllowedExtensions.Contains(extension))
                    throw new ValidationException($"File type {extension} not allowed");

                return request;
            })
            // Virus scan
            .Around((request, next) =>
            {
                logger.LogInformation("Scanning file for viruses: {FileName}", request.FileName);
                var scanResult = virusScanner.Scan(request.File);

                if (scanResult.IsInfected)
                {
                    logger.LogWarning("Virus detected in {FileName}: {Threat}",
                        request.FileName, scanResult.ThreatName);
                    throw new SecurityException($"Virus detected: {scanResult.ThreatName}");
                }

                return next(request);
            })
            // Process images
            .Around((request, next) =>
            {
                if (!IsImage(request.FileName))
                    return next(request);

                logger.LogInformation("Processing image: {FileName}", request.FileName);
                var processedFile = request.File;

                // Resize if needed
                if (request.MaxImageDimension.HasValue)
                {
                    processedFile = imageProcessor.Resize(
                        processedFile,
                        request.MaxImageDimension.Value,
                        request.MaxImageDimension.Value);
                }

                // Add watermark if configured
                if (!string.IsNullOrEmpty(request.WatermarkText))
                {
                    processedFile = imageProcessor.AddWatermark(
                        processedFile,
                        request.WatermarkText,
                        request.WatermarkPosition);
                }

                // Convert format if needed
                if (!string.IsNullOrEmpty(request.ConvertToFormat))
                {
                    processedFile = imageProcessor.Convert(
                        processedFile,
                        request.ConvertToFormat);
                }

                return next(request with { ProcessedFile = processedFile });
            })
            // Generate unique path
            .Before(request =>
            {
                var fileName = request.FileName;
                var extension = Path.GetExtension(fileName);
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var datePath = DateTime.UtcNow.ToString("yyyy/MM/dd");
                var uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{uniqueId}{extension}";

                return request with
                {
                    DestinationPath = $"{request.BasePath}/{datePath}/{uniqueName}",
                    ProcessedFile = request.ProcessedFile ?? request.File
                };
            })
            // Track progress and log
            .Around((request, next) =>
            {
                logger.LogInformation("Uploading {FileName} ({Size} bytes) to {Path}",
                    request.FileName, request.ProcessedFile.Length, request.DestinationPath);

                var sw = Stopwatch.StartNew();
                var result = next(request);
                sw.Stop();

                logger.LogInformation("Upload complete: {Url} in {Duration}ms",
                    result.Url, sw.ElapsedMilliseconds);

                return result with { Duration = sw.Elapsed };
            })
            // Generate thumbnails for images
            .After((request, result) =>
            {
                if (!IsImage(request.FileName) || !request.GenerateThumbnails)
                    return result;

                var thumbnails = new List<string>();
                foreach (var size in request.ThumbnailSizes)
                {
                    var thumbnail = imageProcessor.Resize(request.ProcessedFile, size, size);
                    var thumbPath = request.DestinationPath.Replace(
                        Path.GetExtension(request.FileName),
                        $"_{size}x{size}{Path.GetExtension(request.FileName)}");
                    var thumbResult = cloudStorage.Upload(thumbnail, thumbPath);
                    thumbnails.Add(thumbResult.Url);
                }

                return result with { ThumbnailUrls = thumbnails.ToArray() };
            })
            .Build();
    }

    public UploadResult Upload(UploadRequest request) => _uploader.Execute(request);
}
```

### Why This Pattern

- **Processing pipeline**: Clear sequence of operations
- **Conditional processing**: Image processing only for images
- **Security**: Virus scan before storage
- **Metadata**: Thumbnails generated after upload

---

## Example 4: Order Processing Pipeline

### The Problem

An e-commerce order processor needs validation, inventory check, payment processing, fulfillment, notifications, and audit logging with the ability to handle partial failures.

### The Solution

Use Decorator to build an order processing pipeline with compensation logic.

### The Code

```csharp
public class OrderProcessor
{
    private readonly Decorator<ProcessOrderRequest, ProcessOrderResult> _processor;

    public OrderProcessor(
        IInventoryService inventory,
        IPaymentService payments,
        IFulfillmentService fulfillment,
        INotificationService notifications,
        IAuditLog audit,
        ILogger logger)
    {
        _processor = Decorator<ProcessOrderRequest, ProcessOrderResult>
            .Create(request => new ProcessOrderResult { OrderId = request.Order.Id, Success = true })
            // Validate order
            .Before(request =>
            {
                var errors = ValidateOrder(request.Order);
                if (errors.Any())
                    throw new OrderValidationException(errors);
                return request;
            })
            // Audit logging
            .Around((request, next) =>
            {
                audit.LogOrderStarted(request.Order.Id, request.Order.CustomerId);

                try
                {
                    var result = next(request);
                    audit.LogOrderCompleted(request.Order.Id, result.Success);
                    return result;
                }
                catch (Exception ex)
                {
                    audit.LogOrderFailed(request.Order.Id, ex);
                    throw;
                }
            })
            // Reserve inventory
            .Around((request, next) =>
            {
                logger.LogInformation("Reserving inventory for order {OrderId}", request.Order.Id);

                var reservation = inventory.Reserve(request.Order.Items);
                if (!reservation.Success)
                {
                    return new ProcessOrderResult
                    {
                        OrderId = request.Order.Id,
                        Success = false,
                        Error = "Insufficient inventory",
                        UnavailableItems = reservation.UnavailableItems
                    };
                }

                try
                {
                    var result = next(request with { InventoryReservation = reservation });

                    if (!result.Success)
                    {
                        logger.LogInformation("Releasing inventory for failed order {OrderId}",
                            request.Order.Id);
                        inventory.Release(reservation.ReservationId);
                    }

                    return result;
                }
                catch
                {
                    inventory.Release(reservation.ReservationId);
                    throw;
                }
            })
            // Process payment
            .Around((request, next) =>
            {
                logger.LogInformation("Processing payment for order {OrderId}", request.Order.Id);

                var payment = payments.Process(new PaymentRequest
                {
                    OrderId = request.Order.Id,
                    Amount = request.Order.Total,
                    Currency = request.Order.Currency,
                    PaymentMethod = request.Order.PaymentMethod
                });

                if (!payment.Success)
                {
                    return new ProcessOrderResult
                    {
                        OrderId = request.Order.Id,
                        Success = false,
                        Error = $"Payment failed: {payment.Error}"
                    };
                }

                try
                {
                    var result = next(request with { PaymentTransaction = payment });

                    if (!result.Success)
                    {
                        logger.LogInformation("Refunding payment for failed order {OrderId}",
                            request.Order.Id);
                        payments.Refund(payment.TransactionId);
                    }

                    return result with { TransactionId = payment.TransactionId };
                }
                catch
                {
                    payments.Refund(payment.TransactionId);
                    throw;
                }
            })
            // Create fulfillment
            .Around((request, next) =>
            {
                logger.LogInformation("Creating fulfillment for order {OrderId}", request.Order.Id);

                var fulfillmentOrder = fulfillment.Create(new FulfillmentRequest
                {
                    OrderId = request.Order.Id,
                    Items = request.Order.Items,
                    ShippingAddress = request.Order.ShippingAddress,
                    ShippingMethod = request.Order.ShippingMethod
                });

                var result = next(request with { FulfillmentOrder = fulfillmentOrder });
                return result with { FulfillmentId = fulfillmentOrder.Id };
            })
            // Confirm inventory
            .After((request, result) =>
            {
                if (result.Success && request.InventoryReservation != null)
                {
                    inventory.Confirm(request.InventoryReservation.ReservationId);
                }
                return result;
            })
            // Send notifications
            .After((request, result) =>
            {
                if (result.Success)
                {
                    notifications.SendOrderConfirmation(
                        request.Order.CustomerId,
                        request.Order.Id);
                }
                else
                {
                    notifications.SendOrderFailure(
                        request.Order.CustomerId,
                        request.Order.Id,
                        result.Error);
                }
                return result;
            })
            .Build();
    }

    public ProcessOrderResult Process(Order order) =>
        _processor.Execute(new ProcessOrderRequest { Order = order });
}
```

### Why This Pattern

- **Saga pattern**: Compensation on failure
- **Clear flow**: Each step visible
- **Partial failure**: Handle gracefully
- **Audit trail**: Complete logging

---

## Key Takeaways

1. **Layer responsibilities**: Each decorator handles one concern
2. **Order matters**: Caching before retry, validation before processing
3. **Compensation**: Around decorators enable rollback on failure
4. **Observable**: Add logging and metrics as outer layers
5. **Reusable**: Build decorator factories for common patterns

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
