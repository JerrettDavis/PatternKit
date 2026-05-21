using PatternKit.Application.DataMapping;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.DataMapping;

[Feature("Data Mapper")]
public sealed partial class DataMapperTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Data Mapper maps domain models and data records in both directions")]
    [Fact]
    public Task Data_Mapper_Maps_Domain_Models_And_Data_Records_In_Both_Directions()
        => Given("a mapper between orders and persistence records", CreateMapper)
            .When(
                "mapping a domain order to data and back",
                (Func<DataMapper<Order, OrderRow>, Task<MappedOrderRoundTrip>>)(async mapper =>
                {
                    var data = await mapper.ToDataAsync(new Order("order-100", 125m, true));
                    var domain = await mapper.ToDomainAsync(data.Value!);
                    return new MappedOrderRoundTrip(data, domain);
                }))
            .Then("the data record is isolated from the domain shape", result =>
            {
                ScenarioExpect.True(result.Data.Succeeded);
                ScenarioExpect.Equal("order-100", result.Data.Value!.OrderId);
                ScenarioExpect.Equal("Paid", result.Data.Value.Status);
            })
            .And("the domain model round-trips through the mapper", result =>
            {
                ScenarioExpect.True(result.Domain.Succeeded);
                ScenarioExpect.Equal(125m, result.Domain.Value!.Total);
                ScenarioExpect.True(result.Domain.Value.Paid);
            })
            .AssertPassed();

    [Scenario("Data Mapper returns validation errors before mapping invalid models")]
    [Fact]
    public Task Data_Mapper_Returns_Validation_Errors_Before_Mapping_Invalid_Models()
        => Given("a mapper with domain validation", CreateMapper)
            .When("mapping an invalid domain order", mapper => mapper.ToDataAsync(new Order("", 10m, false)).AsTask().GetAwaiter().GetResult())
            .Then("mapping fails with the validation error", result =>
            {
                ScenarioExpect.False(result.Succeeded);
                var error = ScenarioExpect.Single(result.Errors);
                ScenarioExpect.Equal("order-id-required", error.Code);
            })
            .AssertPassed();

    [Scenario("Data Mapper builder requires both projections")]
    [Fact]
    public Task Data_Mapper_Builder_Requires_Both_Projections()
        => Given("a mapper builder with only one projection", () => DataMapper<Order, OrderRow>.Create()
            .MapToData(static order => new OrderRow(order.Id, order.Total, order.Paid ? "Paid" : "Pending")))
            .Then("building fails clearly", builder =>
                ScenarioExpect.Throws<InvalidOperationException>(() => builder.Build()))
            .AssertPassed();

    [Scenario("Data Mapper honors cancellation")]
    [Fact]
    public Task Data_Mapper_Honors_Cancellation()
        => Given("a canceled token and mapper", () =>
            {
                using var cts = new CancellationTokenSource();
                cts.Cancel();
                return new { Mapper = CreateMapper(), Token = cts.Token };
            })
            .Then("mapping observes cancellation", ctx =>
                ScenarioExpect.Throws<OperationCanceledException>(() =>
                    ctx.Mapper.ToDataAsync(new Order("order-100", 10m, false), ctx.Token).AsTask().GetAwaiter().GetResult()))
            .AssertPassed();

    private static DataMapper<Order, OrderRow> CreateMapper()
        => DataMapper<Order, OrderRow>.Create()
            .MapToData(static order => new OrderRow(order.Id, order.Total, order.Paid ? "Paid" : "Pending"))
            .MapToDomain(static row => new Order(row.OrderId, row.TotalAmount, row.Status == "Paid"))
            .ValidateDomain(static order => string.IsNullOrWhiteSpace(order.Id)
                ? new DataMapperError("order-id-required", "Order id is required.")
                : null)
            .ValidateData(static row => string.IsNullOrWhiteSpace(row.OrderId)
                ? new DataMapperError("order-id-required", "Order row id is required.")
                : null)
            .Build();

    private sealed record Order(string Id, decimal Total, bool Paid);

    private sealed record OrderRow(string OrderId, decimal TotalAmount, string Status);

    private sealed record MappedOrderRoundTrip(
        DataMapperResult<OrderRow> Data,
        DataMapperResult<Order> Domain);
}
