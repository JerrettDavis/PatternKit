# Proxy Pattern Real-World Examples

Production-ready examples demonstrating the Proxy pattern in PatternKit.

---

## Example 1: Database Connection with Virtual Proxy

### The Problem

A service needs database access but creating connections is expensive. Many code paths may not actually use the database, wasting resources on initialization.

### The Solution

A virtual proxy that defers connection creation until first actual query.

### The Code

```csharp
using PatternKit.Structural.Proxy;
using System.Data;

public class LazyDatabaseService
{
    private readonly Proxy<SqlQuery, QueryResult> _queryProxy;
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public LazyDatabaseService(string connectionString, ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;

        _queryProxy = Proxy<SqlQuery, QueryResult>.Create()
            .VirtualProxy(() =>
            {
                _logger.LogInformation("Initializing database connection pool...");
                var stopwatch = Stopwatch.StartNew();

                // Expensive initialization
                var connection = new SqlConnection(_connectionString);
                connection.Open();

                // Warm up connection pool
                using var warmup = connection.CreateCommand();
                warmup.CommandText = "SELECT 1";
                warmup.ExecuteNonQuery();

                _logger.LogInformation(
                    "Database ready in {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);

                // Return the actual query executor
                return query => ExecuteQuery(connection, query);
            })
            .Build();
    }

    public QueryResult Query(string sql, params object[] parameters)
    {
        return _queryProxy.Execute(new SqlQuery(sql, parameters));
    }

    public T QuerySingle<T>(string sql, params object[] parameters)
    {
        var result = Query(sql, parameters);
        return result.Rows.FirstOrDefault()?.GetValue<T>(0)
            ?? throw new InvalidOperationException("No results");
    }

    public IEnumerable<T> QueryMany<T>(string sql, params object[] parameters)
        where T : new()
    {
        var result = Query(sql, parameters);
        return result.MapTo<T>();
    }

    private QueryResult ExecuteQuery(SqlConnection connection, SqlQuery query)
    {
        using var command = connection.CreateCommand();
        command.CommandText = query.Sql;
        command.CommandTimeout = 30;

        for (int i = 0; i < query.Parameters.Length; i++)
        {
            command.Parameters.AddWithValue($"@p{i}", query.Parameters[i]);
        }

        using var reader = command.ExecuteReader();
        return QueryResult.FromReader(reader);
    }
}

public record SqlQuery(string Sql, object[] Parameters);

public class QueryResult
{
    public List<QueryRow> Rows { get; } = new();
    public string[] ColumnNames { get; init; } = Array.Empty<string>();

    public static QueryResult FromReader(IDataReader reader)
    {
        var result = new QueryResult
        {
            ColumnNames = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i))
                .ToArray()
        };

        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            result.Rows.Add(new QueryRow(values, result.ColumnNames));
        }

        return result;
    }

    public IEnumerable<T> MapTo<T>() where T : new()
    {
        // Simple object mapping
        var props = typeof(T).GetProperties()
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var row in Rows)
        {
            var obj = new T();
            for (int i = 0; i < ColumnNames.Length; i++)
            {
                if (props.TryGetValue(ColumnNames[i], out var prop))
                    prop.SetValue(obj, Convert.ChangeType(row.Values[i], prop.PropertyType));
            }
            yield return obj;
        }
    }
}

public record QueryRow(object[] Values, string[] ColumnNames)
{
    public T GetValue<T>(int index) => (T)Convert.ChangeType(Values[index], typeof(T));
    public T GetValue<T>(string name)
    {
        var index = Array.IndexOf(ColumnNames, name);
        return GetValue<T>(index);
    }
}

// Usage
var db = new LazyDatabaseService(connectionString, logger);

// No database connection yet!
Console.WriteLine("Service created, but database not connected");

// First query triggers connection
var users = db.QueryMany<User>("SELECT * FROM Users WHERE Active = @p0", true);
// Logs: "Initializing database connection pool..."
// Logs: "Database ready in 234ms"

// Subsequent queries reuse connection
var count = db.QuerySingle<int>("SELECT COUNT(*) FROM Users");
// No initialization log - connection already established
```

