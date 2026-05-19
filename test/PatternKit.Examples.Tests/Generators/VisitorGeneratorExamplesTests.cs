using PatternKit.Examples.Generators.Visitors;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.GeneratorTests;

[Feature("Visitor generator examples")]
public sealed class VisitorGeneratorExamplesTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("ResultVisitor DispatchesToConcreteDocumentHandlers")]
    [Fact]
    public void ResultVisitor_DispatchesToConcreteDocumentHandlers()
    {
        var visitor = new DocumentVisitorBuilder<string>()
            .When<PdfDocument>(pdf => $"pdf:{pdf.PageCount}")
            .When<WordDocument>(word => $"word:{word.WordCount}")
            .When<SpreadsheetDocument>(sheet => $"sheet:{sheet.SheetCount}")
            .When<MarkdownDocument>(markdown => $"markdown:{markdown.LineCount}")
            .Default(document => $"default:{document.FileName}")
            .Build();

        ScenarioExpect.Equal("pdf:3", new PdfDocument { FileName = "a.pdf", PageCount = 3 }.Accept(visitor));
        ScenarioExpect.Equal("word:400", new WordDocument { FileName = "a.docx", WordCount = 400 }.Accept(visitor));
        ScenarioExpect.Equal("sheet:2", new SpreadsheetDocument { FileName = "a.xlsx", SheetCount = 2 }.Accept(visitor));
        ScenarioExpect.Equal("markdown:9", new MarkdownDocument { FileName = "README.md", LineCount = 9 }.Accept(visitor));
    }

    [Scenario("ActionVisitor CollectsMetadataForEachConcreteDocument")]
    [Fact]
    public void ActionVisitor_CollectsMetadataForEachConcreteDocument()
    {
        var log = new List<string>();
        var visitor = new DocumentActionVisitorBuilder()
            .When<PdfDocument>(pdf => log.Add($"pdf:{pdf.FileName}:{pdf.IsEncrypted}"))
            .When<WordDocument>(word => log.Add($"word:{word.FileName}:{word.HasMacros}"))
            .When<SpreadsheetDocument>(sheet => log.Add($"sheet:{sheet.FileName}:{sheet.HasFormulas}"))
            .When<MarkdownDocument>(markdown => log.Add($"markdown:{markdown.FileName}:{markdown.HasCodeBlocks}"))
            .Default(document => log.Add($"default:{document.FileName}"))
            .Build();

        new PdfDocument { FileName = "contract.pdf", IsEncrypted = true }.Accept(visitor);
        new WordDocument { FileName = "memo.docx", HasMacros = false }.Accept(visitor);
        new SpreadsheetDocument { FileName = "budget.xlsx", HasFormulas = true }.Accept(visitor);
        new MarkdownDocument { FileName = "README.md", HasCodeBlocks = true }.Accept(visitor);

        ScenarioExpect.Equal(
            ["pdf:contract.pdf:True", "word:memo.docx:False", "sheet:budget.xlsx:True", "markdown:README.md:True"],
            log);
    }

    [Scenario("AsyncVisitors DispatchToConcreteDocumentHandlers")]
    [Fact]
    public async Task AsyncVisitors_DispatchToConcreteDocumentHandlers()
    {
        var resultVisitor = new DocumentAsyncVisitorBuilder<string>()
            .WhenAsync<PdfDocument>((pdf, _) => ValueTask.FromResult($"pdf:{pdf.Id}"))
            .WhenAsync<WordDocument>((word, _) => ValueTask.FromResult($"word:{word.Id}"))
            .WhenAsync<SpreadsheetDocument>((sheet, _) => ValueTask.FromResult($"sheet:{sheet.Id}"))
            .WhenAsync<MarkdownDocument>((markdown, _) => ValueTask.FromResult($"markdown:{markdown.Id}"))
            .DefaultAsync((document, _) => ValueTask.FromResult($"default:{document.Id}"))
            .Build();
        var actionLog = new List<string>();
        var actionVisitor = new DocumentAsyncActionVisitorBuilder()
            .WhenAsync<PdfDocument>((pdf, _) =>
            {
                actionLog.Add($"pdf:{pdf.Id}");
                return ValueTask.CompletedTask;
            })
            .DefaultAsync((document, _) =>
            {
                actionLog.Add($"default:{document.Id}");
                return ValueTask.CompletedTask;
            })
            .Build();
        var document = new PdfDocument { Id = "DOC-1", FileName = "doc.pdf" };

        var result = await document.AcceptAsync(resultVisitor);
        await document.AcceptAsync(actionVisitor);

        ScenarioExpect.Equal("pdf:DOC-1", result);
        ScenarioExpect.Equal(["pdf:DOC-1"], actionLog);
    }

    [Scenario("DocumentProcessingDemo RunAsync Completes")]
    [Fact]
    public async Task DocumentProcessingDemo_RunAsync_Completes()
    {
        await DocumentProcessingDemo.RunAsync();
    }

    [Scenario("Document validation covers happy and sad paths for every supported document type")]
    [Fact]
    public async Task DocumentProcessingDemo_ValidationRules_CoverSupportedDocumentTypes()
    {
        await Given("document validation cases", () => new ValidationCase[]
            {
                new("valid-pdf", new PdfDocument { FileName = "ok.pdf", PageCount = 1, SizeBytes = 1024 }, true, "PDF is valid"),
                new("empty-pdf", new PdfDocument { FileName = "empty.pdf", PageCount = 0, SizeBytes = 1024 }, false, "no pages"),
                new("encrypted-pdf", new PdfDocument { FileName = "secret.pdf", PageCount = 1, IsEncrypted = true, SizeBytes = 1024 }, false, "password"),
                new("large-pdf", new PdfDocument { FileName = "large.pdf", PageCount = 1, SizeBytes = 100_000_001 }, false, "maximum size"),
                new("valid-word", new WordDocument { FileName = "ok.docx", WordCount = 1, SizeBytes = 1024 }, true, "Document is valid"),
                new("empty-word", new WordDocument { FileName = "empty.docx", WordCount = 0, SizeBytes = 1024 }, false, "empty"),
                new("macro-word", new WordDocument { FileName = "macro.docx", WordCount = 1, HasMacros = true, SizeBytes = 1024 }, false, "macros"),
                new("large-word", new WordDocument { FileName = "large.docx", WordCount = 1, SizeBytes = 50_000_001 }, false, "maximum size"),
                new("valid-sheet", new SpreadsheetDocument { FileName = "ok.xlsx", SheetCount = 1, SizeBytes = 1024 }, true, "Spreadsheet is valid"),
                new("empty-sheet", new SpreadsheetDocument { FileName = "empty.xlsx", SheetCount = 0, SizeBytes = 1024 }, false, "no sheets"),
                new("wide-sheet", new SpreadsheetDocument { FileName = "wide.xlsx", SheetCount = 101, SizeBytes = 1024 }, false, "Too many sheets"),
                new("large-sheet", new SpreadsheetDocument { FileName = "large.xlsx", SheetCount = 1, SizeBytes = 75_000_001 }, false, "maximum size"),
                new("valid-markdown", new MarkdownDocument { FileName = "ok.md", LineCount = 1, SizeBytes = 1024 }, true, "Markdown is valid"),
                new("empty-markdown", new MarkdownDocument { FileName = "empty.md", LineCount = 0, SizeBytes = 1024 }, false, "empty"),
                new("large-markdown", new MarkdownDocument { FileName = "large.md", LineCount = 1, SizeBytes = 10_000_001 }, false, "maximum size")
            })
            .When("validating each document through the demo rule methods", cases =>
                cases.Select(c => new
                {
                    c.Name,
                    c.ExpectedValid,
                    c.ExpectedMessageFragment,
                    Result = Validate(c.Document)
                }).ToArray())
            .Then("each case has the expected validity", results => results.All(r => r.Result.IsValid == r.ExpectedValid))
            .And("each case reports the expected message", results =>
                results.All(r => r.Result.Message.Contains(r.ExpectedMessageFragment, StringComparison.OrdinalIgnoreCase)))
            .AssertPassed();
    }

    [Scenario("Document async visitors index, scan, default, and cancellation paths")]
    [Fact]
    public async Task DocumentProcessingDemo_AsyncVisitors_CoverIndexScanDefaultAndCancellationPaths()
    {
        await Given("documents and async visitors", () =>
            {
                var scans = new List<string>();
                var indexer = new DocumentAsyncVisitorBuilder<string>()
                    .WhenAsync<PdfDocument>((pdf, ct) => new ValueTask<string>(Task.Delay(1, ct).ContinueWith(_ => $"PDF:{pdf.Id}", ct)))
                    .DefaultAsync((document, ct) => new ValueTask<string>(Task.Delay(1, ct).ContinueWith(_ => $"DEFAULT:{document.Id}", ct)))
                    .Build();
                var scanner = new DocumentAsyncActionVisitorBuilder()
                    .WhenAsync<PdfDocument>((pdf, ct) =>
                    {
                        scans.Add(pdf.IsEncrypted ? "encrypted" : "clear");
                        return ValueTask.CompletedTask;
                    })
                    .DefaultAsync((document, ct) =>
                    {
                        scans.Add($"default:{document.FileName}");
                        return ValueTask.CompletedTask;
                    })
                    .Build();

                return new { Indexer = indexer, Scanner = scanner, Scans = scans };
            })
            .When("running matched, default, and cancelled async visits",
                async Task<(List<string> scans, string pdfKey, string defaultKey, OperationCanceledException? cancelled)> (harness) =>
            {
                var pdf = new PdfDocument { Id = "DOC-PDF", FileName = "file.pdf", IsEncrypted = true };
                var unknown = new UnknownDocument { Id = "DOC-UNKNOWN", FileName = "file.bin" };

                var pdfKey = await pdf.AcceptAsync(harness.Indexer);
                var defaultKey = await unknown.AcceptAsync(harness.Indexer);
                await pdf.AcceptAsync(harness.Scanner);
                await unknown.AcceptAsync(harness.Scanner);

                OperationCanceledException? cancelled = null;
                using var cts = new CancellationTokenSource();
                cts.Cancel();
                try
                {
                    await pdf.AcceptAsync(harness.Indexer, cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    cancelled = ex;
                }

                return (harness.Scans, pdfKey, defaultKey, cancelled);
            })
            .Then("the concrete visitor indexed the PDF", result => result.pdfKey == "PDF:DOC-PDF")
            .And("the default visitor indexed an unknown document", result => result.defaultKey == "DEFAULT:DOC-UNKNOWN")
            .And("the scanner recorded concrete and default scan paths", result =>
                result.scans.SequenceEqual(["encrypted", "default:file.bin"]))
            .And("async indexing observes cancellation", result => result.cancelled is not null)
            .AssertPassed();
    }

    [Scenario("Document processing private helpers cover concrete async indexing and scan branches")]
    [Fact]
    public async Task DocumentProcessingDemo_PrivateAsyncHelpers_CoverConcreteBranches()
    {
        await Given("documents that exercise every helper branch", () => new
            {
                Pdf = new PdfDocument { Id = "PDF-1", FileName = "secure.pdf", PageCount = 4, IsEncrypted = true },
                Word = new WordDocument { Id = "WORD-1", FileName = "macro.docx", WordCount = 12, HasMacros = true },
                SheetWithFormulas = new SpreadsheetDocument { Id = "SHEET-1", FileName = "calc.xlsx", SheetCount = 3, HasFormulas = true },
                SheetWithoutFormulas = new SpreadsheetDocument { Id = "SHEET-2", FileName = "plain.xlsx", SheetCount = 1, HasFormulas = false },
                MarkdownWithCode = new MarkdownDocument { Id = "MD-1", FileName = "code.md", LineCount = 10, HasCodeBlocks = true },
                MarkdownWithoutCode = new MarkdownDocument { Id = "MD-2", FileName = "plain.md", LineCount = 5, HasCodeBlocks = false }
            })
            .When("invoking the indexing and scanning helpers", async Task<(string[] Keys, List<string> Scans)> (docs) =>
            {
                var keys = new[]
                {
                    await InvokeIndexAsync("IndexPdfAsync", docs.Pdf),
                    await InvokeIndexAsync("IndexWordAsync", docs.Word),
                    await InvokeIndexAsync("IndexSpreadsheetAsync", docs.SheetWithFormulas),
                    await InvokeIndexAsync("IndexMarkdownAsync", docs.MarkdownWithCode)
                };
                var scans = new List<string>();

                await InvokeScanAsync("ScanPdfSecurityAsync", docs.Pdf, scans);
                await InvokeScanAsync("ScanWordSecurityAsync", docs.Word, scans);
                await InvokeScanAsync("ScanSpreadsheetSecurityAsync", docs.SheetWithFormulas, scans);
                await InvokeScanAsync("ScanSpreadsheetSecurityAsync", docs.SheetWithoutFormulas, scans);
                await InvokeScanAsync("ScanMarkdownSecurityAsync", docs.MarkdownWithCode, scans);
                await InvokeScanAsync("ScanMarkdownSecurityAsync", docs.MarkdownWithoutCode, scans);

                return (keys, scans);
            })
            .Then("all concrete index keys are produced", result =>
                result.Keys.SequenceEqual(["PDF:PDF-1:P4", "WORD:WORD-1:W12", "SHEET:SHEET-1:S3", "MD:MD-1:L10"]))
            .And("security scans include positive and negative findings", result =>
                result.Scans.Any(s => s.Contains("Encrypted content", StringComparison.Ordinal))
                && result.Scans.Any(s => s.Contains("Contains macros", StringComparison.Ordinal))
                && result.Scans.Any(s => s.Contains("Contains formulas", StringComparison.Ordinal))
                && result.Scans.Any(s => s.Contains("No security issues", StringComparison.Ordinal))
                && result.Scans.Any(s => s.Contains("Contains code blocks", StringComparison.Ordinal)))
            .AssertPassed();
    }

    private static DocumentProcessingDemo.ValidationResult Validate(Document document)
    {
        var methodName = document switch
        {
            PdfDocument => "ValidatePdf",
            WordDocument => "ValidateWord",
            SpreadsheetDocument => "ValidateSpreadsheet",
            MarkdownDocument => "ValidateMarkdown",
            _ => throw new ArgumentOutOfRangeException(nameof(document), document.GetType().Name, "Unsupported document type.")
        };

        var method = typeof(DocumentProcessingDemo).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        ScenarioExpect.NotNull(method);
        return ScenarioExpect.IsType<DocumentProcessingDemo.ValidationResult>(method.Invoke(null, [document]));
    }

    private static async Task<string> InvokeIndexAsync(string methodName, Document document)
    {
        var method = typeof(DocumentProcessingDemo).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        ScenarioExpect.NotNull(method);
        var task = ScenarioExpect.IsAssignableFrom<Task<string>>(method.Invoke(null, [document, CancellationToken.None]));
        return await task;
    }

    private static async Task InvokeScanAsync(string methodName, Document document, List<string> scans)
    {
        var method = typeof(DocumentProcessingDemo).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        ScenarioExpect.NotNull(method);
        var task = ScenarioExpect.IsAssignableFrom<Task>(method.Invoke(null, [document, scans, CancellationToken.None]));
        await task;
    }

    private sealed record ValidationCase(
        string Name,
        Document Document,
        bool ExpectedValid,
        string ExpectedMessageFragment);

    private sealed class UnknownDocument : Document
    {
    }
}
