using PatternKit.Application.TableDataGateway;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.TableDataGateway;

[Feature("Table Data Gateway")]
public sealed partial class TableDataGatewayTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Table Data Gateway stores queries updates and deletes rows")]
    [Fact]
    public Task Table_Data_Gateway_Stores_Queries_Updates_And_Deletes_Rows()
        => Given("an orders table gateway", () => InMemoryTableDataGateway<OrderRow, string>.Create("orders", static row => row.OrderId).Build())
        .When("rows are inserted updated queried and deleted", (Func<ITableDataGateway<OrderRow, string>, ValueTask<TableGatewayScenario>>)(async gateway =>
        {
            var insert = await gateway.InsertAsync(new OrderRow("order-100", "Pending", 125m));
            var duplicate = await gateway.InsertAsync(new OrderRow("order-100", "Pending", 125m));
            var update = await gateway.UpdateAsync(new OrderRow("order-100", "Closed", 125m));
            var closed = await gateway.QueryAsync(static row => row.Status == "Closed");
            var delete = await gateway.DeleteAsync("order-100");
            var missing = await gateway.GetAsync("order-100");
            return new(insert, duplicate, update, closed, delete, missing);
        }))
        .Then("the table gateway enforces row keys and mutations", result =>
        {
            ScenarioExpect.Equal(TableGatewayStatus.Inserted, result.Insert.Status);
            ScenarioExpect.Equal(TableGatewayStatus.Conflict, result.Duplicate.Status);
            ScenarioExpect.Equal(TableGatewayStatus.Updated, result.Update.Status);
            ScenarioExpect.Equal("Closed", ScenarioExpect.Single(result.ClosedRows).Status);
            ScenarioExpect.Equal(TableGatewayStatus.Deleted, result.Delete.Status);
            ScenarioExpect.Null(result.Missing);
        })
        .AssertPassed();

    [Scenario("Table Data Gateway validates required configuration")]
    [Fact]
    public Task Table_Data_Gateway_Validates_Required_Configuration()
        => Given("table gateway builders", () => true)
        .Then("invalid arguments are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => InMemoryTableDataGateway<OrderRow, string>.Create("", static row => row.OrderId));
            ScenarioExpect.Throws<ArgumentNullException>(() => InMemoryTableDataGateway<OrderRow, string>.Create("orders", null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => InMemoryTableDataGateway<OrderRow, string>.Create("orders", static row => row.OrderId).UseComparer(null!));
            var gateway = InMemoryTableDataGateway<OrderRow, string>.Create("orders", static row => row.OrderId).Build();
            ScenarioExpect.Throws<ArgumentNullException>(() => gateway.InsertAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentNullException>(() => gateway.GetAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentNullException>(() => gateway.QueryAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentException>(() => TableGatewayResult<OrderRow>.Conflict(new OrderRow("order-1", "Pending", 10m), ""));
            ScenarioExpect.Throws<ArgumentException>(() => TableGatewayResult<OrderRow>.Missing(null, ""));
        })
        .AssertPassed();

    private sealed record OrderRow(string OrderId, string Status, decimal Total);

    private sealed record TableGatewayScenario(
        TableGatewayResult<OrderRow> Insert,
        TableGatewayResult<OrderRow> Duplicate,
        TableGatewayResult<OrderRow> Update,
        IReadOnlyList<OrderRow> ClosedRows,
        TableGatewayResult<OrderRow> Delete,
        OrderRow? Missing);
}