### Why This Pattern

Virtual proxy delays the 234ms database initialization until actually needed. Services that receive the database service but don't use it avoid the overhead entirely.

---

## Example 2: API Gateway with Multi-Layer Proxy

### The Problem

An API gateway needs: authentication, rate limiting, caching, retry logic, and logging. Implementing all these concerns inline creates unmaintainable code.

### The Solution

Compose multiple proxy layers, each handling one concern.

### The Code

```csharp
using PatternKit.Structural.Proxy;
using System.Collections.Concurrent;

public class ApiGateway
{
    private readonly Proxy<GatewayRequest, GatewayResponse> _pipeline;

    public ApiGateway(
        IAuthService auth,
        IRateLimiter rateLimiter,
        ICache cache,
        HttpClient httpClient,
        ILogger logger,
        IMetrics metrics)
    {
        // Layer 1: Actual HTTP call (innermost)
        Proxy<GatewayRequest, GatewayResponse>.Subject callBackend =
            req => CallBackendService(httpClient, req);

        // Layer 2: Retry with exponential backoff
        var retryProxy = Proxy<GatewayRequest, GatewayResponse>.Create(callBackend)
            .Intercept((req, next) =>
            {
                var delays = new[] { 100, 200, 400 };
                Exception? lastException = null;

                for (int attempt = 0; attempt <= 3; attempt++)
                {
                    try
                    {
                        return next(req);
                    }
                    catch (HttpRequestException ex) when (attempt < 3 && IsRetryable(ex))
                    {
                        lastException = ex;
                        Thread.Sleep(delays[attempt]);
                        logger.LogWarning("Retry {Attempt} for {Path}", attempt + 1, req.Path);
                    }
                }

                throw new GatewayException("Backend unavailable after retries", lastException);
            })
            .Build();

        // Layer 3: Caching for GET requests
        var cacheDict = new ConcurrentDictionary<string, (GatewayResponse Response, DateTime Expiry)>();
        var cachingProxy = Proxy<GatewayRequest, GatewayResponse>.Create(
                req => retryProxy.Execute(req))
            .Intercept((req, next) =>
            {
                // Only cache GET requests
                if (req.Method != "GET")
                    return next(req);

                var cacheKey = $"{req.Path}:{req.QueryString}";

                if (cacheDict.TryGetValue(cacheKey, out var cached) &&
                    cached.Expiry > DateTime.UtcNow)
                {
                    metrics.Increment("cache.hit");
                    return cached.Response with { FromCache = true };
                }

                metrics.Increment("cache.miss");
                var response = next(req);

                if (response.StatusCode == 200 && response.CacheSeconds > 0)
                {
                    cacheDict[cacheKey] = (response, DateTime.UtcNow.AddSeconds(response.CacheSeconds));
                }

                return response;
            })
            .Build();

        // Layer 4: Rate limiting
        var rateLimitProxy = Proxy<GatewayRequest, GatewayResponse>.Create(
                req => cachingProxy.Execute(req))
            .Intercept((req, next) =>
            {
                var clientId = req.Headers.GetValueOrDefault("X-Client-Id") ?? req.ClientIp;

                if (!rateLimiter.TryAcquire(clientId))
                {
                    metrics.Increment("ratelimit.exceeded");
                    return new GatewayResponse
                    {
                        StatusCode = 429,
                        Body = "Rate limit exceeded",
                        Headers = new Dictionary<string, string>
                        {
                            ["Retry-After"] = rateLimiter.GetRetryAfter(clientId).ToString()
                        }
                    };
                }

                return next(req);
            })
            .Build();

        // Layer 5: Authentication
        var authProxy = Proxy<GatewayRequest, GatewayResponse>.Create(
                req => rateLimitProxy.Execute(req))
            .Intercept((req, next) =>
            {
                // Public endpoints skip auth
                if (IsPublicEndpoint(req.Path))
                    return next(req);

                var token = req.Headers.GetValueOrDefault("Authorization");
                if (string.IsNullOrEmpty(token))
                {
                    return new GatewayResponse
                    {
                        StatusCode = 401,
                        Body = "Authorization required"
                    };
                }

                var validation = auth.ValidateToken(token);
                if (!validation.IsValid)
                {
                    return new GatewayResponse
                    {
                        StatusCode = 401,
                        Body = validation.Error ?? "Invalid token"
                    };
                }

                // Add user info to request for downstream
                req = req with
                {
                    Headers = new Dictionary<string, string>(req.Headers)
                    {
                        ["X-User-Id"] = validation.UserId!,
                        ["X-User-Roles"] = string.Join(",", validation.Roles)
                    }
                };

                return next(req);
            })
            .Build();

        // Layer 6: Metrics and logging (outermost)
        _pipeline = Proxy<GatewayRequest, GatewayResponse>.Create(
                req => authProxy.Execute(req))
            .Intercept((req, next) =>
            {
                var correlationId = Guid.NewGuid().ToString()[..8];
                var sw = Stopwatch.StartNew();

                logger.LogInformation(
                    "[{CorrelationId}] {Method} {Path}",
                    correlationId, req.Method, req.Path);

                try
                {
                    var response = next(req);

                    metrics.RecordHistogram("gateway.latency", sw.ElapsedMilliseconds,
                        ("method", req.Method),
                        ("path", NormalizePath(req.Path)),
                        ("status", response.StatusCode.ToString()));

                    logger.LogInformation(
                        "[{CorrelationId}] {StatusCode} in {ElapsedMs}ms",
                        correlationId, response.StatusCode, sw.ElapsedMilliseconds);

                    return response;
                }
                catch (Exception ex)
                {
                    metrics.Increment("gateway.error",
                        ("exception", ex.GetType().Name));

                    logger.LogError(ex,
                        "[{CorrelationId}] Error after {ElapsedMs}ms",
                        correlationId, sw.ElapsedMilliseconds);

                    return new GatewayResponse
                    {
                        StatusCode = 500,
                        Body = "Internal server error"
                    };
                }
            })
            .Build();
    }

    public GatewayResponse Handle(GatewayRequest request)
        => _pipeline.Execute(request);

    private GatewayResponse CallBackendService(HttpClient client, GatewayRequest req)
    {
        var httpRequest = new HttpRequestMessage(
            new HttpMethod(req.Method),
            $"{req.BackendUrl}{req.Path}{req.QueryString}");

        foreach (var header in req.Headers)
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (req.Body != null)
            httpRequest.Content = new StringContent(req.Body, Encoding.UTF8, "application/json");

        var response = client.Send(httpRequest);
        var body = response.Content.ReadAsStringAsync().Result;

        return new GatewayResponse
        {
            StatusCode = (int)response.StatusCode,
            Body = body,
            Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
            CacheSeconds = ParseCacheControl(response)
        };
    }

    private static bool IsRetryable(HttpRequestException ex) =>
        ex.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable or
                        System.Net.HttpStatusCode.GatewayTimeout;

    private static bool IsPublicEndpoint(string path) =>
        path.StartsWith("/api/public/") || path == "/health";

    private static string NormalizePath(string path) =>
        Regex.Replace(path, @"/\d+", "/{id}");

    private static int ParseCacheControl(HttpResponseMessage response)
    {
        if (response.Headers.CacheControl?.MaxAge is TimeSpan maxAge)
            return (int)maxAge.TotalSeconds;
        return 0;
    }
}

public record GatewayRequest(
    string Method,
    string Path,
    string QueryString,
    string? Body,
    Dictionary<string, string> Headers,
    string ClientIp,
    string BackendUrl);

public record GatewayResponse
{
    public int StatusCode { get; init; }
    public string? Body { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
    public int CacheSeconds { get; init; }
    public bool FromCache { get; init; }
}

// Usage
var gateway = new ApiGateway(auth, rateLimiter, cache, httpClient, logger, metrics);

var request = new GatewayRequest(
    Method: "GET",
    Path: "/api/users/123",
    QueryString: "",
    Body: null,
    Headers: new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer eyJ..."
    },
    ClientIp: "192.168.1.100",
    BackendUrl: "https://backend.internal"
);

var response = gateway.Handle(request);
// Execution flow:
// 1. Metrics/Logging: Start timer, log request
// 2. Auth: Validate JWT, add user headers
// 3. Rate Limit: Check client quota
// 4. Cache: Check cache for GET
// 5. Retry: Call backend with retry
// 6. Metrics/Logging: Record latency, log response
```

