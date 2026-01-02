# Bridge Pattern Real-World Examples

Production-ready examples demonstrating the Bridge pattern in real-world scenarios.

---

## Example 1: Multi-Channel Notification System

### The Problem

A notification system must send messages through different channels (email, SMS, push, Slack) based on user preferences and message priority, with logging, metrics, and rate limiting.

### The Solution

Use Bridge to decouple notification logic from channel-specific implementations.

### The Code

```csharp
public class NotificationBridge
{
    private readonly Bridge<NotificationRequest, NotificationResult, INotificationChannel> _bridge;

    public NotificationBridge(
        IChannelFactory channelFactory,
        IRateLimiter rateLimiter,
        IMetrics metrics)
    {
        _bridge = Bridge<NotificationRequest, NotificationResult, INotificationChannel>
            .Create((in NotificationRequest r) => channelFactory.GetChannel(r.ChannelType))
            // Pre-validation
            .Require(static (in NotificationRequest r, INotificationChannel _) =>
                string.IsNullOrEmpty(r.Recipient) ? "Recipient required" : null)
            .Require(static (in NotificationRequest r, INotificationChannel _) =>
                string.IsNullOrEmpty(r.Message) ? "Message required" : null)
            .Require((in NotificationRequest r, INotificationChannel c) =>
                !c.IsRecipientValid(r.Recipient) ? $"Invalid recipient for {c.Name}" : null)
            .Require((in NotificationRequest r, INotificationChannel c) =>
                rateLimiter.IsRateLimited(r.UserId, c.Name)
                    ? "Rate limit exceeded, try again later"
                    : null)
            // Before hooks
            .Before((in NotificationRequest r, INotificationChannel c) =>
            {
                metrics.StartNotification(c.Name);
                Log.Info($"Sending {r.Priority} notification via {c.Name} to {r.Recipient}");
            })
            // Core operation
            .Operation(static (in NotificationRequest r, INotificationChannel c) =>
            {
                var message = new ChannelMessage
                {
                    Recipient = r.Recipient,
                    Subject = r.Subject,
                    Body = r.Message,
                    Priority = r.Priority,
                    Metadata = r.Metadata
                };
                return c.Send(message);
            })
            // After hooks
            .After((in NotificationRequest r, INotificationChannel c, NotificationResult result) =>
            {
                metrics.EndNotification(c.Name, result.Success);
                if (result.Success)
                {
                    Log.Info($"Notification sent: {result.MessageId}");
                    rateLimiter.RecordUsage(r.UserId, c.Name);
                }
                else
                {
                    Log.Warn($"Notification failed: {result.Error}");
                }
                return result;
            })
            .Build();
    }

    public NotificationResult Send(NotificationRequest request) =>
        _bridge.Execute(request);

    public async Task<NotificationResult> SendToPreferredChannelsAsync(
        string userId,
        string message,
        Priority priority,
        CancellationToken ct)
    {
        var preferences = await _userService.GetPreferencesAsync(userId, ct);
        var channels = priority switch
        {
            Priority.Critical => preferences.CriticalChannels,
            Priority.High => preferences.HighChannels,
            _ => preferences.DefaultChannels
        };

        NotificationResult? lastResult = null;
        foreach (var channel in channels)
        {
            var request = new NotificationRequest
            {
                UserId = userId,
                ChannelType = channel,
                Recipient = preferences.GetRecipient(channel),
                Message = message,
                Priority = priority
            };

            if (_bridge.TryExecute(request, out var result, out _))
            {
                if (result.Success)
                    return result;
                lastResult = result;
            }
        }

        return lastResult ?? new NotificationResult { Success = false, Error = "No channels available" };
    }
}

// Channel implementations
public class EmailChannel : INotificationChannel
{
    public string Name => "email";
    public bool IsRecipientValid(string recipient) => recipient.Contains('@');

    public NotificationResult Send(ChannelMessage message)
    {
        var email = new EmailMessage
        {
            To = message.Recipient,
            Subject = message.Subject ?? "Notification",
            Body = message.Body,
            Priority = message.Priority == Priority.Critical
                ? EmailPriority.High
                : EmailPriority.Normal
        };

        var messageId = _emailService.Send(email);
        return new NotificationResult { Success = true, MessageId = messageId };
    }
}

public class SlackChannel : INotificationChannel
{
    public string Name => "slack";
    public bool IsRecipientValid(string recipient) =>
        recipient.StartsWith("@") || recipient.StartsWith("#");

    public NotificationResult Send(ChannelMessage message)
    {
        var slackMessage = new SlackMessage
        {
            Channel = message.Recipient,
            Text = message.Body,
            Emoji = message.Priority == Priority.Critical ? ":rotating_light:" : ":bell:"
        };

        var response = _slackClient.PostMessage(slackMessage);
        return new NotificationResult
        {
            Success = response.Ok,
            MessageId = response.Ts,
            Error = response.Error
        };
    }
}
```

