# Singleton Pattern Real-World Examples

Production-ready examples demonstrating the Singleton pattern in real-world scenarios.

---

## Example 1: Application Configuration

### The Problem

An application needs a single, globally accessible configuration object loaded from multiple sources (files, environment, remote) with validation and fail-fast behavior.

### The Solution

Use Singleton with eager creation to load and validate configuration at startup.

### The Code

```csharp
public class AppConfiguration
{
    // Core settings
    public string Environment { get; private set; } = "development";
    public string ServiceName { get; private set; } = "unknown";

    // Database settings
    public string DatabaseConnectionString { get; private set; } = "";
    public int DatabaseMaxPoolSize { get; private set; } = 100;
    public TimeSpan DatabaseCommandTimeout { get; private set; } = TimeSpan.FromSeconds(30);

    // API settings
    public string ApiBaseUrl { get; private set; } = "";
    public string ApiKey { get; private set; } = "";
    public TimeSpan ApiTimeout { get; private set; } = TimeSpan.FromSeconds(30);
    public int ApiRetryCount { get; private set; } = 3;

    // Feature flags
    public Dictionary<string, bool> Features { get; private set; } = new();

    public void LoadFromFile(string path)
    {
        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<JsonElement>(json);

        if (config.TryGetProperty("environment", out var env))
            Environment = env.GetString() ?? Environment;

        if (config.TryGetProperty("serviceName", out var name))
            ServiceName = name.GetString() ?? ServiceName;

        if (config.TryGetProperty("database", out var db))
        {
            if (db.TryGetProperty("connectionString", out var cs))
                DatabaseConnectionString = cs.GetString() ?? "";
            if (db.TryGetProperty("maxPoolSize", out var pool))
                DatabaseMaxPoolSize = pool.GetInt32();
            if (db.TryGetProperty("commandTimeoutSeconds", out var timeout))
                DatabaseCommandTimeout = TimeSpan.FromSeconds(timeout.GetInt32());
        }

        // ... load other sections
    }

    public void LoadFromEnvironment()
    {
        // Environment variables override file settings
        Environment = GetEnv("APP_ENVIRONMENT", Environment);
        ServiceName = GetEnv("APP_SERVICE_NAME", ServiceName);

        DatabaseConnectionString = GetEnv("DB_CONNECTION_STRING", DatabaseConnectionString);
        if (int.TryParse(GetEnv("DB_MAX_POOL_SIZE"), out var poolSize))
            DatabaseMaxPoolSize = poolSize;

        ApiBaseUrl = GetEnv("API_BASE_URL", ApiBaseUrl);
        ApiKey = GetEnv("API_KEY", ApiKey);

        // Parse feature flags from comma-separated list
        var features = GetEnv("FEATURE_FLAGS", "");
        foreach (var feature in features.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = feature.Split('=');
            if (parts.Length == 2 && bool.TryParse(parts[1], out var enabled))
                Features[parts[0].Trim()] = enabled;
        }
    }

    public void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(DatabaseConnectionString))
            errors.Add("Database connection string is required");

        if (string.IsNullOrEmpty(ApiBaseUrl))
            errors.Add("API base URL is required");

        if (Environment == "production" && string.IsNullOrEmpty(ApiKey))
            errors.Add("API key is required in production");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Configuration validation failed:\n{string.Join("\n", errors)}");
    }

    private static string GetEnv(string name, string defaultValue = "")
        => System.Environment.GetEnvironmentVariable(name) ?? defaultValue;
}

// Singleton wrapper
public static class Config
{
    private static readonly Singleton<AppConfiguration> _instance =
        Singleton<AppConfiguration>
            .Create(static () => new AppConfiguration())
            .Init(static c => c.LoadFromFile("appsettings.json"))
            .Init(static c => c.LoadFromFile($"appsettings.{c.Environment}.json"))
            .Init(static c => c.LoadFromEnvironment())
            .Init(static c => c.Validate())
            .Eager()  // Fail fast on invalid configuration
            .Build();

    public static AppConfiguration Instance => _instance.Instance;

    // Convenience properties
    public static string Environment => Instance.Environment;
    public static bool IsProduction => Environment == "production";
    public static bool IsFeatureEnabled(string feature)
        => Instance.Features.TryGetValue(feature, out var enabled) && enabled;
}

// Usage
if (Config.IsProduction)
{
    logger.LogInformation("Running in production mode");
}

if (Config.IsFeatureEnabled("new-checkout"))
{
    // Use new checkout
}

var connection = new SqlConnection(Config.Instance.DatabaseConnectionString);
```

