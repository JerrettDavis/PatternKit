using PatternKit.Generators.Decorator;

namespace PatternKit.Examples.Decorators;

/// <summary>
/// Example demonstrating the Decorator pattern with a storage interface.
/// Shows how to use generated decorator base classes to implement caching,
/// logging, and retry logic in a composable way.
/// </summary>
[GenerateDecorator]
public interface IFileStorage
{
    string ReadFile(string path);
    void WriteFile(string path, string content);
    bool FileExists(string path);
    void DeleteFile(string path);
}

/// <summary>
/// Simple in-memory file storage implementation.
/// </summary>
public class InMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<string, string> _files = new();

    public string ReadFile(string path)
    {
        if (!_files.TryGetValue(path, out var content))
            throw new FileNotFoundException($"File not found: {path}");
        return content;
    }

    public void WriteFile(string path, string content)
    {
        _files[path] = content;
    }

    public bool FileExists(string path)
    {
        return _files.ContainsKey(path);
    }

    public void DeleteFile(string path)
    {
        _files.Remove(path);
    }
}

/// <summary>
/// Caching decorator that caches file reads.
/// </summary>
public class CachingFileStorage : FileStorageDecoratorBase
{
    private readonly Dictionary<string, string> _cache = new();

    public CachingFileStorage(IFileStorage inner) : base(inner) { }

    public override string ReadFile(string path)
    {
        if (_cache.TryGetValue(path, out var cached))
        {
            Console.WriteLine($"[Cache] Hit for {path}");
            return cached;
        }

        Console.WriteLine($"[Cache] Miss for {path}");
        var content = base.ReadFile(path);
        _cache[path] = content;
        return content;
    }

    public override void WriteFile(string path, string content)
    {
        _cache.Remove(path); // Invalidate cache
        base.WriteFile(path, content);
    }

    public override void DeleteFile(string path)
    {
        _cache.Remove(path); // Invalidate cache
        base.DeleteFile(path);
    }
}

/// <summary>
/// Logging decorator that logs all operations.
/// </summary>
public class LoggingFileStorage : FileStorageDecoratorBase
{
    public LoggingFileStorage(IFileStorage inner) : base(inner) { }

    public override string ReadFile(string path)
    {
        Console.WriteLine($"[Log] Reading file: {path}");
        try
        {
            var result = base.ReadFile(path);
            Console.WriteLine($"[Log] Successfully read {result.Length} characters from {path}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Log] Error reading {path}: {ex.Message}");
            throw;
        }
    }

    public override void WriteFile(string path, string content)
    {
        Console.WriteLine($"[Log] Writing {content.Length} characters to {path}");
        base.WriteFile(path, content);
        Console.WriteLine($"[Log] Successfully wrote to {path}");
    }

    public override bool FileExists(string path)
    {
        Console.WriteLine($"[Log] Checking existence of {path}");
        var exists = base.FileExists(path);
        Console.WriteLine($"[Log] File {path} exists: {exists}");
        return exists;
    }

    public override void DeleteFile(string path)
    {
        Console.WriteLine($"[Log] Deleting file: {path}");
        base.DeleteFile(path);
        Console.WriteLine($"[Log] Successfully deleted {path}");
    }
}

/// <summary>
/// Retry decorator that retries failed operations.
/// </summary>
public class RetryFileStorage : FileStorageDecoratorBase
{
    private readonly int _maxRetries;
    private readonly int _retryDelayMs;

    public RetryFileStorage(IFileStorage inner, int maxRetries = 3, int retryDelayMs = 100) 
        : base(inner)
    {
        _maxRetries = maxRetries;
        _retryDelayMs = retryDelayMs;
    }

    public override string ReadFile(string path)
    {
        return RetryOperation(() => base.ReadFile(path), $"ReadFile({path})");
    }

    public override void WriteFile(string path, string content)
    {
        RetryOperation(() => { base.WriteFile(path, content); return true; }, $"WriteFile({path})");
    }

    public override void DeleteFile(string path)
    {
        RetryOperation(() => { base.DeleteFile(path); return true; }, $"DeleteFile({path})");
    }

    private T RetryOperation<T>(Func<T> operation, string operationName)
    {
        for (int i = 0; i < _maxRetries; i++)
        {
            try
            {
                return operation();
            }
            catch (Exception ex) when (i < _maxRetries - 1)
            {
                Console.WriteLine($"[Retry] Attempt {i + 1}/{_maxRetries} failed for {operationName}: {ex.Message}");
                Thread.Sleep(_retryDelayMs);
            }
        }
        // Last attempt - let exception propagate
        return operation();
    }
}

/// <summary>
/// Demonstrates using the decorator pattern with the generated base class.
/// </summary>
public static class StorageDecoratorDemo
{
    public static void Run()
    {
        Console.WriteLine("=== Decorator Pattern Demo ===\n");

        // Base storage
        var baseStorage = new InMemoryFileStorage();

        // Compose decorators: Logging -> Caching -> Retry -> Base
        // Order matters: outermost decorator is applied first
        var storage = FileStorageDecorators.Compose(
            baseStorage,
            inner => new LoggingFileStorage(inner),
            inner => new CachingFileStorage(inner),
            inner => new RetryFileStorage(inner)
        );

        Console.WriteLine("--- Writing a file ---");
        storage.WriteFile("test.txt", "Hello, Decorators!");

        Console.WriteLine("\n--- Reading the file (cache miss) ---");
        var content1 = storage.ReadFile("test.txt");
        Console.WriteLine($"Content: {content1}");

        Console.WriteLine("\n--- Reading the file again (cache hit) ---");
        var content2 = storage.ReadFile("test.txt");
        Console.WriteLine($"Content: {content2}");

        Console.WriteLine("\n--- Checking file existence ---");
        var exists = storage.FileExists("test.txt");
        Console.WriteLine($"Exists: {exists}");

        Console.WriteLine("\n--- Deleting the file ---");
        storage.DeleteFile("test.txt");

        Console.WriteLine("\n--- Trying to read deleted file (will fail with retries) ---");
        try
        {
            storage.ReadFile("test.txt");
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"Expected error: {ex.Message}");
        }

        Console.WriteLine("\n=== Demo Complete ===");
    }
}
