using PatternKit.Generators.Visitors;

namespace PatternKit.Examples.Generators.Visitors;

#region Document Model

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

#endregion

/// <summary>
/// Comprehensive example demonstrating the visitor pattern generator in a document processing pipeline.
/// This example shows how to use all four visitor types (sync/async, result/action) for different
/// document processing scenarios commonly found in content management systems, legal tech, and 
/// document automation platforms.
/// </summary>
public static class DocumentProcessingDemo
{
    /// <summary>
    /// Runs the comprehensive document processing demonstration showing all visitor types in action.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Document Processing Pipeline with Visitor Pattern Generator ===\n");

        // Sample documents
        var documents = new Document[]
        {
            new PdfDocument 
            { 
                Id = "DOC-001", 
                FileName = "Annual_Report_2024.pdf", 
                PageCount = 42,
                IsEncrypted = false,
                SizeBytes = 5_242_880 // 5 MB
            },
            new WordDocument 
            { 
                Id = "DOC-002", 
                FileName = "Quarterly_Review.docx", 
                WordCount = 3_500,
                HasMacros = false,
                SizeBytes = 102_400 // 100 KB
            },
            new SpreadsheetDocument 
            { 
                Id = "DOC-003", 
                FileName = "Financial_Data_Q4.xlsx", 
                SheetCount = 8,
                HasFormulas = true,
                SizeBytes = 2_097_152 // 2 MB
            },
            new MarkdownDocument 
            { 
                Id = "DOC-004", 
                FileName = "README.md", 
                LineCount = 250,
                HasCodeBlocks = true,
                SizeBytes = 15_360 // 15 KB
            }
        };

        // 1. Document Validation (Sync Result Visitor)
        Console.WriteLine("1. Document Validation (Sync Result Visitor)");
        Console.WriteLine("   Validates documents and returns validation results\n");
        
        var validator = new DocumentVisitorBuilder<ValidationResult>()
            .When<PdfDocument>(pdf => ValidatePdf(pdf))
            .When<WordDocument>(word => ValidateWord(word))
            .When<SpreadsheetDocument>(sheet => ValidateSpreadsheet(sheet))
            .When<MarkdownDocument>(md => ValidateMarkdown(md))
            .Default(doc => new ValidationResult(false, $"Unknown document type: {doc.FileName}"))
            .Build();

        foreach (var doc in documents)
        {
            var result = doc.Accept(validator);
            var status = result.IsValid ? "✓ VALID" : "✗ INVALID";
            Console.WriteLine($"   {status}: {doc.FileName} - {result.Message}");
        }
        Console.WriteLine();

        // 2. Metadata Extraction (Action Visitor)
        Console.WriteLine("2. Metadata Extraction (Action Visitor)");
        Console.WriteLine("   Extracts and logs metadata from documents\n");
        
        var metadataLogger = new List<string>();
        var metadataExtractor = new DocumentActionVisitorBuilder()
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

        foreach (var doc in documents)
        {
            doc.Accept(metadataExtractor);
        }

        foreach (var entry in metadataLogger)
        {
            Console.WriteLine($"   {entry}");
        }
        Console.WriteLine();

        // 3. Async Content Indexing (Async Result Visitor)
        Console.WriteLine("3. Async Content Indexing (Async Result Visitor)");
        Console.WriteLine("   Asynchronously indexes documents and returns index keys\n");
        
        var indexer = new DocumentAsyncVisitorBuilder<string>()
            .WhenAsync<PdfDocument>(async (pdf, ct) => await IndexPdfAsync(pdf, ct))
            .WhenAsync<WordDocument>(async (word, ct) => await IndexWordAsync(word, ct))
            .WhenAsync<SpreadsheetDocument>(async (sheet, ct) => await IndexSpreadsheetAsync(sheet, ct))
            .WhenAsync<MarkdownDocument>(async (md, ct) => await IndexMarkdownAsync(md, ct))
            .DefaultAsync(async (doc, ct) =>
            {
                await Task.Delay(100, ct); // Simulate async work
                return $"UNINDEXED:{doc.Id}";
            })
            .Build();

        foreach (var doc in documents)
        {
            var indexKey = await doc.AcceptAsync(indexer);
            Console.WriteLine($"   Indexed: {doc.FileName} → {indexKey}");
        }
        Console.WriteLine();

        // 4. Async Security Scanning (Async Action Visitor)
        Console.WriteLine("4. Async Security Scanning (Async Action Visitor)");
        Console.WriteLine("   Asynchronously scans documents for security issues\n");
        
        var scanResults = new List<string>();
        var securityScanner = new DocumentAsyncActionVisitorBuilder()
            .WhenAsync<PdfDocument>(async (pdf, ct) => await ScanPdfSecurityAsync(pdf, scanResults, ct))
            .WhenAsync<WordDocument>(async (word, ct) => await ScanWordSecurityAsync(word, scanResults, ct))
            .WhenAsync<SpreadsheetDocument>(async (sheet, ct) => await ScanSpreadsheetSecurityAsync(sheet, scanResults, ct))
            .WhenAsync<MarkdownDocument>(async (md, ct) => await ScanMarkdownSecurityAsync(md, scanResults, ct))
            .DefaultAsync(async (doc, ct) =>
            {
                await Task.Delay(50, ct);
                scanResults.Add($"{doc.FileName}: Unknown format - manual review required");
            })
            .Build();

        foreach (var doc in documents)
        {
            await doc.AcceptAsync(securityScanner);
        }

        foreach (var result in scanResults)
        {
            Console.WriteLine($"   {result}");
        }
        Console.WriteLine();