### Why This Pattern

- **Channel abstraction**: Add channels without changing core logic
- **Cross-cutting concerns**: Logging, metrics, rate limiting in hooks
- **Validation per channel**: Each channel validates its recipient format
- **Fallback support**: Try multiple channels in preference order

---

## Example 2: Document Export System

### The Problem

A document management system must export documents to various formats (PDF, DOCX, HTML, Markdown) with different rendering engines, watermarks, and access control based on user tier.

### The Solution

Use Bridge to separate export logic from format-specific renderers.

### The Code

```csharp
public class DocumentExportBridge
{
    private readonly Bridge<ExportRequest, ExportResult, IDocumentExporter> _bridge;

    public DocumentExportBridge(
        IExporterFactory exporterFactory,
        IAccessControl accessControl,
        IWatermarkService watermarkService)
    {
        _bridge = Bridge<ExportRequest, ExportResult, IDocumentExporter>
            .Create((in ExportRequest r) => exporterFactory.GetExporter(r.Format))
            // Access control
            .Require((in ExportRequest r, IDocumentExporter e) =>
            {
                var permission = accessControl.CheckExportPermission(r.UserId, r.DocumentId, r.Format);
                return permission.Allowed ? null : permission.Reason;
            })
            // Feature availability
            .Require((in ExportRequest r, IDocumentExporter e) =>
            {
                if (r.Options.IncludeComments && !e.SupportsComments)
                    return $"{r.Format} export does not support comments";
                if (r.Options.IncludeTrackChanges && !e.SupportsTrackChanges)
                    return $"{r.Format} export does not support track changes";
                return null;
            })
            // Logging
            .Before((in ExportRequest r, IDocumentExporter e) =>
                Log.Info($"Exporting document {r.DocumentId} to {r.Format} for user {r.UserId}"))
            // Core export
            .Operation(static (in ExportRequest r, IDocumentExporter e) =>
            {
                var document = LoadDocument(r.DocumentId);
                var exportOptions = new ExportOptions
                {
                    PageSize = r.Options.PageSize,
                    Orientation = r.Options.Orientation,
                    IncludeComments = r.Options.IncludeComments,
                    IncludeTrackChanges = r.Options.IncludeTrackChanges,
                    Quality = r.Options.Quality
                };

                return new ExportResult
                {
                    Data = e.Export(document, exportOptions),
                    MimeType = e.MimeType,
                    FileName = $"{document.Title}.{e.FileExtension}"
                };
            })
            // Apply watermark based on user tier
            .After((in ExportRequest r, IDocumentExporter e, ExportResult result) =>
            {
                var tier = accessControl.GetUserTier(r.UserId);
                if (tier != UserTier.Premium && e.SupportsWatermark)
                {
                    result.Data = watermarkService.Apply(result.Data, r.Format, "Trial Version");
                }
                return result;
            })
            // Size validation
            .RequireResult(static (in ExportRequest r, IDocumentExporter _, in ExportResult result) =>
            {
                var maxSize = r.Options.MaxOutputSize ?? 100_000_000;
                return result.Data.Length > maxSize
                    ? $"Export exceeds maximum size of {maxSize / 1_000_000}MB"
                    : null;
            })
            // Metrics
            .After((in ExportRequest r, IDocumentExporter e, ExportResult result) =>
            {
                Metrics.RecordExport(r.Format, result.Data.Length);
                return result;
            })
            .Build();
    }

    public ExportResult Export(ExportRequest request) =>
        _bridge.Execute(request);

    public (bool Success, ExportResult? Result, string? Error) TryExport(ExportRequest request)
    {
        if (_bridge.TryExecute(request, out var result, out var error))
            return (true, result, null);
        return (false, null, error);
    }
}

// Exporter implementations
public class PdfExporter : IDocumentExporter
{
    public string MimeType => "application/pdf";
    public string FileExtension => "pdf";
    public bool SupportsComments => true;
    public bool SupportsTrackChanges => false;
    public bool SupportsWatermark => true;

    public byte[] Export(Document document, ExportOptions options)
    {
        using var pdf = new PdfDocument();

        foreach (var page in document.Pages)
        {
            var pdfPage = pdf.AddPage();
            pdfPage.Size = MapPageSize(options.PageSize);
            pdfPage.Orientation = MapOrientation(options.Orientation);

            var renderer = new PageRenderer(pdfPage);
            renderer.Render(page);

            if (options.IncludeComments)
            {
                foreach (var comment in page.Comments)
                {
                    renderer.AddAnnotation(comment);
                }
            }
        }

        using var stream = new MemoryStream();
        pdf.Save(stream);
        return stream.ToArray();
    }
}

public class MarkdownExporter : IDocumentExporter
{
    public string MimeType => "text/markdown";
    public string FileExtension => "md";
    public bool SupportsComments => false;
    public bool SupportsTrackChanges => false;
    public bool SupportsWatermark => false;

    public byte[] Export(Document document, ExportOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {document.Title}");
        sb.AppendLine();

        foreach (var section in document.Sections)
        {
            sb.AppendLine($"## {section.Heading}");
            sb.AppendLine();
            sb.AppendLine(ConvertToMarkdown(section.Content));
            sb.AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
```