### Why This Pattern

- **Fail fast**: Invalid configuration detected at startup
- **Single source of truth**: One instance accessed everywhere
- **Layered loading**: File → environment → validation
- **Convenient access**: Static properties for common values

---

## Example 2: Logging Infrastructure

### The Problem

A distributed system needs a centralized logging factory that configures structured logging, correlation IDs, and multiple sinks (console, file, external service).

### The Solution

Use Singleton to create the logger factory once with all configuration.

### The Code

```csharp
public class LoggingInfrastructure
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly AsyncLocal<string> _correlationId = new();

    public LoggingInfrastructure()
    {
        _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(GetMinimumLevel())
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
                })
                .AddProvider(new StructuredFileLoggerProvider(
                    "logs/app.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30));

            // Add external logging in production
            if (Environment.GetEnvironmentVariable("APP_ENV") == "production")
            {
                builder.AddProvider(new ExternalLoggerProvider(
                    endpoint: Environment.GetEnvironmentVariable("LOG_ENDPOINT")!,
                    apiKey: Environment.GetEnvironmentVariable("LOG_API_KEY")!));
            }
        });
    }

    public ILogger<T> GetLogger<T>() => _loggerFactory.CreateLogger<T>();

    public ILogger GetLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);

    public string CorrelationId
    {
        get => _correlationId.Value ?? Guid.NewGuid().ToString();
        set => _correlationId.Value = value;
    }

    public IDisposable BeginScope(string correlationId)
    {
        var previous = _correlationId.Value;
        _correlationId.Value = correlationId;
        return new CorrelationScope(this, previous);
    }

    private static LogLevel GetMinimumLevel()
    {
        var level = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "Information";
        return Enum.TryParse<LogLevel>(level, true, out var result)
            ? result
            : LogLevel.Information;
    }

    private class CorrelationScope : IDisposable
    {
        private readonly LoggingInfrastructure _logging;
        private readonly string? _previous;

        public CorrelationScope(LoggingInfrastructure logging, string? previous)
        {
            _logging = logging;
            _previous = previous;
        }

        public void Dispose() => _logging._correlationId.Value = _previous;
    }
}

// Singleton wrapper
public static class Log
{
    private static readonly Singleton<LoggingInfrastructure> _instance =
        Singleton<LoggingInfrastructure>
            .Create(static () => new LoggingInfrastructure())
            .Eager()
            .Build();

    private static LoggingInfrastructure Infrastructure => _instance.Instance;

    public static ILogger<T> For<T>() => Infrastructure.GetLogger<T>();

    public static ILogger For(string category) => Infrastructure.GetLogger(category);

    public static string CorrelationId
    {
        get => Infrastructure.CorrelationId;
        set => Infrastructure.CorrelationId = value;
    }

    public static IDisposable BeginCorrelation(string correlationId)
        => Infrastructure.BeginScope(correlationId);
}

// Usage in services
public class OrderService
{
    private readonly ILogger<OrderService> _logger = Log.For<OrderService>();

    public async Task<Order> CreateOrderAsync(OrderRequest request)
    {
        using (Log.BeginCorrelation(request.CorrelationId ?? Guid.NewGuid().ToString()))
        {
            _logger.LogInformation("Creating order for customer {CustomerId}", request.CustomerId);

            try
            {
                var order = await ProcessOrderAsync(request);
                _logger.LogInformation("Order {OrderId} created successfully", order.Id);
                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create order for customer {CustomerId}",
                    request.CustomerId);
                throw;
            }
        }
    }
}
```

### Why This Pattern

