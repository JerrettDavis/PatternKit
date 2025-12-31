# Document Processing with Visitor Generator

This example demonstrates how to use the Visitor Pattern Generator to build a comprehensive document processing pipeline. The scenario showcases common operations found in content management systems, document automation platforms, and legal tech applications.

## Scenario

We need to process various document types (PDF, Word, Excel, Markdown) through multiple stages:

1. **Validation** - Check if documents meet requirements (sync result visitor)
2. **Metadata Extraction** - Extract and log document properties (sync action visitor)
3. **Content Indexing** - Index documents for search (async result visitor)
4. **Security Scanning** - Scan for security issues (async action visitor)
5. **Complex Pipelines** - Combine multiple visitors in workflows

## Document Model

First, define the document hierarchy with the `[GenerateVisitor]` attribute:

```csharp
using PatternKit.Generators;

[GenerateVisitor]
public partial class Document
{
    public string Id { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

public partial class PdfDocument : Document
{
    public int PageCount { get; init; }
    public bool IsEncrypted { get; init; }
}

public partial class WordDocument : Document
{
    public int WordCount { get; init; }
    public bool HasMacros { get; init; }
}

public partial class SpreadsheetDocument : Document
{
    public int SheetCount { get; init; }
    public bool HasFormulas { get; init; }
}

public partial class MarkdownDocument : Document
{
    public int LineCount { get; init; }
    public bool HasCodeBlocks { get; init; }
}
```

The generator automatically creates visitor interfaces and Accept methods for all types.

## Use Case 1: Document Validation (Sync Result Visitor)

Validate documents and return detailed validation results:

```csharp
public record ValidationResult(bool IsValid, string Message);

// Build the validator
var validator = new DocumentVisitorBuilder<ValidationResult>()
    .When<PdfDocument>(pdf =>
    {
        if (pdf.PageCount == 0)
            return new ValidationResult(false, "PDF has no pages");
        if (pdf.IsEncrypted)
            return new ValidationResult(false, "Encrypted PDFs require password");
        if (pdf.SizeBytes > 100_000_000)
            return new ValidationResult(false, "PDF exceeds maximum size");
        return new ValidationResult(true, "PDF is valid");
    })
    .When<WordDocument>(word =>
    {
        if (word.WordCount == 0)
            return new ValidationResult(false, "Document is empty");
        if (word.HasMacros)
            return new ValidationResult(false, "Documents with macros require security review");
        if (word.SizeBytes > 50_000_000)
            return new ValidationResult(false, "Document exceeds maximum size");
        return new ValidationResult(true, "Document is valid");
    })
    .When<SpreadsheetDocument>(sheet =>
    {
        if (sheet.SheetCount == 0)
            return new ValidationResult(false, "Spreadsheet has no sheets");
        if (sheet.SheetCount > 100)
            return new ValidationResult(false, "Too many sheets (max 100)");
        return new ValidationResult(true, "Spreadsheet is valid");
    })
    .When<MarkdownDocument>(md =>
    {
        if (md.LineCount == 0)
            return new ValidationResult(false, "Markdown file is empty");
        return new ValidationResult(true, "Markdown is valid");
    })
    .Default(doc => new ValidationResult(false, $"Unknown document type: {doc.FileName}"))
    .Build();

// Use the validator
foreach (var doc in documents)
{
    var result = doc.Accept(validator);
    if (result.IsValid)
        Console.WriteLine($"✓ {doc.FileName}: {result.Message}");
    else
        Console.WriteLine($"✗ {doc.FileName}: {result.Message}");
}
```

### Key Benefits
- **Type-safe dispatch** - Compiler ensures all document types are handled
- **Clear validation logic** - Each document type has explicit validation rules
- **Reusable** - The validator can be used across the application
- **Testable** - Easy to unit test with mock documents

## Use Case 2: Metadata Extraction (Sync Action Visitor)

Extract and log metadata without returning values:

```csharp
var metadataLogger = new List<string>();

var extractor = new DocumentActionVisitorBuilder()
    .When<PdfDocument>(pdf => 
        metadataLogger.Add($"PDF: {pdf.FileName}, Pages: {pdf.PageCount}, Encrypted: {pdf.IsEncrypted}"))
    .When<WordDocument>(word => 
        metadataLogger.Add($"WORD: {word.FileName}, Words: {word.WordCount}, Macros: {word.HasMacros}"))
    .When<SpreadsheetDocument>(sheet => 
        metadataLogger.Add($"SHEET: {sheet.FileName}, Sheets: {sheet.SheetCount}, Formulas: {sheet.HasFormulas}"))
    .When<MarkdownDocument>(md => 
        metadataLogger.Add($"MD: {md.FileName}, Lines: {md.LineCount}, Code: {md.HasCodeBlocks}"))
    .Default(doc => 
        metadataLogger.Add($"UNKNOWN: {doc.FileName}"))
    .Build();

// Extract metadata from all documents
foreach (var doc in documents)
{
    doc.Accept(extractor);
}

// metadataLogger now contains all metadata entries
foreach (var entry in metadataLogger)
{
    Console.WriteLine(entry);
}
```

