using PatternKit.Examples.Decorators;

namespace PatternKit.Examples.Tests.Decorators;

public sealed class StorageDecoratorExampleTests
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
