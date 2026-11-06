using PatternKit.Application.Repository;
using PatternKit.Application.Specification;
using TinyBDD;

namespace PatternKit.Tests.Application.Repository;

public sealed class RepositoryTests
{
    [Scenario("Repository Adds Gets Lists And Filters Entities")]
    [Fact]
    public async Task Repository_Adds_Gets_Lists_And_Filters_Entities()
    {
        var repository = InMemoryRepository<Order, string>.Create(static order => order.Id).Build();
        var open = Specification<Order>.Where("open", static order => order.Status == "Open");

        var stored = await repository.AddAsync(new Order("order-1", "Open", 125m));
        _ = await repository.AddAsync(new Order("order-2", "Closed", 25m));

        var loaded = await repository.GetAsync("order-1");
        var all = await repository.ListAsync();
        var openOrders = await repository.FindAsync(open);

        ScenarioExpect.True(stored.Succeeded);
        ScenarioExpect.Equal("order-1", loaded!.Id);
        ScenarioExpect.Equal(2, all.Count);
        var match = ScenarioExpect.Single(openOrders);
        ScenarioExpect.Equal("order-1", match.Id);
    }

    [Scenario("Repository RejectsDuplicateAddsAndMissingUpdates")]
    [Fact]
    public async Task Repository_RejectsDuplicateAddsAndMissingUpdates()
    {
        var repository = InMemoryRepository<Order, string>.Create(static order => order.Id).Build();
        var order = new Order("order-1", "Open", 125m);
        _ = await repository.AddAsync(order);

        var duplicate = await repository.AddAsync(order);
        var missing = await repository.UpdateAsync(new Order("missing", "Open", 10m));

        ScenarioExpect.Equal(RepositoryStatus.Conflict, duplicate.Status);
        ScenarioExpect.False(duplicate.Succeeded);
        ScenarioExpect.Contains("already exists", duplicate.Reason!);
        ScenarioExpect.Equal(RepositoryStatus.Missing, missing.Status);
        ScenarioExpect.Contains("was not found", missing.Reason!);
    }

    [Scenario("Repository UpdatesAndRemovesExistingEntities")]
    [Fact]
    public async Task Repository_UpdatesAndRemovesExistingEntities()
    {
        var repository = InMemoryRepository<Order, string>.Create(static order => order.Id).Build();
        _ = await repository.AddAsync(new Order("order-1", "Open", 125m));

        var updated = await repository.UpdateAsync(new Order("order-1", "Closed", 125m));
        var loaded = await repository.GetAsync("order-1");
        var removed = await repository.RemoveAsync("order-1");
        var afterRemove = await repository.GetAsync("order-1");

        ScenarioExpect.True(updated.Succeeded);
        ScenarioExpect.Equal("Closed", loaded!.Status);
        ScenarioExpect.True(removed);
        ScenarioExpect.Null(afterRemove);
    }

    [Scenario("Repository ValidatesInputsAndObservesCancellation")]
    [Fact]
    public async Task Repository_ValidatesInputsAndObservesCancellation()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() => InMemoryRepository<Order, string>.Create(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => InMemoryRepository<Order, string>.Create(static order => order.Id).UseComparer(null!));

        var repository = InMemoryRepository<Order, string>.Create(static order => order.Id).Build();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await repository.AddAsync(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await repository.FindAsync(null!));
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () => await repository.ListAsync(source.Token));
    }

    private sealed record Order(string Id, string Status, decimal Total);
}