### Key Benefits
- **Side-effect focused** - Perfect for logging, metrics, auditing
- **No return value overhead** - Cleaner syntax for void operations
- **Composable** - Can be combined with other action visitors

## Use Case 3: Content Indexing (Async Result Visitor)

Asynchronously index documents for search:

```csharp
var indexer = new DocumentAsyncVisitorBuilder<string>()
    .WhenAsync<PdfDocument>(async (pdf, ct) =>
    {
        // Simulate OCR and text extraction
        await Task.Delay(150, ct);
        return $"PDF:{pdf.Id}:P{pdf.PageCount}";
    })
    .WhenAsync<WordDocument>(async (word, ct) =>
    {
        // Simulate text extraction
        await Task.Delay(100, ct);
        return $"WORD:{word.Id}:W{word.WordCount}";
    })
    .WhenAsync<SpreadsheetDocument>(async (sheet, ct) =>
    {
        // Simulate formula evaluation and data extraction
        await Task.Delay(200, ct);
        return $"SHEET:{sheet.Id}:S{sheet.SheetCount}";
    })
    .WhenAsync<MarkdownDocument>(async (md, ct) =>
    {
        // Simulate parsing
        await Task.Delay(50, ct);
        return $"MD:{md.Id}:L{md.LineCount}";
    })
    .DefaultAsync(async (doc, ct) =>
    {
        await Task.Delay(100, ct);
        return $"UNINDEXED:{doc.Id}";
    })
    .Build();

// Index documents asynchronously
foreach (var doc in documents)
{
    var indexKey = await doc.AcceptAsync(indexer, cancellationToken);
    Console.WriteLine($"Indexed: {doc.FileName} → {indexKey}");
    
    // Store indexKey in search engine...
}
```

### Key Benefits
- **True async/await** - Integrates with async APIs (database, HTTP, I/O)
- **ValueTask** - Reduces allocations for fast operations
- **Cancellation support** - Proper cancellation token propagation
- **Scalable** - Can process large document sets efficiently

## Use Case 4: Security Scanning (Async Action Visitor)

Asynchronously scan documents for security issues:

```csharp
var scanResults = new List<string>();

var scanner = new DocumentAsyncActionVisitorBuilder()
    .WhenAsync<PdfDocument>(async (pdf, ct) =>
    {
        // Simulate malware scan
        await Task.Delay(120, ct);
        if (pdf.IsEncrypted)
            scanResults.Add($"{pdf.FileName}: ⚠ Encrypted content requires manual review");
        else
            scanResults.Add($"{pdf.FileName}: ✓ No security issues detected");
    })
    .WhenAsync<WordDocument>(async (word, ct) =>
    {
        // Simulate macro analysis
        await Task.Delay(100, ct);
        if (word.HasMacros)
            scanResults.Add($"{word.FileName}: ⚠ Contains macros - sandboxed execution required");
        else
            scanResults.Add($"{word.FileName}: ✓ No security issues detected");
    })
    .WhenAsync<SpreadsheetDocument>(async (sheet, ct) =>
    {
        // Simulate formula and link analysis
        await Task.Delay(150, ct);
        if (sheet.HasFormulas)
            scanResults.Add($"{sheet.FileName}: ℹ Contains formulas - verify external references");
        else
            scanResults.Add($"{sheet.FileName}: ✓ No security issues detected");
    })
    .WhenAsync<MarkdownDocument>(async (md, ct) =>
    {
        // Simulate XSS/injection scan
        await Task.Delay(50, ct);
        if (md.HasCodeBlocks)
            scanResults.Add($"{md.FileName}: ℹ Contains code blocks - ensure safe rendering");
        else
            scanResults.Add($"{md.FileName}: ✓ No security issues detected");
    })
    .DefaultAsync(async (doc, ct) =>
    {
        await Task.Delay(50, ct);
        scanResults.Add($"{doc.FileName}: Unknown format - manual review required");
    })
    .Build();

// Scan all documents
foreach (var doc in documents)
{
    await doc.AcceptAsync(scanner, cancellationToken);
}

// Review scan results
foreach (var result in scanResults)
{
    Console.WriteLine(result);
}
```

### Key Benefits
- **Async side effects** - Perfect for I/O-bound operations (logging to database, external API calls)
- **Non-blocking** - Doesn't block the thread while waiting
- **Resource efficient** - Can scan many documents concurrently

