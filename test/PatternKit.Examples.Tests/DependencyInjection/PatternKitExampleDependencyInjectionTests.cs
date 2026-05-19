using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.DependencyInjection;

[Feature("Example-level dependency injection integrations")]
public sealed class PatternKitExampleDependencyInjectionTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Every catalog example has a fluent IServiceCollection integration")]
    [Fact]
    public Task Every_Catalog_Example_Has_A_Fluent_IServiceCollection_Integration()
        => Given("a service collection configured with all PatternKit examples", () =>
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddPatternKitExamples();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving the catalog and registered example service descriptors", provider => new
            {
                Provider = provider,
                Catalog = provider.GetRequiredService<IPatternKitExampleCatalog>(),
                Descriptors = provider.GetServices<PatternKitExampleServiceDescriptor>().ToArray()
            })
            .Then("each catalog entry has a matching IoC descriptor", result =>
                result.Catalog.Entries.All(entry =>
                    result.Descriptors.Any(descriptor =>
                        string.Equals(descriptor.ExampleName, entry.Name, StringComparison.Ordinal))))
            .And("each descriptor resolves its concrete integration service", result =>
                result.Descriptors.All(descriptor =>
                    result.Provider.GetRequiredService(descriptor.ServiceType) is not null))
            .And("each integration advertises dependency injection as an available surface", result =>
                result.Descriptors.All(descriptor =>
                    descriptor.Integration.HasFlag(ExampleIntegrationSurface.DependencyInjection)))
            .AssertPassed();
}