### Why This Pattern

- **Format abstraction**: Add exporters without changing core logic
- **Access control**: Permission checks before export
- **Feature validation**: Ensure format supports requested features
- **Tier-based watermarking**: Apply watermarks for non-premium users

---

## Example 3: Payment Gateway Integration

### The Problem

An e-commerce platform must process payments through multiple gateways (Stripe, PayPal, Square) with fraud detection, retry logic, and detailed audit logging.

### The Solution

Use Bridge to decouple payment processing from gateway-specific implementations.

### The Code

```csharp
public class PaymentBridge
{
    private readonly Bridge<PaymentRequest, PaymentResult, IPaymentGateway> _bridge;

    public PaymentBridge(
        IGatewayFactory gatewayFactory,
        IFraudDetector fraudDetector,
        IAuditLogger auditLogger)
    {
        _bridge = Bridge<PaymentRequest, PaymentResult, IPaymentGateway>
            .Create((in PaymentRequest r) => gatewayFactory.GetGateway(r.GatewayId))
            // Basic validation
            .Require(static (in PaymentRequest r, IPaymentGateway _) =>
                r.Amount <= 0 ? "Invalid amount" : null)
            .Require(static (in PaymentRequest r, IPaymentGateway g) =>
                !g.SupportsCurrency(r.Currency)
                    ? $"{g.Name} does not support {r.Currency}"
                    : null)
            .Require(static (in PaymentRequest r, IPaymentGateway g) =>
                r.Amount > g.MaxTransactionAmount
                    ? $"Amount exceeds {g.Name} maximum of {g.MaxTransactionAmount}"
                    : null)
            // Fraud detection
            .Require((in PaymentRequest r, IPaymentGateway _) =>
            {
                var fraudCheck = fraudDetector.Analyze(new FraudContext
                {
                    UserId = r.UserId,
                    Amount = r.Amount,
                    Currency = r.Currency,
                    PaymentMethod = r.PaymentMethod,
                    IpAddress = r.IpAddress,
                    DeviceFingerprint = r.DeviceFingerprint
                });

                return fraudCheck.RiskLevel switch
                {
                    RiskLevel.High => "Transaction flagged for review",
                    RiskLevel.Critical => "Transaction blocked for security",
                    _ => null
                };
            })
            // Audit start
            .Before((in PaymentRequest r, IPaymentGateway g) =>
            {
                auditLogger.LogPaymentStart(new PaymentAudit
                {
                    TransactionId = r.TransactionId,
                    Gateway = g.Name,
                    Amount = r.Amount,
                    Currency = r.Currency,
                    UserId = r.UserId,
                    Timestamp = DateTime.UtcNow
                });
            })
            // Process payment
            .Operation(static (in PaymentRequest r, IPaymentGateway g) =>
            {
                var gatewayRequest = new GatewayPaymentRequest
                {
                    Amount = r.Amount,
                    Currency = r.Currency,
                    PaymentMethodToken = r.PaymentMethodToken,
                    Description = r.Description,
                    Metadata = r.Metadata,
                    IdempotencyKey = r.TransactionId.ToString()
                };

                return g.ProcessPayment(gatewayRequest);
            })
            // Audit completion
            .After((in PaymentRequest r, IPaymentGateway g, PaymentResult result) =>
            {
                auditLogger.LogPaymentComplete(new PaymentAudit
                {
                    TransactionId = r.TransactionId,
                    Gateway = g.Name,
                    Success = result.Success,
                    GatewayTransactionId = result.GatewayTransactionId,
                    ErrorCode = result.ErrorCode,
                    Timestamp = DateTime.UtcNow
                });

                return result;
            })
            .Build();
    }

    public PaymentResult Process(PaymentRequest request) =>
        _bridge.Execute(request);

    public async Task<PaymentResult> ProcessWithRetryAsync(
        PaymentRequest request,
        CancellationToken ct)
    {
        var retryCount = 0;
        var maxRetries = 3;

        while (true)
        {
            if (_bridge.TryExecute(request, out var result, out var error))
            {
                if (result.Success)
                    return result;

                // Check if retryable
                if (result.IsRetryable && retryCount < maxRetries)
                {
                    retryCount++;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), ct);
                    continue;
                }

                return result;
            }

            // Validation error - not retryable
            return new PaymentResult
            {
                Success = false,
                ErrorCode = "VALIDATION_ERROR",
                ErrorMessage = error
            };
        }
    }
}
```

