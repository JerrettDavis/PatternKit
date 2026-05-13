using PatternKit.Examples.Generators.Visitors;

namespace PatternKit.Examples.Tests.GeneratorTests;

public sealed class VisitorGeneratorExamplesTests
{
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

        Assert.Equal("pdf:3", new PdfDocument { FileName = "a.pdf", PageCount = 3 }.Accept(visitor));
        Assert.Equal("word:400", new WordDocument { FileName = "a.docx", WordCount = 400 }.Accept(visitor));
        Assert.Equal("sheet:2", new SpreadsheetDocument { FileName = "a.xlsx", SheetCount = 2 }.Accept(visitor));
        Assert.Equal("markdown:9", new MarkdownDocument { FileName = "README.md", LineCount = 9 }.Accept(visitor));
    }

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

        Assert.Equal(
            ["pdf:contract.pdf:True", "word:memo.docx:False", "sheet:budget.xlsx:True", "markdown:README.md:True"],
            log);
    }

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

        Assert.Equal("pdf:DOC-1", result);
        Assert.Equal(["pdf:DOC-1"], actionLog);
    }

    [Fact]
    public async Task DocumentProcessingDemo_RunAsync_Completes()
    {
        await DocumentProcessingDemo.RunAsync();
    }
}
