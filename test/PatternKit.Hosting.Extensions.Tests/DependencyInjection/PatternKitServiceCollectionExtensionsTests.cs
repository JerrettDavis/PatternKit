using Microsoft.Extensions.DependencyInjection;
using PatternKit.Behavioral.NullObject;
using PatternKit.Cloud.Bulkhead;
using PatternKit.Cloud.CircuitBreaker;
using PatternKit.Cloud.PriorityQueue;
using PatternKit.Cloud.QueueLoadLeveling;
using PatternKit.Cloud.RateLimiting;
using PatternKit.Cloud.Retry;
using PatternKit.Hosting.DependencyInjection;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using PatternKit.Messaging.Reliability;
using PatternKit.Messaging.Storage;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Hosting.Extensions.Tests.DependencyInjection;

[Feature("PatternKit hosting extensions")]
public sealed class PatternKitServiceCollectionExtensionsTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Messaging primitives register through IServiceCollection")]
    [Fact]
    public Task Messaging_Primitives_Register_Through_IServiceCollection()
        => Given("a service collection with PatternKit messaging primitives", () =>
            {
                var services = new ServiceCollection();
                services
                    .AddPatternKitMessageChannel<OrderCommand>(
                        "orders",
                        builder => builder.WithCapacity(1, MessageChannelBackpressurePolicy.Reject))
                    .AddPatternKitMessageStore<OrderCommand>(
                        "order-store",
                        builder => builder.IdentifyBy(static (message, _) => message.Payload.OrderId))
                    .AddPatternKitGuaranteedDelivery<OrderCommand>(
                        builder => builder
                            .Name("order-delivery")
                            .LeaseDuration(TimeSpan.FromSeconds(5))
                            .MaxDeliveryAttempts(2));

                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and using the registered primitives", ResolveMessagingRegistrationsAsync)
            .Then("the registered services preserve production configuration", result =>
            {
                ScenarioExpect.Equal("orders", result.Channel.Name);
                ScenarioExpect.True(result.Send.Accepted);
                ScenarioExpect.Equal("order-store", result.Append.StoreName);
                ScenarioExpect.Equal("order-1", result.Append.StoredMessage.MessageId);
                ScenarioExpect.Equal("order-delivery", result.Delivery.Name);
                ScenarioExpect.Equal(TimeSpan.FromSeconds(5), result.Delivery.LeaseDuration);
                ScenarioExpect.Equal(2, result.Delivery.MaxDeliveryAttempts);
                ScenarioExpect.NotNull(result.Lease);
            })
            .AssertPassed();

    [Scenario("Cloud resilience primitives register through IServiceCollection")]
    [Fact]
    public Task Cloud_Resilience_Primitives_Register_Through_IServiceCollection()
        => Given("a service collection with PatternKit cloud resilience primitives", () =>
            {
                var services = new ServiceCollection();
                services
                    .AddPatternKitRetryPolicy<ServiceReply>(
                        "inventory-retry",
                        builder => builder
                            .WithMaxAttempts(2)
                            .HandleResult(static reply => !reply.Available))
                    .AddPatternKitCircuitBreakerPolicy<ServiceReply>(
                        "inventory-breaker",
                        builder => builder
                            .WithFailureThreshold(1)
                            .HandleResult(static reply => !reply.Available))
                    .AddPatternKitBulkheadPolicy<ServiceReply>(
                        "inventory-bulkhead",
                        builder => builder.WithMaxConcurrency(2))
                    .AddPatternKitRateLimitPolicy<ServiceReply>(
                        "inventory-rate-limit",
                        builder => builder.WithPermitLimit(1).WithWindow(TimeSpan.FromMinutes(1)))
                    .AddPatternKitQueueLoadLevelingPolicy<ServiceReply>(
                        "inventory-leveling",
                        builder => builder.WithMaxConcurrentWorkers(2).WithMaxQueueLength(4))
                    .AddPatternKitPriorityQueue<WorkItem, int>(
                        static item => item.Priority,
                        "inventory-priority");

                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and using the registered policies", provider =>
            {
                using (provider)
                {
                    var retry = provider.GetRequiredService<RetryPolicy<ServiceReply>>();
                    var breaker = provider.GetRequiredService<CircuitBreakerPolicy<ServiceReply>>();
                    var bulkhead = provider.GetRequiredService<BulkheadPolicy<ServiceReply>>();
                    var rateLimit = provider.GetRequiredService<RateLimitPolicy<ServiceReply>>();
                    var leveling = provider.GetRequiredService<QueueLoadLevelingPolicy<ServiceReply>>();
                    var priority = provider.GetRequiredService<PriorityQueuePolicy<WorkItem, int>>();

                    var retryResult = retry.Execute(static () => new ServiceReply(true));
                    var breakerResult = breaker.Execute(static () => new ServiceReply(true));
                    var bulkheadResult = bulkhead.Execute(static () => new ServiceReply(true));
                    var rateLimitResult = rateLimit.Execute("tenant-a", static () => new ServiceReply(true));
                    var levelingResult = leveling.Execute(static () => new ServiceReply(true));
                    priority.Enqueue(new("slow", 1));
                    priority.Enqueue(new("fast", 10));
                    var next = priority.Dequeue();

                    return new CloudRegistrationResult(
                        retry,
                        breaker,
                        bulkhead,
                        rateLimit,
                        leveling,
                        priority,
                        retryResult,
                        breakerResult,
                        bulkheadResult,
                        rateLimitResult,
                        levelingResult,
                        next);
                }
            })
            .Then("the registered policies execute with configured names and behavior", result =>
            {
                ScenarioExpect.Equal("inventory-retry", result.Retry.Name);
                ScenarioExpect.True(result.RetryResult.Succeeded);
                ScenarioExpect.Equal("inventory-breaker", result.Breaker.Name);
                ScenarioExpect.True(result.BreakerResult.Succeeded);
                ScenarioExpect.Equal("inventory-bulkhead", result.Bulkhead.Name);
                ScenarioExpect.True(result.BulkheadResult.Succeeded);
                ScenarioExpect.Equal("inventory-rate-limit", result.RateLimit.Name);
                ScenarioExpect.True(result.RateLimitResult.Allowed);
                ScenarioExpect.Equal("inventory-leveling", result.Leveling.Name);
                ScenarioExpect.True(result.LevelingResult.Accepted);
                ScenarioExpect.Equal("inventory-priority", result.Priority.Name);
                ScenarioExpect.Equal("fast", result.Next.Item!.Id);
            })
            .AssertPassed();

    [Scenario("Hosting extensions support DI lifetimes")]
    [Fact]
    public Task Hosting_Extensions_Support_Di_Lifetimes()
        => Given("a service collection with scoped and transient PatternKit registrations", () =>
            {
                var services = new ServiceCollection();
                services
                    .AddPatternKitMessageChannel<OrderCommand>(lifetime: ServiceLifetime.Scoped)
                    .AddPatternKitRetryPolicy<ServiceReply>(lifetime: ServiceLifetime.Transient)
                    .AddPatternKitNullObject<INotificationSink>(new SilentNotificationSink(), ServiceLifetime.Singleton)
                    .AddPatternKitNullObject<IStatusSink>(_ => new SilentStatusSink(), ServiceLifetime.Singleton);

                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving across scopes", provider =>
            {
                using (provider)
                using (var scopeA = provider.CreateScope())
                using (var scopeB = provider.CreateScope())
                {
                    var channelA1 = scopeA.ServiceProvider.GetRequiredService<MessageChannel<OrderCommand>>();
                    var channelA2 = scopeA.ServiceProvider.GetRequiredService<MessageChannel<OrderCommand>>();
                    var channelB = scopeB.ServiceProvider.GetRequiredService<MessageChannel<OrderCommand>>();
                    var retryA = provider.GetRequiredService<RetryPolicy<ServiceReply>>();
                    var retryB = provider.GetRequiredService<RetryPolicy<ServiceReply>>();
                    var nullObject = provider.GetRequiredService<NullObject<INotificationSink>>();
                    var sink = provider.GetRequiredService<INotificationSink>();
                    var statusNullObject = provider.GetRequiredService<NullObject<IStatusSink>>();
                    var statusSink = provider.GetRequiredService<IStatusSink>();

                    return new LifetimeRegistrationResult(channelA1, channelA2, channelB, retryA, retryB, nullObject, sink, statusNullObject, statusSink);
                }
            })
            .Then("scoped registrations reuse a scope and transient registrations create new policies", result =>
            {
                ScenarioExpect.Same(result.ChannelA1, result.ChannelA2);
                ScenarioExpect.NotSame(result.ChannelA1, result.ChannelB);
                ScenarioExpect.NotSame(result.RetryA, result.RetryB);
                ScenarioExpect.Same(result.NullObject.Instance, result.Sink);
                ScenarioExpect.False(result.NullObject.Instance.Send("C-1", "optional"));
                ScenarioExpect.Same(result.StatusNullObject.Instance, result.StatusSink);
                ScenarioExpect.Equal("suppressed", result.StatusSink.Status);
            })
            .AssertPassed();

    [Scenario("Hosting extensions validate registration input")]
    [Fact]
    public Task Hosting_Extensions_Validate_Registration_Input()
        => Given("invalid PatternKit hosting registration inputs", () => new InvalidRegistrationInputs(null, new ServiceCollection()))
            .When("calling registration extensions", inputs => new InvalidRegistrationResults(
                ScenarioExpect.Throws<ArgumentNullException>(
                    () => inputs.MissingServices!.AddPatternKitMessageChannel<OrderCommand>()),
                ScenarioExpect.Throws<ArgumentNullException>(
                    () => inputs.Services.AddPatternKitPriorityQueue<WorkItem, int>(null!)),
                ScenarioExpect.Throws<ArgumentNullException>(
                    () => inputs.MissingServices!.AddPatternKitNullObject<INotificationSink>(new SilentNotificationSink())),
                ScenarioExpect.Throws<ArgumentNullException>(
                    () => inputs.MissingServices!.AddPatternKitNullObject<INotificationSink>(_ => new SilentNotificationSink())),
                ScenarioExpect.Throws<ArgumentNullException>(
                    () => inputs.Services.AddPatternKitNullObject<INotificationSink>((INotificationSink)null!)),
                ScenarioExpect.Throws<ArgumentNullException>(
                    () => inputs.Services.AddPatternKitNullObject<INotificationSink>((Func<IServiceProvider, INotificationSink>)null!)),
                ScenarioExpect.Throws<ArgumentNullException>(
                    () => inputs.Services.AddPatternKitNullObject<INotificationSink>(_ => null!).BuildServiceProvider(validateScopes: true).GetRequiredService<NullObject<INotificationSink>>()),
                ScenarioExpect.Throws<ArgumentOutOfRangeException>(
                    () => inputs.Services.AddPatternKitRetryPolicy<ServiceReply>(lifetime: (ServiceLifetime)99))))
            .Then("the extensions reject invalid registrations explicitly", results =>
            {
                ScenarioExpect.Equal("services", results.MissingServicesException.ParamName);
                ScenarioExpect.Equal("prioritySelector", results.PrioritySelectorException.ParamName);
                ScenarioExpect.Equal("services", results.NullObjectInstanceMissingServicesException.ParamName);
                ScenarioExpect.Equal("services", results.NullObjectFactoryMissingServicesException.ParamName);
                ScenarioExpect.Equal("instance", results.NullObjectInstanceException.ParamName);
                ScenarioExpect.Equal("factory", results.NullObjectFactoryException.ParamName);
                ScenarioExpect.Equal("instance", results.NullObjectFactoryResultException.ParamName);
                ScenarioExpect.Equal("lifetime", results.InvalidLifetimeException.ParamName);
            })
            .AssertPassed();

    private sealed record OrderCommand(string OrderId, decimal Total);
    private sealed record ServiceReply(bool Available);
    private sealed record WorkItem(string Id, int Priority);
    private interface INotificationSink
    {
        bool Send(string recipient, string body);
    }

    private sealed class SilentNotificationSink : INotificationSink
    {
        public bool Send(string recipient, string body) => false;
    }

    private interface IStatusSink
    {
        string Status { get; }
    }

    private sealed class SilentStatusSink : IStatusSink
    {
        public string Status => "suppressed";
    }

    private sealed record InvalidRegistrationInputs(IServiceCollection? MissingServices, IServiceCollection Services);

    private sealed record InvalidRegistrationResults(
        ArgumentNullException MissingServicesException,
        ArgumentNullException PrioritySelectorException,
        ArgumentNullException NullObjectInstanceMissingServicesException,
        ArgumentNullException NullObjectFactoryMissingServicesException,
        ArgumentNullException NullObjectInstanceException,
        ArgumentNullException NullObjectFactoryException,
        ArgumentNullException NullObjectFactoryResultException,
        ArgumentOutOfRangeException InvalidLifetimeException);

    private sealed record MessagingRegistrationResult(
        MessageChannel<OrderCommand> Channel,
        MessageStore<OrderCommand> Store,
        GuaranteedDeliveryQueue<OrderCommand> Delivery,
        MessageChannelSendResult Send,
        MessageStoreAppendResult<OrderCommand> Append,
        GuaranteedDeliveryLease<OrderCommand>? Lease);

    private sealed record CloudRegistrationResult(
        RetryPolicy<ServiceReply> Retry,
        CircuitBreakerPolicy<ServiceReply> Breaker,
        BulkheadPolicy<ServiceReply> Bulkhead,
        RateLimitPolicy<ServiceReply> RateLimit,
        QueueLoadLevelingPolicy<ServiceReply> Leveling,
        PriorityQueuePolicy<WorkItem, int> Priority,
        RetryResult<ServiceReply> RetryResult,
        CircuitBreakerResult<ServiceReply> BreakerResult,
        BulkheadResult<ServiceReply> BulkheadResult,
        RateLimitResult<ServiceReply> RateLimitResult,
        QueueLoadLevelingResult<ServiceReply> LevelingResult,
        PriorityQueueDequeueResult<WorkItem, int> Next);

    private sealed record LifetimeRegistrationResult(
        MessageChannel<OrderCommand> ChannelA1,
        MessageChannel<OrderCommand> ChannelA2,
        MessageChannel<OrderCommand> ChannelB,
        RetryPolicy<ServiceReply> RetryA,
        RetryPolicy<ServiceReply> RetryB,
        NullObject<INotificationSink> NullObject,
        INotificationSink Sink,
        NullObject<IStatusSink> StatusNullObject,
        IStatusSink StatusSink);

    private static async Task<MessagingRegistrationResult> ResolveMessagingRegistrationsAsync(ServiceProvider provider)
    {
        using (provider)
        {
            var channel = provider.GetRequiredService<MessageChannel<OrderCommand>>();
            var store = provider.GetRequiredService<MessageStore<OrderCommand>>();
            var delivery = provider.GetRequiredService<GuaranteedDeliveryQueue<OrderCommand>>();
            var message = Message<OrderCommand>.Create(new("order-1", 125m));

            var send = channel.Send(message);
            var append = store.Append(message);
            await delivery.EnqueueAsync(message);
            var lease = await delivery.TryReceiveAsync();

            return new MessagingRegistrationResult(channel, store, delivery, send, append, lease);
        }
    }
}
