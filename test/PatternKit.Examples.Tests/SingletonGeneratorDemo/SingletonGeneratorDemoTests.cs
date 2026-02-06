using PatternKit.Examples.SingletonGeneratorDemo;

namespace PatternKit.Examples.Tests.SingletonGeneratorDemo;

public class SingletonGeneratorDemoTests
{
    [Fact]
    public void AppClock_ReturnsSameInstance()
    {
        // Act
        var clock1 = AppClock.Instance;
        var clock2 = AppClock.Instance;

        // Assert
        Assert.Same(clock1, clock2);
    }

    [Fact]
    public void AppClock_ProvidesCurrentTime()
    {
        // Act
        var before = DateTime.UtcNow;
        var clockTime = AppClock.Instance.UtcNow;
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(clockTime, before, after);
    }

    [Fact]
    public void AppClock_ProvidesUnixTimestamp()
    {
        // Arrange
        var beforeTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var timestamp = AppClock.Instance.UnixTimestamp;
        var afterTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Assert
        Assert.True(timestamp > 0);
        Assert.InRange(timestamp, beforeTimestamp, afterTimestamp);
    }

    [Fact]
    public void ConfigManager_ReturnsSameInstance()
    {
        // Act
        var config1 = ConfigManager.Instance;
        var config2 = ConfigManager.Instance;

        // Assert
        Assert.Same(config1, config2);
    }

    [Fact]
    public void ConfigManager_HasDefaultValues()
    {
        // Act
        var config = ConfigManager.Instance;

        // Assert
        Assert.NotNull(config.AppName);
        Assert.NotNull(config.Environment);
        Assert.NotNull(config.ConnectionString);
    }

    [Fact]
    public void ConfigManager_LoadedAtIsInPast()
    {
        // Act
        var config = ConfigManager.Instance;

        // Assert
        Assert.True(config.LoadedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void ServiceRegistry_ReturnsSameInstance()
    {
        // Act
        var registry1 = ServiceRegistry.Instance;
        var registry2 = ServiceRegistry.Instance;

        // Assert
        Assert.Same(registry1, registry2);
    }

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

        // Assert
        Assert.Same(service, resolved);
    }

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

        // Assert
        Assert.Same(first, second); // Factory result is cached
        Assert.Equal(1, callCount); // Factory only called once
    }

    [Fact]
    public void ServiceRegistry_TryResolve_ReturnsNullWhenNotFound()
    {
        // Arrange
        var registry = ServiceRegistry.Instance;
        registry.Clear();

        // Act
        var result = registry.TryResolve<IUnregisteredService>();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ServiceRegistry_Resolve_ThrowsWhenNotFound()
    {
        // Arrange
        var registry = ServiceRegistry.Instance;
        registry.Clear();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            registry.Resolve<IUnregisteredService>());
    }

    [Fact]
    public void ServiceRegistry_IsRegistered()
    {
        // Arrange
        var registry = ServiceRegistry.Instance;
        registry.Clear();
        registry.Register<ITestService>(new TestService("test"));

        // Assert
        Assert.True(registry.IsRegistered<ITestService>());
        Assert.False(registry.IsRegistered<IUnregisteredService>());
    }

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

        // Assert - all should get the same instance
        var first = results[0];
        Assert.All(results, r => Assert.Same(first, r));
        // Factory is invoked exactly once due to Lazy<T> ensuring single execution
        Assert.Equal(1, creationCount);
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
