using PatternKit.Examples.StateDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.StateDemo;

public sealed class StateDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Happy path: pay -> ship -> deliver")]
    [Fact]
    public async Task Happy_Path()
    {
        await Given("order lifecycle demo", () => (Final: OrderStateDemo.OrderState.New, Log: new List<string>()))
            .When("run events pay, ship, deliver", _ => OrderStateDemo.Run("pay", "ship", "deliver"))
            .Then("final state Delivered and audited sequence recorded", r =>
                r.Final == OrderStateDemo.OrderState.Delivered &&
                string.Join(",", r.Log.ToArray()) == string.Join(",",
                    new[]{
                        "audit:new->","charge","notify:paid",
                        "audit:paid->","ship","notify:shipped",
                        "audit:shipped->","deliver","notify:delivered"
                    }))
            .AssertPassed();
    }

    [Scenario("Cancellation and refund path")]
    [Fact]
    public async Task Cancel_Refund()
    {
        await Given("order", () => default(object?))
            .When("new -> cancel -> refund", _ => OrderStateDemo.Run("cancel", "refund"))
            .Then("final state Refunded and logs include notifications and refund", r =>
                r.Final == OrderStateDemo.OrderState.Refunded &&
                string.Join(",", r.Log.ToArray()) == string.Join(",",
                    new[]{
                        "audit:new->","cancel","notify:cancelled",
                        "refund","notify:refunded"
                    }))
            .AssertPassed();
    }

    [Scenario("Delivered ignores further events via default stay")]
    [Fact]
    public async Task Delivered_Ignores()
    {
        await Given("order delivered", () => OrderStateDemo.Run("pay", "ship", "deliver"))
            .When("send unknown event after delivered", _ => OrderStateDemo.Run("pay", "ship", "deliver", "x"))
            .Then("still Delivered and last step ignored", r => r.Final == OrderStateDemo.OrderState.Delivered && r.Log.Last() == "ignore")
            .AssertPassed();
    }
}