- **Consistent logging**: Same configuration everywhere
- **Correlation tracking**: Request correlation via AsyncLocal
- **Early initialization**: Available before DI container
- **No allocation per logger**: Factory caches loggers

---

## Example 3: Connection Pool Manager

### The Problem

A high-throughput service needs to manage database connection pools with warm-up, health checks, and graceful shutdown.

### The Solution

Use Singleton to manage the connection pool lifecycle with proper initialization.

### The Code

```csharp
public class ConnectionPoolManager : IAsyncDisposable
{
    private readonly Dictionary<string, ConnectionPool> _pools = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Timer _healthCheckTimer;

    public ConnectionPoolManager()
    {
        _healthCheckTimer = new Timer(
            _ => RunHealthChecks(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    public void Initialize()
    {
        // Create default pools from configuration
        var config = Config.Instance;

        AddPool("primary", new ConnectionPoolOptions
        {
            ConnectionString = config.DatabaseConnectionString,
            MinSize = 10,
            MaxSize = config.DatabaseMaxPoolSize,
            IdleTimeout = TimeSpan.FromMinutes(5)
        });

        if (!string.IsNullOrEmpty(config.ReadReplicaConnectionString))
        {
            AddPool("readonly", new ConnectionPoolOptions
            {
                ConnectionString = config.ReadReplicaConnectionString,
                MinSize = 5,
                MaxSize = 50,
                IdleTimeout = TimeSpan.FromMinutes(5)
            });
        }
    }

    public void WarmUp()
    {
        foreach (var (name, pool) in _pools)
        {
            try
            {
                pool.WarmUp();
                Log.For<ConnectionPoolManager>()
                    .LogInformation("Warmed up pool {PoolName} with {Count} connections",
                        name, pool.MinSize);
            }
            catch (Exception ex)
            {
                Log.For<ConnectionPoolManager>()
                    .LogError(ex, "Failed to warm up pool {PoolName}", name);
                throw;
            }
        }
    }

    public IDbConnection GetConnection(string poolName = "primary")
    {
        if (!_pools.TryGetValue(poolName, out var pool))
            throw new ArgumentException($"Unknown pool: {poolName}");

        return pool.Acquire();
    }

    public async Task<IDbConnection> GetConnectionAsync(
        string poolName = "primary",
        CancellationToken ct = default)
    {
        if (!_pools.TryGetValue(poolName, out var pool))
            throw new ArgumentException($"Unknown pool: {poolName}");

        return await pool.AcquireAsync(ct);
    }

    private void AddPool(string name, ConnectionPoolOptions options)
    {
        var pool = new ConnectionPool(options);
        _pools[name] = pool;
    }

    private void RunHealthChecks()
    {
        foreach (var (name, pool) in _pools)
        {
            try
            {
                var healthy = pool.CheckHealth();
                if (!healthy)
                {
                    Log.For<ConnectionPoolManager>()
                        .LogWarning("Pool {PoolName} health check failed", name);
                }
            }
            catch (Exception ex)
            {
                Log.For<ConnectionPoolManager>()
                    .LogError(ex, "Error during health check for pool {PoolName}", name);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdownCts.Cancel();
        await _healthCheckTimer.DisposeAsync();

        foreach (var pool in _pools.Values)
        {
            await pool.DisposeAsync();
        }
    }
}

// Singleton wrapper
public static class DbPool
{
    private static readonly Singleton<ConnectionPoolManager> _instance =
        Singleton<ConnectionPoolManager>
            .Create(static () => new ConnectionPoolManager())
            .Init(static m => m.Initialize())
            .Init(static m => m.WarmUp())
            .Eager()
            .Build();

    public static ConnectionPoolManager Instance => _instance.Instance;

    public static IDbConnection Primary => Instance.GetConnection("primary");
    public static IDbConnection ReadOnly => Instance.GetConnection("readonly");

    public static Task<IDbConnection> PrimaryAsync(CancellationToken ct = default)
        => Instance.GetConnectionAsync("primary", ct);

    public static Task<IDbConnection> ReadOnlyAsync(CancellationToken ct = default)
        => Instance.GetConnectionAsync("readonly", ct);
}

// Usage
using var connection = DbPool.Primary;
var result = await connection.QueryAsync<Order>("SELECT * FROM orders WHERE id = @Id", new { Id = orderId });

// Or async acquisition
using var readConnection = await DbPool.ReadOnlyAsync(cancellationToken);
var reports = await readConnection.QueryAsync<Report>("SELECT * FROM reports");
```

