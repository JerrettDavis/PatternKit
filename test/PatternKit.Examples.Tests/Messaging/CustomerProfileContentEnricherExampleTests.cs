using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Transformation;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class CustomerProfileContentEnricherExampleTests
{
    [Scenario("FluentContentEnricher EnrichesCustomerProfile")]
    [Fact]
    public async Task FluentContentEnricher_EnrichesCustomerProfile()
    {
        var summary = await CustomerProfileContentEnricherExampleRunner.RunFluentAsync(
            new("customer-1", " USER@EXAMPLE.COM ", null, false));

        ScenarioExpect.Equal("customer-1", summary.CustomerId);
        ScenarioExpect.Equal("user@example.com", summary.Email);
        ScenarioExpect.Equal("Standard", summary.Tier);
        ScenarioExpect.False(summary.MarketingOptIn);
        ScenarioExpect.Equal(2, summary.AppliedSteps);
    }

    [Scenario("GeneratedContentEnricher MatchesFluentBehavior")]
    [Fact]
    public async Task GeneratedContentEnricher_MatchesFluentBehavior()
    {
        var update = new CustomerProfileUpdate("customer-2", " PREMIUM@EXAMPLE.COM ", "Premium", false);

        var fluent = await CustomerProfileContentEnricherExampleRunner.RunFluentAsync(update);
        var generated = await GeneratedCustomerProfileContentEnricher.Create()
            .EnrichAsync(PatternKit.Messaging.Message<CustomerProfileUpdate>.Create(update));

        ScenarioExpect.Equal(fluent.Email, generated.Message.Payload.Email);
        ScenarioExpect.Equal(fluent.Tier, generated.Message.Payload.Tier);
        ScenarioExpect.True(generated.Message.Payload.MarketingOptIn);
    }

    [Scenario("ServiceCollection ImportsContentEnricherExample")]
    [Fact]
    public async Task ServiceCollection_ImportsContentEnricherExample()
    {
        var services = new ServiceCollection();
        services.AddCustomerProfileContentEnricherDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var enricher = provider.GetRequiredService<AsyncContentEnricher<CustomerProfileUpdate>>();
        var runner = provider.GetRequiredService<CustomerProfileContentEnricherExampleRunner>();

        var direct = await enricher.EnrichAsync(PatternKit.Messaging.Message<CustomerProfileUpdate>.Create(new("customer-1", null, "Premium", false)));
        var summary = await runner.RunGeneratedAsync(new("customer-2", "USER@EXAMPLE.COM", null, false));

        ScenarioExpect.Equal("unknown@example.com", direct.Message.Payload.Email);
        ScenarioExpect.True(direct.Message.Payload.MarketingOptIn);
        ScenarioExpect.Equal("Standard", summary.Tier);
    }

    [Scenario("AggregateServiceCollection ImportsContentEnricherExample")]
    [Fact]
    public async Task AggregateServiceCollection_ImportsContentEnricherExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<CustomerProfileContentEnricherExampleService>();

        var summary = await example.Service.EnrichAsync(new("customer-1", "USER@EXAMPLE.COM", "Premium", false));

        ScenarioExpect.Equal("user@example.com", summary.Email);
        ScenarioExpect.True(summary.MarketingOptIn);
    }
}