## Use Case 5: Complex Processing Pipeline

Combine multiple visitors in a complete workflow:

```csharp
async Task ProcessDocumentAsync(Document doc, CancellationToken ct)
{
    Console.WriteLine($"Processing: {doc.FileName}");
    
    // Step 1: Validate
    var validationResult = doc.Accept(validator);
    if (!validationResult.IsValid)
    {
        Console.WriteLine($"  ✗ Skipped: {validationResult.Message}");
        return;
    }
    Console.WriteLine($"  ✓ Validated");
    
    // Step 2: Extract metadata
    doc.Accept(extractor);
    Console.WriteLine($"  → Metadata extracted");
    
    // Step 3: Index for search
    var indexKey = await doc.AcceptAsync(indexer, ct);
    Console.WriteLine($"  → Indexed: {indexKey}");
    
    // Step 4: Security scan
    await doc.AcceptAsync(scanner, ct);
    Console.WriteLine($"  → Security scan complete");
    
    Console.WriteLine($"  ✓ Processing complete\n");
}

// Process all documents through the pipeline
var tasks = documents.Select(doc => ProcessDocumentAsync(doc, cancellationToken));
await Task.WhenAll(tasks);
```

### Pipeline Benefits
- **Modular** - Each stage is independent and reusable
- **Composable** - Easy to add/remove/reorder stages
- **Maintainable** - Clear separation of concerns
- **Testable** - Each visitor can be tested independently

## Real-World Enhancements

### Error Handling

```csharp
var resilientValidator = new DocumentVisitorBuilder<ValidationResult>()
    .When<PdfDocument>(pdf =>
    {
        try
        {
            return ValidatePdf(pdf);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PDF validation failed");
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    })
    // ... other handlers with try/catch
    .Build();
```

### Progress Reporting

```csharp
var progressReporter = new DocumentActionVisitorBuilder()
    .When<PdfDocument>(pdf => 
        progress.Report($"Processing PDF: {pdf.PageCount} pages"))
    .When<WordDocument>(word => 
        progress.Report($"Processing Word: {word.WordCount} words"))
    // ... other handlers
    .Build();

foreach (var doc in documents)
{
    doc.Accept(progressReporter);
    // ... process document
}
```

### Caching

```csharp
var cache = new Dictionary<string, ValidationResult>();

var cachedValidator = new DocumentVisitorBuilder<ValidationResult>()
    .When<PdfDocument>(pdf =>
    {
        if (cache.TryGetValue(pdf.Id, out var cached))
            return cached;
            
        var result = ValidatePdf(pdf);
        cache[pdf.Id] = result;
        return result;
    })
    // ... other handlers with caching
    .Build();
```

## Performance Considerations

The visitor generator produces highly optimized code:

- **O(1) dispatch** - Dictionary-based type lookup
- **No reflection** - All types resolved at compile time
- **Minimal allocations** - Builder reuses dictionary, implementations are sealed
- **ValueTask for async** - Reduces GC pressure for fast async operations

## Comparison to Manual Implementation

### Manual Visitor (Traditional)

```csharp
// Requires defining interfaces and implementing for each operation
public interface IDocumentVisitor<T>
{
    T Visit(PdfDocument pdf);
    T Visit(WordDocument word);
    // ... all types
}

// Requires Accept methods on each type
public partial class PdfDocument
{
    public T Accept<T>(IDocumentVisitor<T> visitor) => visitor.Visit(this);
}

// Requires separate class for each operation
public class ValidationVisitor : IDocumentVisitor<ValidationResult>
{
    public ValidationResult Visit(PdfDocument pdf) { /* ... */ }
    public ValidationResult Visit(WordDocument word) { /* ... */ }
    // ... all types
}
```

### Generated Visitor (PatternKit)

```csharp
// Just annotate the base type
[GenerateVisitor]
public partial class Document { }

// Use fluent builder - no boilerplate
var validator = new DocumentVisitorBuilder<ValidationResult>()
    .When<PdfDocument>(pdf => ValidatePdf(pdf))
    .When<WordDocument>(word => ValidateWord(word))
    .Build();
```

**Lines of code saved: ~80% reduction in boilerplate**

## Next Steps

- Review [Visitor Generator Documentation](../generators/visitor-generator.md) for advanced options
- Explore [Runtime Visitor Pattern](../patterns/behavioral/visitor/visitor.md) for non-generated scenarios
- See [Source Generator Best Practices](../generators/best-practices.md) for performance tips

## Complete Example

The full working example is available at:
- [DocumentProcessingDemo.cs](../../src/PatternKit.Examples/Generators/Visitors/DocumentProcessingDemo.cs)

Run it with:
```bash
dotnet run --project src/PatternKit.Examples -- document-processing
```