        // 5. Complex Processing Pipeline
        Console.WriteLine("5. Complex Processing Pipeline");
        Console.WriteLine("   Combining multiple visitors for a complete workflow\n");
        
        foreach (var doc in documents)
        {
            Console.WriteLine($"   Processing: {doc.FileName}");
            
            // Validate
            var validationResult = doc.Accept(validator);
            if (!validationResult.IsValid)
            {
                Console.WriteLine($"      ✗ Skipped (validation failed): {validationResult.Message}");
                continue;
            }
            
            // Index
            var key = await doc.AcceptAsync(indexer);
            Console.WriteLine($"      → Indexed with key: {key}");
            
            // Security scan
            var beforeCount = scanResults.Count;
            await doc.AcceptAsync(securityScanner);
            var newScans = scanResults.Skip(beforeCount);
            if (newScans.Any())
            {
                foreach (var scan in newScans)
                {
                    Console.WriteLine($"      → Security: {scan}");
                }
            }
            
            Console.WriteLine();
        }

        Console.WriteLine("=== Demo Complete ===");
    }

    #region Validation (Sync Result Visitor)

    public record ValidationResult(bool IsValid, string Message);

    private static ValidationResult ValidatePdf(PdfDocument pdf)
    {
        if (pdf.PageCount == 0)
            return new ValidationResult(false, "PDF has no pages");
        if (pdf.IsEncrypted)
            return new ValidationResult(false, "Encrypted PDFs require password");
        if (pdf.SizeBytes > 100_000_000) // 100 MB
            return new ValidationResult(false, "PDF exceeds maximum size");
        return new ValidationResult(true, "PDF is valid");
    }

    private static ValidationResult ValidateWord(WordDocument word)
    {
        if (word.WordCount == 0)
            return new ValidationResult(false, "Document is empty");
        if (word.HasMacros)
            return new ValidationResult(false, "Documents with macros require security review");
        if (word.SizeBytes > 50_000_000) // 50 MB
            return new ValidationResult(false, "Document exceeds maximum size");
        return new ValidationResult(true, "Document is valid");
    }

    private static ValidationResult ValidateSpreadsheet(SpreadsheetDocument sheet)
    {
        if (sheet.SheetCount == 0)
            return new ValidationResult(false, "Spreadsheet has no sheets");
        if (sheet.SheetCount > 100)
            return new ValidationResult(false, "Too many sheets (max 100)");
        if (sheet.SizeBytes > 75_000_000) // 75 MB
            return new ValidationResult(false, "Spreadsheet exceeds maximum size");
        return new ValidationResult(true, "Spreadsheet is valid");
    }

    private static ValidationResult ValidateMarkdown(MarkdownDocument md)
    {
        if (md.LineCount == 0)
            return new ValidationResult(false, "Markdown file is empty");
        if (md.SizeBytes > 10_000_000) // 10 MB
            return new ValidationResult(false, "Markdown file exceeds maximum size");
        return new ValidationResult(true, "Markdown is valid");
    }

    #endregion

    #region Async Indexing (Async Result Visitor)

    private static async Task<string> IndexPdfAsync(PdfDocument pdf, CancellationToken ct)
    {
        await Task.Delay(150, ct); // Simulate OCR and text extraction
        return $"PDF:{pdf.Id}:P{pdf.PageCount}";
    }

    private static async Task<string> IndexWordAsync(WordDocument word, CancellationToken ct)
    {
        await Task.Delay(100, ct); // Simulate text extraction
        return $"WORD:{word.Id}:W{word.WordCount}";
    }

    private static async Task<string> IndexSpreadsheetAsync(SpreadsheetDocument sheet, CancellationToken ct)
    {
        await Task.Delay(200, ct); // Simulate formula evaluation and data extraction
        return $"SHEET:{sheet.Id}:S{sheet.SheetCount}";
    }

    private static async Task<string> IndexMarkdownAsync(MarkdownDocument md, CancellationToken ct)
    {
        await Task.Delay(50, ct); // Simulate parsing
        return $"MD:{md.Id}:L{md.LineCount}";
    }

    #endregion

    #region Security Scanning (Async Action Visitor)

    private static async Task ScanPdfSecurityAsync(PdfDocument pdf, List<string> results, CancellationToken ct)
    {
        await Task.Delay(120, ct); // Simulate malware scan
        if (pdf.IsEncrypted)
            results.Add($"{pdf.FileName}: ⚠ Encrypted content requires manual review");
        else
            results.Add($"{pdf.FileName}: ✓ No security issues detected");
    }

    private static async Task ScanWordSecurityAsync(WordDocument word, List<string> results, CancellationToken ct)
    {
        await Task.Delay(100, ct); // Simulate macro analysis
        if (word.HasMacros)
            results.Add($"{word.FileName}: ⚠ Contains macros - sandboxed execution required");
        else
            results.Add($"{word.FileName}: ✓ No security issues detected");
    }

    private static async Task ScanSpreadsheetSecurityAsync(SpreadsheetDocument sheet, List<string> results, CancellationToken ct)
    {
        await Task.Delay(150, ct); // Simulate formula and link analysis
        if (sheet.HasFormulas)
            results.Add($"{sheet.FileName}: ℹ Contains formulas - verify external references");
        else
            results.Add($"{sheet.FileName}: ✓ No security issues detected");
    }

    private static async Task ScanMarkdownSecurityAsync(MarkdownDocument md, List<string> results, CancellationToken ct)
    {
        await Task.Delay(50, ct); // Simulate XSS/injection scan
        if (md.HasCodeBlocks)
            results.Add($"{md.FileName}: ℹ Contains code blocks - ensure safe rendering");
        else
            results.Add($"{md.FileName}: ✓ No security issues detected");
    }

    #endregion
}
