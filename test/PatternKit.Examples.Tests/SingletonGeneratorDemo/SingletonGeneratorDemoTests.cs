using PatternKit.Examples.SingletonGeneratorDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.SingletonGeneratorDemo;

public class SingletonGeneratorDemoTests
{
    [Scenario("AppClock ReturnsSameInstance")]
    [Fact]
    public void AppClock_ReturnsSameInstance()
    {
        // Act
        var clock1 = AppClock.Instance;
        var clock2 = AppClock.Instance;

        // Then
        ScenarioExpect.Same(clock1, clock2);
    }

    [Scenario("AppClock ProvidesCurrentTime")]
    [Fact]
    public void AppClock_ProvidesCurrentTime()
    {
        // Act
        var before = DateTime.UtcNow;
        var clockTime = AppClock.Instance.UtcNow;
        var after = DateTime.UtcNow;

        // Then
        ScenarioExpect.InRange(clockTime, before, after);
    }

    [Scenario("AppClock ProvidesUnixTimestamp")]
    [Fact]
    public void AppClock_ProvidesUnixTimestamp()
    {
        // Arrange
        var beforeTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var timestamp = AppClock.Instance.UnixTimestamp;
        var afterTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Then
        ScenarioExpect.True(timestamp > 0);
        ScenarioExpect.InRange(timestamp, beforeTimestamp, afterTimestamp);
    }

    [Scenario("AppClock ProvidesLocalTimeAndCurrentDate")]
    [Fact]
    public void AppClock_ProvidesLocalTimeAndCurrentDate()
    {
        var before = DateTimeOffset.Now.AddSeconds(-1);
        var now = AppClock.Instance.Now;
        var after = DateTimeOffset.Now.AddSeconds(1);
        var today = AppClock.Instance.Today;

        ScenarioExpect.InRange(now, before, after);
        ScenarioExpect.InRange(today.DayNumber, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).DayNumber, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).DayNumber);
    }

    [Scenario("ConfigManager ReturnsSameInstance")]
    [Fact]
    public void ConfigManager_ReturnsSameInstance()
    {
        // Act
        var config1 = ConfigManager.Instance;
        var config2 = ConfigManager.Instance;

        // Then
        ScenarioExpect.Same(config1, config2);
    }

    [Scenario("ConfigManager HasDefaultValues")]
    [Fact]
    public void ConfigManager_HasDefaultValues()
    {
        // Act
        var config = ConfigManager.Instance;

        // Then
        ScenarioExpect.NotNull(config.AppName);
        ScenarioExpect.NotNull(config.Environment);
        ScenarioExpect.NotNull(config.ConnectionString);
        ScenarioExpect.False(config.DebugLogging);
        ScenarioExpect.Contains($"App={config.AppName}", config.ToString(), StringComparison.Ordinal);
    }

    [Scenario("ConfigManager LoadedAtIsInPast")]
    [Fact]
    public void ConfigManager_LoadedAtIsInPast()
    {
        // Act
        var config = ConfigManager.Instance;

        // Then
        ScenarioExpect.True(config.LoadedAt <= DateTime.UtcNow);
    }

    [Scenario("ServiceRegistry ReturnsSameInstance")]
    [Fact]
    public void ServiceRegistry_ReturnsSameInstance()
    {
        // Act
        var registry1 = ServiceRegistry.Instance;
        var registry2 = ServiceRegistry.Instance;

        // Then
        ScenarioExpect.Same(registry1, registry2);
    }

    [Scenario("ServiceRegistry RegisterAndResolve")]
    [Fact]
    public void ServiceRegistry_RegisterAndResolve()
    {
        // Arrange
        var registry = ServiceRegistry.Instance;
        registry.Clear();
        var service = new TestService("test");

        // Act
        registry.Register<ITestService>(service);
        var resolved = registry.Resolve<ITestService>();

        // Then
        ScenarioExpect.Same(service, resolved);
    }

    [Scenario("ServiceRegistry RegisterFactory")]
    [Fact]
    public void ServiceRegistry_RegisterFactory()
    {
        // Arrange
        var registry = ServiceRegistry.Instance;
        registry.Clear();
        var callCount = 0;

        // Act
        registry.RegisterFactory<ITestService>(() =>
        {
            callCount++;
            return new TestService($"created-{callCount}");
        });

        var first = registry.Resolve<ITestService>();
        var second = registry.Resolve<ITestService>();

        // Then
        ScenarioExpect.Same(first, second); // Factory result is cached
        ScenarioExpect.Equal(1, callCount); // Factory only called once
    }

    [Scenario("ServiceRegistry TryResolve ReturnsNullWhenNotFound")]
    [Fact]
    public void ServiceRegistry_TryResolve_ReturnsNullWhenNotFound()
    {
        // Arrange
        var registry = ServiceRegistry.Instance;
        registry.Clear();

        // Act
        var result = registry.TryResolve<IUnregisteredService>();

        // Then
        ScenarioExpect.Null(result);
    }

    [Scenario("ServiceRegistry Resolve ThrowsWhenNotFound")]
    [Fact]
    public void ServiceRegistry_Resolve_ThrowsWhenNotFound()
    {
        // Arrange
        var registry = ServiceRegistry.Instance;
        registry.Clear();

        // Act and verify
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            registry.Resolve<IUnregisteredService>());
    }

    [Scenario("ServiceRegistry IsRegistered")]
    [Fact]
    public void ServiceRegistry_IsRegistered()
    {
        // Arrange
        var registry = ServiceRegistry.Instance;
        registry.Clear();
        registry.Register<ITestService>(new TestService("test"));

        // Then
        ScenarioExpect.True(registry.IsRegistered<ITestService>());
        ScenarioExpect.False(registry.IsRegistered<IUnregisteredService>());
    }

    [Scenario("ServiceRegistry ThreadSafe ParallelAccess")]
    [Fact]
    public void ServiceRegistry_ThreadSafe_ParallelAccess()
    {
        // Arrange
        var registry = ServiceRegistry.Instance;
        registry.Clear();
        var creationCount = 0;

        registry.RegisterFactory<ITestService>(() =>
        {
            Interlocked.Increment(ref creationCount);
            Thread.Sleep(10); // Simulate slow creation
            return new TestService($"thread-{Environment.CurrentManagedThreadId}");
        });

        // Act - access from multiple threads simultaneously
        var results = new ITestService[10];
        Parallel.For(0, 10, i =>
        {
            results[i] = registry.Resolve<ITestService>();
        });

        // Then - all should get the same instance
        var first = results[0];
        ScenarioExpect.All(results, r => ScenarioExpect.Same(first, r));
        // Factory is invoked exactly once due to Lazy<T> ensuring single execution
        ScenarioExpect.Equal(1, creationCount);
    }

    // Test service interface
    public interface ITestService
    {
        string Name { get; }
    }

    public interface IUnregisteredService { }

    // Test service implementation
    public class TestService : ITestService
    {
        public string Name { get; }
        public TestService(string name) => Name = name;
    }
}