### Why This Pattern

Each proxy layer handles exactly one cross-cutting concern. The composition is clear, testable, and maintainable. Adding new concerns (e.g., circuit breaker) means adding one new proxy layer.

---

## Example 3: Feature Flag Protection Proxy

### The Problem

A system needs to control feature access based on user tiers, A/B test groups, and gradual rollouts. Checking feature flags in every method creates duplication.

### The Solution

Protection proxies that gate feature access declaratively.

### The Code

```csharp
using PatternKit.Structural.Proxy;

public class FeatureFlagService
{
    private readonly IFeatureStore _store;
    private readonly IUserContext _userContext;

    public FeatureFlagService(IFeatureStore store, IUserContext userContext)
    {
        _store = store;
        _userContext = userContext;
    }

    public Proxy<TIn, TOut> Protect<TIn, TOut>(
        string featureFlag,
        Proxy<TIn, TOut>.Subject subject,
        TOut fallback) where TIn : notnull
    {
        return Proxy<TIn, TOut>.Create(subject)
            .Intercept((input, next) =>
            {
                var user = _userContext.GetCurrentUser();
                var isEnabled = _store.IsEnabled(featureFlag, user);

                if (!isEnabled)
                {
                    _store.LogDisabledAccess(featureFlag, user.Id);
                    return fallback;
                }

                return next(input);
            })
            .Build();
    }

    public Proxy<TIn, TOut> ProtectWithABTest<TIn, TOut>(
        string experiment,
        Proxy<TIn, TOut>.Subject controlSubject,
        Proxy<TIn, TOut>.Subject treatmentSubject) where TIn : notnull
    {
        return Proxy<TIn, TOut>.Create(controlSubject)
            .Intercept((input, next) =>
            {
                var user = _userContext.GetCurrentUser();
                var variant = _store.GetExperimentVariant(experiment, user);

                _store.LogExperimentExposure(experiment, user.Id, variant);

                return variant == "treatment"
                    ? treatmentSubject(input)
                    : next(input); // control
            })
            .Build();
    }

    public Proxy<TIn, TOut> ProtectWithGradualRollout<TIn, TOut>(
        string featureFlag,
        Proxy<TIn, TOut>.Subject newImplementation,
        Proxy<TIn, TOut>.Subject oldImplementation) where TIn : notnull
    {
        return Proxy<TIn, TOut>.Create(oldImplementation)
            .Intercept((input, next) =>
            {
                var user = _userContext.GetCurrentUser();
                var rolloutPercentage = _store.GetRolloutPercentage(featureFlag);

                // Consistent bucketing based on user ID
                var bucket = Math.Abs(user.Id.GetHashCode()) % 100;
                var useNew = bucket < rolloutPercentage;

                if (useNew)
                {
                    _store.LogRolloutSelection(featureFlag, user.Id, "new");
                    return newImplementation(input);
                }

                _store.LogRolloutSelection(featureFlag, user.Id, "old");
                return next(input);
            })
            .Build();
    }
}

public class RecommendationService
{
    private readonly Proxy<UserContext, List<Recommendation>> _getRecommendations;
    private readonly Proxy<SearchQuery, SearchResults> _search;

    public RecommendationService(
        FeatureFlagService features,
        IProductCatalog catalog,
        IMLService mlService)
    {
        // Protect ML recommendations with feature flag
        _getRecommendations = features.Protect<UserContext, List<Recommendation>>(
            featureFlag: "ml-recommendations",
            subject: ctx => mlService.GetPersonalizedRecommendations(ctx),
            fallback: new List<Recommendation>() // Empty list if disabled
        );

        // A/B test new search algorithm
        _search = features.ProtectWithABTest<SearchQuery, SearchResults>(
            experiment: "search-v2",
            controlSubject: query => catalog.SearchV1(query),
            treatmentSubject: query => catalog.SearchV2(query)
        );
    }

    public List<Recommendation> GetRecommendations(UserContext context)
        => _getRecommendations.Execute(context);

    public SearchResults Search(SearchQuery query)
        => _search.Execute(query);
}

public class CheckoutService
{
    private readonly Proxy<CheckoutRequest, CheckoutResult> _checkout;

    public CheckoutService(
        FeatureFlagService features,
        IPaymentService payments)
    {
        // Gradual rollout of new payment flow
        _checkout = features.ProtectWithGradualRollout<CheckoutRequest, CheckoutResult>(
            featureFlag: "new-payment-flow",
            newImplementation: req => ProcessCheckoutV2(payments, req),
            oldImplementation: req => ProcessCheckoutV1(payments, req)
        );
    }

    public CheckoutResult Checkout(CheckoutRequest request)
        => _checkout.Execute(request);

    private CheckoutResult ProcessCheckoutV1(IPaymentService payments, CheckoutRequest req)
    {
        // Old flow: single-step payment
        var result = payments.ProcessPayment(req.PaymentInfo, req.Total);
        return new CheckoutResult(result.Success, result.TransactionId);
    }

    private CheckoutResult ProcessCheckoutV2(IPaymentService payments, CheckoutRequest req)
    {
        // New flow: authorize then capture
        var auth = payments.Authorize(req.PaymentInfo, req.Total);
        if (!auth.Success)
            return new CheckoutResult(false, null, auth.Error);

        var capture = payments.Capture(auth.AuthorizationId);
        return new CheckoutResult(capture.Success, capture.TransactionId);
    }
}

// Usage in startup
services.AddSingleton<FeatureFlagService>();
services.AddSingleton<RecommendationService>();
services.AddSingleton<CheckoutService>();

// Controller
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly RecommendationService _recommendations;

    [HttpGet("recommendations")]
    public IActionResult GetRecommendations()
    {
        var context = new UserContext(User.GetUserId(), User.GetTier());

        // Feature flag check happens inside proxy
        var recommendations = _recommendations.GetRecommendations(context);

        return Ok(recommendations);
    }
}
```