### Why This Pattern

- **Centralized management**: All pools in one place
- **Warm-up on startup**: Connections ready before requests
- **Health monitoring**: Background health checks
- **Graceful shutdown**: Proper cleanup on termination

---

## Example 4: Distributed Cache Client

### The Problem

A microservices system needs a shared cache client (Redis) with connection management, retry policies, and circuit breaker patterns.

### The Solution

Use Singleton to manage the cache client with resilience patterns built in.

### The Code

```csharp
public class CacheClient
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ResiliencePolicy _policy;
    private readonly ILogger _logger;

    public CacheClient(string connectionString)
    {
        _logger = Log.For<CacheClient>();

        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.ConnectTimeout = 5000;

        _redis = ConnectionMultiplexer.Connect(options);
        _database = _redis.GetDatabase();

        _policy = new ResiliencePolicy(
            retryCount: 3,
            circuitBreakerThreshold: 5,
            circuitBreakerDuration: TimeSpan.FromSeconds(30));

        _redis.ConnectionFailed += (_, e) =>
            _logger.LogWarning("Redis connection failed: {Reason}", e.FailureType);

        _redis.ConnectionRestored += (_, e) =>
            _logger.LogInformation("Redis connection restored");
    }

    public void WarmUp()
    {
        // Test connection
        _database.Ping();
        _logger.LogInformation("Cache connection verified");
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        return await _policy.ExecuteAsync(async () =>
        {
            var value = await _database.StringGetAsync(key);
            if (value.IsNullOrEmpty) return default;
            return JsonSerializer.Deserialize<T>(value!);
        }, ct);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        await _policy.ExecuteAsync(async () =>
        {
            var json = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, json, expiry);
        }, ct);
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory();
        await SetAsync(key, value, expiry, ct);
        return value;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _policy.ExecuteAsync(async () =>
        {
            await _database.KeyDeleteAsync(key);
        }, ct);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return await _policy.ExecuteAsync(async () =>
        {
            return await _database.KeyExistsAsync(key);
        }, ct);
    }
}

// Singleton wrapper
public static class Cache
{
    private static readonly Singleton<CacheClient> _instance =
        Singleton<CacheClient>
            .Create(() => new CacheClient(Config.Instance.RedisConnectionString))
            .Init(static c => c.WarmUp())
            .Eager()
            .Build();

    public static CacheClient Instance => _instance.Instance;

    public static Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        => Instance.GetAsync<T>(key, ct);

    public static Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        => Instance.SetAsync(key, value, expiry, ct);

    public static Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiry, CancellationToken ct = default)
        => Instance.GetOrSetAsync(key, factory, expiry, ct);
}

// Usage
// Simple get/set
await Cache.SetAsync("user:123", user, TimeSpan.FromMinutes(30));
var cachedUser = await Cache.GetAsync<User>("user:123");

// Get-or-set pattern
var products = await Cache.GetOrSetAsync(
    "products:featured",
    async () => await db.GetFeaturedProductsAsync(),
    TimeSpan.FromMinutes(5));
```

### Why This Pattern

- **Connection sharing**: One multiplexer for all cache operations
- **Built-in resilience**: Retry and circuit breaker included
- **Connection monitoring**: Automatic reconnection handling
- **Convenient API**: Static methods for common operations

---

## Key Takeaways

1. **Eager for fail-fast**: Use `.Eager()` when startup errors should stop the app
2. **Init for setup**: Chain initialization steps in order
3. **Static lambdas**: Avoid closures with `static () =>`
4. **Wrapper pattern**: Static class provides convenient access
5. **Testing awareness**: Consider interfaces for mockability

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