### Why This Pattern

- **Gateway abstraction**: Add gateways without core changes
- **Fraud detection**: Check before processing
- **Audit trail**: Complete logging via hooks
- **Currency/limit validation**: Per-gateway constraints

---

## Example 4: Cloud Storage Abstraction

### The Problem

A file management system must support multiple cloud storage providers (S3, Azure Blob, GCS) with encryption, compression, and quota management based on user plans.

### The Solution

Use Bridge to abstract storage operations from provider implementations.

### The Code

```csharp
public class StorageBridge
{
    private readonly Bridge<StorageRequest, StorageResult, IStorageProvider> _bridge;

    public StorageBridge(
        IStorageProviderFactory providerFactory,
        IQuotaService quotaService,
        IEncryptionService encryptionService)
    {
        _bridge = Bridge<StorageRequest, StorageResult, IStorageProvider>
            .Create((in StorageRequest r) => providerFactory.GetProvider(r.ProviderId))
            // Quota check
            .Require((in StorageRequest r, IStorageProvider p) =>
            {
                var quota = quotaService.GetQuota(r.UserId);
                var used = quotaService.GetUsed(r.UserId);
                var needed = r.Operation == StorageOperation.Upload ? r.Data.Length : 0;

                return used + needed > quota
                    ? $"Storage quota exceeded. Used: {used}, Quota: {quota}"
                    : null;
            })
            // File size limits
            .Require(static (in StorageRequest r, IStorageProvider p) =>
                r.Operation == StorageOperation.Upload && r.Data.Length > p.MaxFileSize
                    ? $"File exceeds maximum size of {p.MaxFileSize / 1_000_000}MB"
                    : null)
            // File type validation
            .Require(static (in StorageRequest r, IStorageProvider p) =>
            {
                if (r.Operation != StorageOperation.Upload)
                    return null;

                var extension = Path.GetExtension(r.Path);
                return p.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
                    ? null
                    : $"File type {extension} not allowed";
            })
            // Logging
            .Before(static (in StorageRequest r, IStorageProvider p) =>
                Log.Info($"{r.Operation} {r.Path} via {p.Name}"))
            // Core operation
            .Operation((in StorageRequest r, IStorageProvider p) =>
            {
                return r.Operation switch
                {
                    StorageOperation.Upload => HandleUpload(r, p, encryptionService),
                    StorageOperation.Download => HandleDownload(r, p, encryptionService),
                    StorageOperation.Delete => HandleDelete(r, p),
                    StorageOperation.List => HandleList(r, p),
                    _ => throw new NotSupportedException()
                };
            })
            // Update quota on upload
            .After((in StorageRequest r, IStorageProvider _, StorageResult result) =>
            {
                if (result.Success && r.Operation == StorageOperation.Upload)
                {
                    quotaService.RecordUsage(r.UserId, result.BytesWritten);
                }
                return result;
            })
            // Metrics
            .After(static (in StorageRequest r, IStorageProvider p, StorageResult result) =>
            {
                Metrics.RecordStorageOperation(p.Name, r.Operation.ToString(), result.Success);
                return result;
            })
            .Build();
    }

    private static StorageResult HandleUpload(
        in StorageRequest r,
        IStorageProvider p,
        IEncryptionService encryption)
    {
        var data = r.Data;

        // Compress if beneficial
        if (r.Options.Compress && data.Length > 1024)
        {
            data = Compress(data);
        }

        // Encrypt if requested
        if (r.Options.Encrypt)
        {
            data = encryption.Encrypt(data, r.UserId);
        }

        var result = p.Upload(r.Path, data, r.Metadata);
        return new StorageResult
        {
            Success = true,
            Path = result.Path,
            Url = result.Url,
            BytesWritten = data.Length,
            ETag = result.ETag
        };
    }

    public StorageResult Execute(StorageRequest request) =>
        _bridge.Execute(request);
}
```

### Why This Pattern

- **Provider abstraction**: Add providers without core changes
- **Quota management**: Check and update usage in hooks
- **Encryption/compression**: Apply transformations transparently
- **Per-provider limits**: Validate against provider constraints

---

## Key Takeaways

1. **Decouple what from how**: Abstraction and implementation vary independently
2. **Cross-cutting in hooks**: Logging, metrics, validation in Before/After
3. **Input-driven selection**: Choose implementation based on request
4. **Validation layering**: Pre-validation, operation, post-validation
5. **Safe execution**: TryExecute for expected failures

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