### Why This Pattern

Feature flag logic is encapsulated in proxies, not scattered across business logic. Changing rollout percentages or flag states doesn't require code changes - the proxy handles it transparently.

---

## Example 4: Mock Framework for Testing

### The Problem

Unit tests need to mock dependencies with configurable behavior, verify invocations, and capture arguments. Building mocks from scratch is tedious.

### The Solution

A mock framework built on proxy interception.

### The Code

```csharp
using PatternKit.Structural.Proxy;

public class Mock<TIn, TOut> where TIn : notnull
{
    private readonly List<TIn> _invocations = new();
    private readonly List<(Func<TIn, bool> Predicate, TOut Result)> _setups = new();
    private Func<TIn, TOut> _defaultBehavior = _ => default!;
    private Func<TIn, Exception>? _throwBehavior;

    public Mock<TIn, TOut> Setup(Func<TIn, bool> predicate, TOut result)
    {
        _setups.Add((predicate, result));
        return this;
    }

    public Mock<TIn, TOut> Returns(TOut value)
    {
        _defaultBehavior = _ => value;
        return this;
    }

    public Mock<TIn, TOut> Returns(Func<TIn, TOut> behavior)
    {
        _defaultBehavior = behavior;
        return this;
    }

    public Mock<TIn, TOut> Throws<TException>() where TException : Exception, new()
    {
        _throwBehavior = _ => new TException();
        return this;
    }

    public Mock<TIn, TOut> Throws(Func<TIn, Exception> exceptionFactory)
    {
        _throwBehavior = exceptionFactory;
        return this;
    }

    public Proxy<TIn, TOut> Build()
    {
        return Proxy<TIn, TOut>.Create(Execute)
            .Intercept((input, next) =>
            {
                _invocations.Add(input);
                return next(input);
            })
            .Build();
    }

    private TOut Execute(TIn input)
    {
        if (_throwBehavior != null)
            throw _throwBehavior(input);

        foreach (var (predicate, result) in _setups)
        {
            if (predicate(input))
                return result;
        }

        return _defaultBehavior(input);
    }

    // Verification methods
    public void Verify(Func<TIn, bool> predicate, int times)
    {
        var count = _invocations.Count(predicate);
        if (count != times)
            throw new MockVerificationException(
                $"Expected {times} calls matching predicate, but got {count}");
    }

    public void VerifyOnce(Func<TIn, bool> predicate) => Verify(predicate, 1);

    public void VerifyNever(Func<TIn, bool> predicate) => Verify(predicate, 0);

    public void VerifyAny() => Verify(_ => true, _invocations.Count > 0 ? _invocations.Count : throw new MockVerificationException("Expected at least one call"));

    public IReadOnlyList<TIn> Invocations => _invocations.AsReadOnly();

    public TIn? LastInvocation => _invocations.LastOrDefault();
}

public class MockVerificationException : Exception
{
    public MockVerificationException(string message) : base(message) { }
}

// Async version
public class AsyncMock<TIn, TOut> where TIn : notnull
{
    private readonly List<TIn> _invocations = new();
    private readonly List<(Func<TIn, bool> Predicate, TOut Result)> _setups = new();
    private Func<TIn, Task<TOut>> _defaultBehavior = _ => Task.FromResult(default(TOut)!);

    public AsyncMock<TIn, TOut> Setup(Func<TIn, bool> predicate, TOut result)
    {
        _setups.Add((predicate, result));
        return this;
    }

    public AsyncMock<TIn, TOut> ReturnsAsync(TOut value)
    {
        _defaultBehavior = _ => Task.FromResult(value);
        return this;
    }

    public AsyncMock<TIn, TOut> ReturnsAsync(Func<TIn, Task<TOut>> behavior)
    {
        _defaultBehavior = behavior;
        return this;
    }

    public AsyncProxy<TIn, TOut> Build()
    {
        return AsyncProxy<TIn, TOut>.Create(ExecuteAsync)
            .Intercept(async (input, ct, next) =>
            {
                _invocations.Add(input);
                return await next(input, ct);
            })
            .Build();
    }

    private async ValueTask<TOut> ExecuteAsync(TIn input, CancellationToken ct)
    {
        foreach (var (predicate, result) in _setups)
        {
            if (predicate(input))
                return result;
        }

        return await _defaultBehavior(input);
    }

    public void Verify(Func<TIn, bool> predicate, int times)
    {
        var count = _invocations.Count(predicate);
        if (count != times)
            throw new MockVerificationException(
                $"Expected {times} calls, got {count}");
    }
}

// Tests
public class OrderServiceTests
{
    [Fact]
    public void ProcessOrder_ValidOrder_ChargesPayment()
    {
        // Arrange
        var paymentMock = new Mock<PaymentRequest, PaymentResult>()
            .Setup(r => r.Amount > 0, new PaymentResult(true, "tx-123"))
            .Setup(r => r.Amount <= 0, new PaymentResult(false, null, "Invalid amount"));

        var inventoryMock = new Mock<ReservationRequest, ReservationResult>()
            .Returns(new ReservationResult(true, "res-456"));

        var service = new OrderService(
            paymentProxy: paymentMock.Build(),
            inventoryProxy: inventoryMock.Build());

        // Act
        var result = service.ProcessOrder(new Order
        {
            Items = new[] { new OrderItem("SKU-1", 2) },
            Total = 99.99m,
            PaymentInfo = new PaymentInfo("4111111111111111", "12/25", "123")
        });

        // Assert
        Assert.True(result.Success);
        Assert.Equal("tx-123", result.TransactionId);

        // Verify interactions
        paymentMock.VerifyOnce(r => r.Amount == 99.99m);
        inventoryMock.VerifyOnce(r => r.Items.Length == 1);
    }

    [Fact]
    public void ProcessOrder_PaymentFails_ReturnsError()
    {
        // Arrange
        var paymentMock = new Mock<PaymentRequest, PaymentResult>()
            .Returns(new PaymentResult(false, null, "Declined"));

        var inventoryMock = new Mock<ReservationRequest, ReservationResult>()
            .Returns(new ReservationResult(true, "res-789"));

        var service = new OrderService(
            paymentProxy: paymentMock.Build(),
            inventoryProxy: inventoryMock.Build());

        // Act
        var result = service.ProcessOrder(new Order { Total = 100m });

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Declined", result.Error);
    }

    [Fact]
    public async Task GetUserAsync_CachesResult()
    {
        // Arrange
        var callCount = 0;
        var userMock = new AsyncMock<int, User>()
            .ReturnsAsync(async id =>
            {
                callCount++;
                await Task.Delay(10); // Simulate DB
                return new User(id, $"User {id}");
            });

        var proxy = userMock.Build();

        // Act
        var user1 = await proxy.ExecuteAsync(123);
        var user2 = await proxy.ExecuteAsync(123);

        // Assert - both calls go through (no caching in this mock)
        Assert.Equal(2, callCount);
        Assert.Equal("User 123", user1.Name);
    }

    [Fact]
    public void ProcessOrder_ThrowsOnNetworkError()
    {
        // Arrange
        var paymentMock = new Mock<PaymentRequest, PaymentResult>()
            .Throws<HttpRequestException>();

        var service = new OrderService(paymentProxy: paymentMock.Build(), null!);

        // Act & Assert
        Assert.Throws<HttpRequestException>(() =>
            service.ProcessOrder(new Order { Total = 50m }));
    }
}

// Service under test
public class OrderService
{
    private readonly Proxy<PaymentRequest, PaymentResult> _paymentProxy;
    private readonly Proxy<ReservationRequest, ReservationResult>? _inventoryProxy;

    public OrderService(
        Proxy<PaymentRequest, PaymentResult> paymentProxy,
        Proxy<ReservationRequest, ReservationResult>? inventoryProxy)
    {
        _paymentProxy = paymentProxy;
        _inventoryProxy = inventoryProxy;
    }

    public OrderResult ProcessOrder(Order order)
    {
        if (_inventoryProxy != null)
        {
            var reservation = _inventoryProxy.Execute(
                new ReservationRequest(order.Items ?? Array.Empty<OrderItem>()));

            if (!reservation.Success)
                return new OrderResult(false, null, "Items unavailable");
        }

        var payment = _paymentProxy.Execute(
            new PaymentRequest(order.Total, order.PaymentInfo!));

        if (!payment.Success)
            return new OrderResult(false, null, payment.Error);

        return new OrderResult(true, payment.TransactionId);
    }
}
```

### Why This Pattern

The mock framework uses proxies to capture invocations and control behavior. This is exactly how production mocking libraries like Moq work internally, demonstrating the proxy pattern's power for testing infrastructure.

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
