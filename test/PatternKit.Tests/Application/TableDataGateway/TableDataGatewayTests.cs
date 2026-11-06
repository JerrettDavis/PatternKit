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
            ScenarioExpect.Throws<ArgumentNullException>(() => gateway.UpdateAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentNullException>(() => gateway.DeleteAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentException>(() => TableGatewayResult<OrderRow>.Conflict(new OrderRow("order-1", "Pending", 10m), ""));
            ScenarioExpect.Throws<ArgumentException>(() => TableGatewayResult<OrderRow>.Missing(null, ""));
        })
        .AssertPassed();

    [Scenario("Table Data Gateway reports missing mutations")]
    [Fact]
    public Task Table_Data_Gateway_Reports_Missing_Mutations()
        => Given("an empty orders table gateway", () => InMemoryTableDataGateway<OrderRow, string>.Create("orders", static row => row.OrderId).Build())
        .When("missing rows are updated and deleted", (Func<InMemoryTableDataGateway<OrderRow, string>, ValueTask<MissingMutationScenario>>)(async gateway =>
        {
            var update = await gateway.UpdateAsync(new OrderRow("order-404", "Closed", 12m));
            var delete = await gateway.DeleteAsync("order-404");
            return new(update, delete);
        }))
        .Then("missing mutations include failure status and reasons", result =>
        {
            ScenarioExpect.False(result.Update.Succeeded);
            ScenarioExpect.False(result.Delete.Succeeded);
            ScenarioExpect.Equal(TableGatewayStatus.Missing, result.Update.Status);
            ScenarioExpect.Equal(TableGatewayStatus.Missing, result.Delete.Status);
            ScenarioExpect.Equal("order-404", result.Update.Row!.OrderId);
            ScenarioExpect.Null(result.Delete.Row);
            ScenarioExpect.Contains("order-404", result.Update.Reason!);
            ScenarioExpect.Contains("orders", result.Delete.Reason!);
        })
        .AssertPassed();

    [Scenario("Table Data Gateway lists rows with configured comparer")]
    [Fact]
    public Task Table_Data_Gateway_Lists_Rows_With_Configured_Comparer()
        => Given("a case insensitive orders table gateway", () => InMemoryTableDataGateway<OrderRow, string>
            .Create("orders", static row => row.OrderId)
            .UseComparer(StringComparer.OrdinalIgnoreCase)
            .Build())
        .When("rows are inserted listed and duplicated with different key casing", (Func<InMemoryTableDataGateway<OrderRow, string>, ValueTask<ListScenario>>)(async gateway =>
        {
            var insert = await gateway.InsertAsync(new OrderRow("ORDER-1", "Pending", 25m));
            var duplicate = await gateway.InsertAsync(new OrderRow("order-1", "Pending", 25m));
            var rows = await gateway.ListAsync();
            return new(insert, duplicate, rows);
        }))
        .Then("the custom comparer controls key identity", result =>
        {
            ScenarioExpect.True(result.Insert.Succeeded);
            ScenarioExpect.False(result.Duplicate.Succeeded);
            ScenarioExpect.Equal(TableGatewayStatus.Conflict, result.Duplicate.Status);
            ScenarioExpect.Equal("ORDER-1", ScenarioExpect.Single(result.Rows).OrderId);
        })
        .AssertPassed();

    [Scenario("Table Data Gateway honors cancellation")]
    [Fact]
    public Task Table_Data_Gateway_Honors_Cancellation()
        => Given("a canceled token and orders table gateway", () =>
        {
            var source = new CancellationTokenSource();
            source.Cancel();
            var gateway = InMemoryTableDataGateway<OrderRow, string>.Create("orders", static row => row.OrderId).Build();
            return new CanceledGatewayScenario(gateway, source.Token);
        })
        .Then("operations throw cancellation before mutating rows", scenario =>
        {
            ScenarioExpect.Throws<OperationCanceledException>(() => scenario.Gateway.InsertAsync(new OrderRow("order-1", "Pending", 10m), scenario.Token).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<OperationCanceledException>(() => scenario.Gateway.GetAsync("order-1", scenario.Token).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<OperationCanceledException>(() => scenario.Gateway.ListAsync(scenario.Token).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<OperationCanceledException>(() => scenario.Gateway.QueryAsync(static row => row.Status == "Pending", scenario.Token).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<OperationCanceledException>(() => scenario.Gateway.UpdateAsync(new OrderRow("order-1", "Closed", 10m), scenario.Token).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<OperationCanceledException>(() => scenario.Gateway.DeleteAsync("order-1", scenario.Token).AsTask().GetAwaiter().GetResult());
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

    private sealed record MissingMutationScenario(TableGatewayResult<OrderRow> Update, TableGatewayResult<OrderRow> Delete);

    private sealed record ListScenario(TableGatewayResult<OrderRow> Insert, TableGatewayResult<OrderRow> Duplicate, IReadOnlyList<OrderRow> Rows);

    private sealed record CanceledGatewayScenario(InMemoryTableDataGateway<OrderRow, string> Gateway, CancellationToken Token);
}
