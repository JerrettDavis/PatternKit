# Decorator Pattern Examples

This page provides comprehensive examples of using the Decorator pattern in PatternKit.

## Table of Contents

- [Basic Examples](#basic-examples)
- [Cross-Cutting Concerns](#cross-cutting-concerns)
- [Real-World Scenarios](#real-world-scenarios)
- [Advanced Patterns](#advanced-patterns)

## Basic Examples

### Simple Input/Output Transformation

```csharp
using PatternKit.Structural.Decorator;

// Double a number and add 10
var calculator = Decorator<int, int>.Create(static x => x * 2)
    .Before(static x => x + 5)     // Input: 10 → 15
    .After(static (_, r) => r + 10) // Output: 30 → 40
    .Build();

var result = calculator.Execute(10); // 40
```

### String Processing Pipeline

```csharp
var textProcessor = Decorator<string, string>.Create(static s => s.ToUpper())
    .Before(static s => s.Trim())           // Remove whitespace
    .Before(static s => s.Replace("_", " ")) // Replace underscores
    .After(static (_, r) => $"[{r}]")        // Wrap in brackets
    .Build();

var result = textProcessor.Execute("  hello_world  "); // "[HELLO WORLD]"
```

## Cross-Cutting Concerns

### Logging Decorator

```csharp
public static class LoggingDecorators
{
    public static Decorator<TIn, TOut>.Builder WithLogging<TIn, TOut>(
        this Decorator<TIn, TOut>.Builder builder,
        ILogger logger)
    {
        return builder.Around((input, next) =>
        {
            logger.LogInformation("Executing with input: {Input}", input);
            var sw = Stopwatch.StartNew();
            
            try
            {
                var result = next(input);
                logger.LogInformation(
                    "Completed in {ElapsedMs}ms with result: {Result}",
                    sw.ElapsedMilliseconds,
                    result);
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, 
                    "Failed after {ElapsedMs}ms with input: {Input}",
                    sw.ElapsedMilliseconds,
                    input);
                throw;
            }
        });
    }
}

// Usage
var operation = Decorator<Query, Result>.Create(q => Database.Execute(q))
    .WithLogging(logger)
    .Build();
```

### Caching Decorator

```csharp
public static class CachingDecorators
{
    public static Decorator<TIn, TOut>.Builder WithCache<TIn, TOut>(
        this Decorator<TIn, TOut>.Builder builder,
        IMemoryCache cache,
        TimeSpan expiration) where TIn : notnull
    {
        return builder.Around((input, next) =>
        {
            var cacheKey = $"{typeof(TIn).Name}:{input}";
            
            if (cache.TryGetValue<TOut>(cacheKey, out var cached))
            {
                return cached;
            }
            
            var result = next(input);
            cache.Set(cacheKey, result, expiration);
            return result;
        });
    }
}

// Usage
var cachedOperation = Decorator<int, ExpensiveResult>.Create(Compute)
    .WithCache(memoryCache, TimeSpan.FromMinutes(5))
    .Build();
```

### Retry Logic

```csharp
public static class RetryDecorators
{
    public static Decorator<TIn, TOut>.Builder WithRetry<TIn, TOut>(
        this Decorator<TIn, TOut>.Builder builder,
        int maxAttempts = 3,
        TimeSpan? delay = null)
    {
        return builder.Around((input, next) =>
        {
            var attempts = 0;
            var backoffDelay = delay ?? TimeSpan.FromSeconds(1);
            Exception lastException = null;
            
            while (attempts < maxAttempts)
            {
                try
                {
                    return next(input);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;
                    
                    if (attempts < maxAttempts)
                    {
                        Thread.Sleep(backoffDelay * attempts); // Exponential backoff
                    }
                }
            }
            
            throw new InvalidOperationException(
                $"Operation failed after {maxAttempts} attempts",
                lastException);
        });
    }
}

// Usage
var resilient = Decorator<HttpRequest, HttpResponse>.Create(SendRequest)
    .WithRetry(maxAttempts: 3)
    .Build();
```

## Real-World Scenarios

### Web API Request Pipeline

```csharp
public class ApiRequestHandler
{
    private readonly Decorator<ApiRequest, ApiResponse> _pipeline;
    
    public ApiRequestHandler(
        ILogger logger,
        IMemoryCache cache,
        IAuthService authService,
        IMetrics metrics)
    {
        _pipeline = Decorator<ApiRequest, ApiResponse>
            .Create(HandleRequest)
            .Before(ValidateRequest)
            .Before(req => authService.Authorize(req))
            .Around(AddRequestLogging(logger))
            .Around(AddMetrics(metrics))
            .WithCache(cache, TimeSpan.FromMinutes(1))
            .WithRetry(maxAttempts: 2)
            .Build();
    }
    
    public ApiResponse Handle(ApiRequest request) => _pipeline.Execute(request);
    
    private static ApiRequest ValidateRequest(ApiRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Endpoint))
            throw new ValidationException("Endpoint is required");
        return req;
    }
    
    private static ApiResponse HandleRequest(ApiRequest req)
    {
        // Core business logic
        return new ApiResponse(200, "Success");
    }
    
    private static Decorator<ApiRequest, ApiResponse>.AroundTransform 
        AddRequestLogging(ILogger logger)
    {
        return (req, next) =>
        {
            logger.LogInformation("Processing {Endpoint}", req.Endpoint);
            var response = next(req);
            logger.LogInformation("Returned {Status}", response.Status);
            return response;
        };
    }
    
    private static Decorator<ApiRequest, ApiResponse>.AroundTransform 
        AddMetrics(IMetrics metrics)
    {
        return (req, next) =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var response = next(req);
                metrics.RecordSuccess(req.Endpoint, sw.Elapsed);
                return response;
            }
            catch (Exception)
            {
                metrics.RecordFailure(req.Endpoint, sw.Elapsed);
                throw;
            }
        };
    }
}
```

### Database Query with Connection Management

```csharp
public class QueryExecutor
{
    private readonly Decorator<SqlQuery, DataTable> _executor;
    
    public QueryExecutor(IDbConnectionFactory connectionFactory, ILogger logger)
    {
        _executor = Decorator<SqlQuery, DataTable>
            .Create(ExecuteQuery)
            .Around((query, next) =>
            {
                using var connection = connectionFactory.Create();
                connection.Open();
                
                using var transaction = connection.BeginTransaction();
                try
                {
                    var result = next(query);
                    transaction.Commit();
                    return result;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            })
            .Around((query, next) =>
            {
                logger.LogDebug("Executing: {Sql}", query.Sql);
                var result = next(query);
                logger.LogDebug("Returned {RowCount} rows", result.Rows.Count);
                return result;
            })
            .Build();
    }
    
    public DataTable Execute(SqlQuery query) => _executor.Execute(query);
    
    private static DataTable ExecuteQuery(SqlQuery query)
    {
        // Execute query using current connection/transaction from decorator
        var table = new DataTable();
        // ... fill table
        return table;
    }
}
```

### File Processing with Validation

```csharp
public class FileProcessor
{
    private readonly Decorator<FileInfo, ProcessedData> _processor;
    
    public FileProcessor(ILogger logger)
    {
        _processor = Decorator<FileInfo, ProcessedData>
            .Create(ProcessFile)
            .Before(ValidateFile)
            .Before(CreateBackup)
            .After((file, data) => 
            {
                data.SourceFile = file.FullName;
                data.ProcessedAt = DateTime.UtcNow;
                return data;
            })
            .Around((file, next) =>
            {
                logger.LogInformation("Processing {FileName}", file.Name);
                try
                {
                    return next(file);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process {FileName}", file.Name);
                    throw;
                }
            })
            .Build();
    }
    
    private static FileInfo ValidateFile(FileInfo file)
    {
        if (!file.Exists)
            throw new FileNotFoundException($"File not found: {file.FullName}");
        
        if (file.Length == 0)
            throw new InvalidOperationException("File is empty");
        
        if (file.Extension != ".csv")
            throw new InvalidOperationException("Only CSV files are supported");
        
        return file;
    }
    
    private static FileInfo CreateBackup(FileInfo file)
    {
        var backupPath = $"{file.FullName}.backup";
        File.Copy(file.FullName, backupPath, overwrite: true);
        return file;
    }
    
    private static ProcessedData ProcessFile(FileInfo file)
    {
        // Core processing logic
        return new ProcessedData();
    }
}
```

## Advanced Patterns

### Conditional Decoration

```csharp
public static Decorator<TIn, TOut>.Builder DecorateIf<TIn, TOut>(
    this Decorator<TIn, TOut>.Builder builder,
    bool condition,
    Func<Decorator<TIn, TOut>.Builder, Decorator<TIn, TOut>.Builder> decoration)
{
    return condition ? decoration(builder) : builder;
}

// Usage
var operation = Decorator<Request, Response>.Create(Handle)
    .DecorateIf(isDevelopment, b => b.Around(AddVerboseLogging))
    .DecorateIf(useCache, b => b.WithCache(cache, ttl))
    .Build();
```

### Composite Decorators

```csharp
public class DecoratorPipeline<TIn, TOut>
{
    private readonly List<Func<Decorator<TIn, TOut>.Builder, 
        Decorator<TIn, TOut>.Builder>> _decorators = new();
    
    public DecoratorPipeline<TIn, TOut> Add(
        Func<Decorator<TIn, TOut>.Builder, Decorator<TIn, TOut>.Builder> decorator)
    {
        _decorators.Add(decorator);
        return this;
    }
    
    public Decorator<TIn, TOut> Build(Decorator<TIn, TOut>.Component component)
    {
        var builder = Decorator<TIn, TOut>.Create(component);
        
        foreach (var decorator in _decorators)
        {
            builder = decorator(builder);
        }
        
        return builder.Build();
    }
}

// Usage
var pipeline = new DecoratorPipeline<Query, Result>()
    .Add(b => b.WithLogging(logger))
    .Add(b => b.WithCache(cache, ttl))
    .Add(b => b.WithRetry(3));

var operation = pipeline.Build(ExecuteQuery);
```

### Async Decorator Wrapper

```csharp
public static class AsyncDecoratorExtensions
{
    public static async Task<TOut> ExecuteAsync<TIn, TOut>(
        this Decorator<TIn, Task<TOut>> decorator,
        TIn input)
    {
        var task = decorator.Execute(input);
        return await task;
    }
}

// Usage
var asyncOp = Decorator<int, Task<string>>.Create(
    async x => 
    {
        await Task.Delay(100);
        return x.ToString();
    })
    .Around(async (x, next) =>
    {
        Console.WriteLine("Starting async operation");
        var result = await next(x);
        Console.WriteLine("Completed async operation");
        return result;
    })
    .Build();

var result = await asyncOp.ExecuteAsync(42);
```

### Performance Profiling

```csharp
public class PerformanceProfiler
{
    private readonly ConcurrentDictionary<string, PerformanceStats> _stats = new();
    
    public Decorator<TIn, TOut>.AroundTransform Profile<TIn, TOut>(string operationName)
    {
        return (input, next) =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = next(input);
                RecordSuccess(operationName, sw.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                RecordFailure(operationName, sw.Elapsed, ex);
                throw;
            }
        };
    }
    
    private void RecordSuccess(string operation, TimeSpan duration)
    {
        var stats = _stats.GetOrAdd(operation, _ => new PerformanceStats());
        stats.RecordSuccess(duration);
    }
    
    private void RecordFailure(string operation, TimeSpan duration, Exception ex)
    {
        var stats = _stats.GetOrAdd(operation, _ => new PerformanceStats());
        stats.RecordFailure(duration, ex);
    }
    
    public IReadOnlyDictionary<string, PerformanceStats> GetStats() => _stats;
}

// Usage
var profiler = new PerformanceProfiler();

var operation = Decorator<Query, Result>.Create(Execute)
    .Around(profiler.Profile<Query, Result>("DatabaseQuery"))
    .Build();
```

## See Also

- [Decorator Pattern Guide](decorator.md)
- [Chain of Responsibility](../../behavioral/chain/actionchain.md)
- [Strategy Pattern](../../behavioral/strategy/strategy.md)

