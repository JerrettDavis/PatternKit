using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.ChangeDataCaptureDemo;
using PatternKit.Messaging.ChangeDataCapture;
using TinyBDD;

namespace PatternKit.Examples.Tests.ChangeDataCaptureDemo;

public sealed class ProductCatalogChangeDataCaptureDemoTests
{
    [Scenario("Product catalog change data capture works through fluent and generated policies")]
    [Fact]
    public async Task Product_Catalog_Change_Data_Capture_Works_Through_Fluent_And_Generated_Policies()
    {
        var fluentPublisher = new InMemoryProductChangePublisher();
        var generatedPublisher = new InMemoryProductChangePublisher();
        var fluent = ProductCatalogChangeDataCapturePolicies.CreateFluent(
            new InMemoryChangeDataCaptureStore<ProductMutation, ProductChanged>(),
            fluentPublisher);
        var generated = GeneratedProductCatalogChangeDataCapture.CreateGenerated(
            (changed, ct) => generatedPublisher.PublishAsync(changed, ct),
            new InMemoryChangeDataCaptureStore<ProductMutation, ProductChanged>());

        await fluent.CaptureAsync(new("sku-1", "Desk", 125m, 1));
        await generated.CaptureAsync(new("sku-2", "Lamp", 40m, 1));
        var fluentSummary = await fluent.PublishPendingAsync();
        var generatedSummary = await generated.PublishPendingAsync();

        ScenarioExpect.Equal(new ChangeDataCapturePublishSummary(1, 0), fluentSummary);
        ScenarioExpect.Equal(new ChangeDataCapturePublishSummary(1, 0), generatedSummary);
        ScenarioExpect.Equal("Desk", ScenarioExpect.Single(fluentPublisher.Published).Name);
        ScenarioExpect.Equal("Lamp", ScenarioExpect.Single(generatedPublisher.Published).Name);
    }

    [Scenario("Product catalog change data capture is importable through IServiceCollection")]
    [Fact]
    public async Task Product_Catalog_Change_Data_Capture_Is_Importable_Through_ServiceCollection()
    {
        using var provider = new ServiceCollection()
            .AddProductCatalogChangeDataCaptureDemo()
            .BuildServiceProvider(validateScopes: true);

        var service = provider.GetRequiredService<ProductCatalogChangeDataCaptureService>();
        var publisher = provider.GetRequiredService<InMemoryProductChangePublisher>();

        var summary = await service.UpsertAsync(new("sku-1", "Desk", 125m, 1));

        ScenarioExpect.Equal(new ChangeDataCapturePublishSummary(1, 0), summary);
        var changed = ScenarioExpect.Single(publisher.Published);
        ScenarioExpect.Equal("sku-1", changed.Sku);
        ScenarioExpect.Equal(1, changed.Sequence);
    }
}
