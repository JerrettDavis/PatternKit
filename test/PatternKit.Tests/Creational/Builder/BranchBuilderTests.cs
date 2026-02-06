using PatternKit.Creational.Builder;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Creational.Builder;

[Feature("BranchBuilder<TPred,THandler> (collect pairs + optional default, project to product)")]
public sealed class BranchBuilderTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Concrete delegate shapes for the tests
    private delegate bool Pred(in int x);
    private delegate string Handler(in int x);

    // Handlers / predicates (method pointers)
    private static bool IsEven(in int x) => (x & 1) == 0;
    private static bool IsPositive(in int x) => x > 0;
    private static string HandleEven(in int _) => "even";
    private static string HandlePositive(in int _) => "pos";
    private static string HandleDefaultA(in int _) => "defA";
    private static string HandleDefaultB(in int _) => "defB";
    private static string Fallback(in int _) => "fallback";

    // Product projected by BranchBuilder.Build
    private sealed record Product(Pred[] Preds, Handler[] Handlers, bool HasDefault, Handler Default);

    private static Product BuildProduct(BranchBuilder<Pred, Handler> b, Handler? configuredDefault = null)
    {
        if (configuredDefault is not null)
            b.Default(configuredDefault);

        return b.Build(
            fallbackDefault: Fallback,
            projector: static (p, h, hasDef, def) => new Product(p, h, hasDef, def));
    }

    // ---------------- Scenarios ----------------

    [Scenario("Add preserves order; fallback default is used when none was configured")]
    [Fact]
    public async Task Order_And_Fallback()
    {
        await Given("a builder with two predicate/handler pairs", () =>
            {
                var b = BranchBuilder<Pred, Handler>.Create()
                    .Add(IsEven, HandleEven)
                    .Add(IsPositive, HandlePositive);
                return BuildProduct(b); // no explicit Default
            })
            .Then("should have 2 predicates and 2 handlers in registration order", p =>
            {
                Assert.Equal(2, p.Preds.Length);
                Assert.Equal(2, p.Handlers.Length);

                // Invoke to prove order: first is IsEven -> true on 2, second IsPositive -> true on 1
                var v2 = 2; var v1 = 1;
                Assert.True(p.Preds[0](in v2));
                Assert.True(p.Preds[1](in v1));

                // Handlers return expected labels
                Assert.Equal("even", p.Handlers[0](in v2));
                Assert.Equal("pos", p.Handlers[1](in v1));
                return true;
            })
            .And("HasDefault=false and Default==fallback", p =>
                !p.HasDefault && ReferenceEquals(p.Default, (Handler)Fallback))
            .AssertPassed();
    }

    [Scenario("Explicit Default is passed through and HasDefault=true")]
    [Fact]
    public async Task Explicit_Default_Wins()
    {
        await Given("a builder with pairs and an explicit default", () =>
            {
                var b = BranchBuilder<Pred, Handler>.Create()
                    .Add(IsEven, HandleEven)
                    .Add(IsPositive, HandlePositive);
                return BuildProduct(b, HandleDefaultA);
            })
            .Then("HasDefault=true and Default==configured A (not fallback)", p =>
                p.HasDefault && ReferenceEquals(p.Default, (Handler)HandleDefaultA))
            .AssertPassed();
    }

    [Scenario("Default can be replaced; last one wins")]
    [Fact]
    public async Task Default_Replacement_Last_Wins()
    {
        await Given("a builder with two Default() calls", () =>
            {
                var b = BranchBuilder<Pred, Handler>.Create()
                    .Add(IsEven, HandleEven)
                    .Default(HandleDefaultA)
                    .Default(HandleDefaultB); // replace
                return BuildProduct(b);
            })
            .Then("HasDefault=true and Default==B", p =>
                p.HasDefault && ReferenceEquals(p.Default, (Handler)HandleDefaultB))
            .AssertPassed();
    }

    [Scenario("Build supports empty set of pairs")]
    [Fact]
    public async Task Build_Empty_Is_Allowed()
    {
        await Given("an empty builder (no pairs, no default)", () =>
            {
                var b = BranchBuilder<Pred, Handler>.Create();
                return BuildProduct(b);
            })
            .Then("arrays are empty; HasDefault=false; Default==fallback", p =>
                p.Preds.Length == 0 &&
                p.Handlers.Length == 0 &&
                !p.HasDefault &&
                ReferenceEquals(p.Default, (Handler)Fallback))
            .AssertPassed();
    }

    [Scenario("Build snapshots are immutable: later Add() does not mutate earlier product arrays")]
    [Fact]
    public async Task Build_Snapshot_Immutability()
    {
        await Given("a builder; build P1, then add another pair and build P2", () =>
            {
                var b = BranchBuilder<Pred, Handler>.Create()
                    .Add(IsEven, HandleEven);

                var p1 = BuildProduct(b); // snapshot of one pair

                b.Add(IsPositive, HandlePositive);
                var p2 = BuildProduct(b); // snapshot of two pairs

                return (P1: p1, P2: p2);
            })
            .Then("P1 has 1 pair; P2 has 2 pairs", t =>
            {
                Assert.Single(t.P1.Preds);
                Assert.Single(t.P1.Handlers);
                Assert.Equal(2, t.P2.Preds.Length);
                Assert.Equal(2, t.P2.Handlers.Length);

                // Prove P1 refers to its own array instance (not resized/mutated):
                var v2 = 2;
                Assert.True(t.P1.Preds[0](in v2));
                Assert.Equal("even", t.P1.Handlers[0](in v2));
                return true;
            })
            .AssertPassed();
    }
}
