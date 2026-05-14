using PatternKit.Examples.Decorators;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Decorators;

[Feature("Storage decorator example")]
[Collection(PatternKit.Examples.Tests.ConsoleTestCollection.Name)]
public sealed class StorageDecoratorExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Fact]
    public void InMemoryStorage_ReadWriteExistsAndDelete_WorkAsExpected()
    {
        var storage = new InMemoryFileStorage();

        storage.WriteFile("readme.txt", "hello");

        Assert.True(storage.FileExists("readme.txt"));
        Assert.Equal("hello", storage.ReadFile("readme.txt"));

        storage.DeleteFile("readme.txt");

        Assert.False(storage.FileExists("readme.txt"));
        Assert.Throws<FileNotFoundException>(() => storage.ReadFile("readme.txt"));
    }

    [Fact]
    public void GeneratedDecoratorChain_ForwardsAndInvalidatesCache()
    {
        var inner = new InMemoryFileStorage();
        var storage = FileStorageDecorators.Compose(
            inner,
            next => new CachingFileStorage(next),
            next => new LoggingFileStorage(next));

        storage.WriteFile("invoice.txt", "v1");
        Assert.Equal("v1", storage.ReadFile("invoice.txt"));
        Assert.Equal("v1", storage.ReadFile("invoice.txt"));

        storage.WriteFile("invoice.txt", "v2");

        Assert.Equal("v2", storage.ReadFile("invoice.txt"));
        Assert.True(storage.FileExists("invoice.txt"));

        storage.DeleteFile("invoice.txt");
        Assert.False(storage.FileExists("invoice.txt"));
    }

    [Fact]
    public void RetryDecorator_RetriesUntilOperationSucceeds()
    {
        var storage = new RetryFileStorage(new FlakyStorage(failuresBeforeSuccess: 2), maxRetries: 3, retryDelayMs: 0);

        storage.WriteFile("data.txt", "payload");

        Assert.Equal("payload", storage.ReadFile("data.txt"));
    }

    [Fact]
    public void RetryDecorator_RethrowsAfterRetriesAreExhausted()
    {
        var storage = new RetryFileStorage(new FlakyStorage(failuresBeforeSuccess: 5), maxRetries: 2, retryDelayMs: 0);

        Assert.Throws<IOException>(() => storage.WriteFile("data.txt", "payload"));
    }

    [Scenario("Public storage decorator demo runs the composed logging, caching, and retry workflow")]
    [Fact]
    public async Task StorageDecoratorDemo_Run_CoversComposedWorkflow()
    {
        await Given("a redirected console", CaptureConsole)
            .When("running the storage decorator demo", string (capture) =>
            {
                try
                {
                    StorageDecoratorDemo.Run();
                    return capture.Output();
                }
                finally
                {
                    capture.Dispose();
                }
            })
            .Then("the write path ran", output => output.Contains("Writing a file", StringComparison.Ordinal))
            .And("the cache hit path ran", output => output.Contains("[Cache] Hit", StringComparison.Ordinal))
            .And("the retry failure path was handled", output => output.Contains("Expected error:", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Logging decorator reports read failures and delete operations")]
    [Fact]
    public async Task LoggingDecorator_CoversFailureAndDeleteBranches()
    {
        await Given("logging storage with a redirected console", () =>
            {
                var capture = CaptureConsole();
                return new LoggingHarness(new LoggingFileStorage(new InMemoryFileStorage()), capture);
            })
            .When("deleting a missing file and reading it", LoggingResult (harness) =>
            {
                try
                {
                    harness.Storage.DeleteFile("missing.txt");
                    FileNotFoundException? missing = null;
                    try
                    {
                        harness.Storage.ReadFile("missing.txt");
                    }
                    catch (FileNotFoundException ex)
                    {
                        missing = ex;
                    }

                    return new LoggingResult(harness.Capture.Output(), missing);
                }
                finally
                {
                    harness.Capture.Dispose();
                }
            })
            .Then("delete was logged", result => result.Output.Contains("Deleting file: missing.txt", StringComparison.Ordinal))
            .And("read failure was logged", result => result.Output.Contains("Error reading missing.txt", StringComparison.Ordinal))
            .And("the storage error propagated", result => result.Exception is not null)
            .AssertPassed();
    }

    private static ConsoleCapture CaptureConsole() => new();

    private sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter _original = Console.Out;
        private readonly StringWriter _writer = new();

        public ConsoleCapture()
        {
            Console.SetOut(_writer);
        }

        public string Output() => _writer.ToString();

        public void Dispose()
        {
            Console.SetOut(_original);
            _writer.Dispose();
        }
    }

    private sealed record LoggingHarness(LoggingFileStorage Storage, ConsoleCapture Capture);

    private sealed record LoggingResult(string Output, FileNotFoundException? Exception);

    private sealed class FlakyStorage : IFileStorage
    {
        private readonly InMemoryFileStorage _inner = new();
        private int _remainingFailures;

        public FlakyStorage(int failuresBeforeSuccess)
        {
            _remainingFailures = failuresBeforeSuccess;
        }

        public string ReadFile(string path) => _inner.ReadFile(path);

        public void WriteFile(string path, string content)
        {
            if (_remainingFailures-- > 0)
            {
                throw new IOException("Transient write failure.");
            }

            _inner.WriteFile(path, content);
        }

        public bool FileExists(string path) => _inner.FileExists(path);

        public void DeleteFile(string path) => _inner.DeleteFile(path);
    }
}
