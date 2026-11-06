using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Transformation;

public sealed class InMemoryClaimCheckStoreWithTtlTests
{
    [Scenario("StoreAsync And TryLoadAsync RoundTrip")]
    [Fact]
    public async Task StoreAsync_And_TryLoadAsync_RoundTrip()
    {
        var store = new InMemoryClaimCheckStoreWithTtl<string>();
        await store.StoreAsync("claim-1", "payload-1", MessageHeaders.Empty);

        var loaded = await store.TryLoadAsync("claim-1");

        ScenarioExpect.NotNull(loaded);
        ScenarioExpect.Equal("payload-1", loaded!.Payload);
    }

    [Scenario("TryLoadAsync ReturnsNullForUnknownClaim")]
    [Fact]
    public async Task TryLoadAsync_ReturnsNullForUnknownClaim()
    {
        var store = new InMemoryClaimCheckStoreWithTtl<string>();

        var loaded = await store.TryLoadAsync("nonexistent");

        ScenarioExpect.Null(loaded);
    }

    [Scenario("StoreAsync WithTtl ExpiredEntryNotReturned")]
    [Fact]
    public async Task StoreAsync_WithTtl_ExpiredEntryNotReturned()
    {
        var store = new InMemoryClaimCheckStoreWithTtl<string>();
        await store.StoreAsync("claim-1", "payload-1", MessageHeaders.Empty, TimeSpan.FromMilliseconds(20));

        await Task.Delay(50);
        var loaded = await store.TryLoadAsync("claim-1");

        ScenarioExpect.Null(loaded);
    }

    [Scenario("StoreAsync WithTtl ActiveEntryReturned")]
    [Fact]
    public async Task StoreAsync_WithTtl_ActiveEntryReturned()
    {
        var store = new InMemoryClaimCheckStoreWithTtl<string>();
        await store.StoreAsync("claim-1", "payload-1", MessageHeaders.Empty, TimeSpan.FromMinutes(10));

        var loaded = await store.TryLoadAsync("claim-1");

        ScenarioExpect.NotNull(loaded);
    }

    [Scenario("EvictExpiredAsync RemovesExpiredEntries")]
    [Fact]
    public async Task EvictExpiredAsync_RemovesExpiredEntries()
    {
        var store = new InMemoryClaimCheckStoreWithTtl<string>();
        await store.StoreAsync("claim-expire", "a", MessageHeaders.Empty, TimeSpan.FromMilliseconds(20));
        await store.StoreAsync("claim-keep", "b", MessageHeaders.Empty, TimeSpan.FromMinutes(10));

        await Task.Delay(50);
        var evicted = await store.EvictExpiredAsync();

        ScenarioExpect.Equal(1, evicted);
        ScenarioExpect.Null(await store.TryLoadAsync("claim-expire"));
        ScenarioExpect.NotNull(await store.TryLoadAsync("claim-keep"));
    }

    [Scenario("StoreAsync PreservesHeaders")]
    [Fact]
    public async Task StoreAsync_PreservesHeaders()
    {
        var store = new InMemoryClaimCheckStoreWithTtl<string>();
        var headers = MessageHeaders.Empty.WithCorrelationId("corr-42");
        await store.StoreAsync("claim-1", "payload", headers);

        var loaded = await store.TryLoadAsync("claim-1");

        ScenarioExpect.Equal("corr-42", loaded!.Headers.CorrelationId);
    }

    [Scenario("StoreAsync RejectsEmptyClaimId")]
    [Fact]
    public async Task StoreAsync_RejectsEmptyClaimId()
    {
        var store = new InMemoryClaimCheckStoreWithTtl<string>();

        await ScenarioExpect.ThrowsAsync<ArgumentException>(
            () => store.StoreAsync("", "payload", MessageHeaders.Empty).AsTask());
    }

    [Scenario("StoreAsync RejectsNegativeTtl")]
    [Fact]
    public async Task StoreAsync_RejectsNegativeTtl()
    {
        var store = new InMemoryClaimCheckStoreWithTtl<string>();

        await ScenarioExpect.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.StoreAsync("claim-1", "payload", MessageHeaders.Empty, TimeSpan.FromSeconds(-1)).AsTask());
    }

    [Scenario("StoreAsync RejectsNullHeaders")]
    [Fact]
    public async Task StoreAsync_RejectsNullHeaders()
    {
        var store = new InMemoryClaimCheckStoreWithTtl<string>();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(
            () => store.StoreAsync("claim-1", "payload", null!).AsTask());
    }
}
