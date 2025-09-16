using PatternKit.Creational.Singleton;
using PatternKit.Examples.Singleton;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Singleton;

[Feature("POS App State Singleton demo (eager/lazy + init hooks)")]
public sealed class PosAppStateDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("RunLazyOnce returns the expected log order and calls factory once")]
    [Fact]
    public Task RunLazyOnce_Logs_And_FactoryOnce()
        => Given("the RunLazyOnce helper", () => (Func<List<string>>)PosAppStateDemo.RunLazyOnce)
            .When("running it", run => run())
            .Then("log[0] is factory:config", log => log.ElementAtOrDefault(0) == "factory:config")
            .And("log[1] is init:pricing", log => log.ElementAtOrDefault(1) == "init:pricing")
            .And("log[2] is init:devices", log => log.ElementAtOrDefault(2) == "init:devices")
            .And("factory called once", _ => PosAppStateDemo.FactoryCalls == 1)
            .AssertPassed();

    [Scenario("RunEagerOnce creates at Build and logs the same order")]
    [Fact]
    public Task RunEagerOnce_Logs_And_FactoryOnce()
        => Given("the RunEagerOnce helper", () => (Func<List<string>>)PosAppStateDemo.RunEagerOnce)
            .When("running it", run => run())
            .Then("log[0] is factory:config", log => log.ElementAtOrDefault(0) == "factory:config")
            .And("then init:pricing, init:devices", log => log.Skip(1).Take(2).SequenceEqual(["init:pricing", "init:devices"]))
            .And("factory called once", _ => PosAppStateDemo.FactoryCalls == 1)
            .AssertPassed();

    [Scenario("Lazy singleton: thread-safe single factory invocation under concurrency")]
    [Fact]
    public async Task Lazy_ThreadSafe_Single_Factory()
    {
        PosAppStateDemo.ResetCounters();
        var s = PosAppStateDemo.BuildLazy();

        await Given("32 concurrent Instance reads", () => s)
            .When("parallel read", async Task<Singleton<PosAppState>> (singleton) =>
            {
                var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() => singleton.Instance)).ToArray();
                await Task.WhenAll(tasks);
                return singleton;
            })
            .Then("factory called exactly once", _ => Volatile.Read(ref PosAppStateDemo.FactoryCalls) == 1)
            .AssertPassed();
    }

    [Scenario("State invariants after init: config loaded, pricing prewarmed, devices connected")]
    [Fact]
    public Task State_Invariants_After_Init()
        => Given("a fresh lazy singleton", () => { PosAppStateDemo.ResetCounters(); return PosAppStateDemo.BuildLazy(); })
            .When("access Instance", s => s.Instance)
            .Then("store id and tax", st => st.Config is { StoreId: "STORE-001", TaxRate: 0.0875m })
            .And("pricing warmed count == 3", st => st.Pricing.WarmedCount == 3)
            .And("devices ready", st => st.Devices is { PrinterReady: true, DrawerReady: true })
            .AssertPassed();
}
