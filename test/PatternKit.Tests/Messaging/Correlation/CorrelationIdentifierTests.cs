using PatternKit.Messaging;
using PatternKit.Messaging.Correlation;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Messaging.Correlation;

[Feature("Correlation Identifier")]
public sealed partial class CorrelationIdentifierTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Ensure Preserves Existing Correlation")]
    [Fact]
    public Task Ensure_Preserves_Existing_Correlation()
        => Given("a correlated message", () => Message<Order>.Create(new("ord-1")).WithCorrelationId("corr-existing"))
            .When("the correlation identifier ensures the message", message =>
                CorrelationIdentifier<Order>.Create()
                    .GenerateWith(static () => "corr-new")
                    .Build()
                    .Ensure(message))
            .Then("the existing correlation id is preserved", result =>
                ScenarioExpect.Equal("corr-existing", result.Headers.CorrelationId))
            .AssertPassed();

    [Scenario("Ensure Selects Correlation From Payload")]
    [Fact]
    public Task Ensure_Selects_Correlation_From_Payload()
        => Given("an uncorrelated message", () => Message<Order>.Create(new("ord-42")))
            .When("the correlation identifier derives a key from the payload", message =>
                CorrelationIdentifier<Order>.Create()
                    .Select(static (candidate, _) => "order:" + candidate.Payload.Id)
                    .Build()
                    .Ensure(message))
            .Then("the message carries the selected correlation id", result =>
                ScenarioExpect.Equal("order:ord-42", result.Headers.CorrelationId))
            .AssertPassed();

    [Scenario("Ensure Falls Back To Generator")]
    [Fact]
    public Task Ensure_Falls_Back_To_Generator()
        => Given("an uncorrelated message with no selected id", () => Message<Order>.Create(new("ord-1")))
            .When("the selector returns no value", message =>
                CorrelationIdentifier<Order>.Create()
                    .Select(static (_, _) => null)
                    .GenerateWith(static () => "corr-generated")
                    .Build()
                    .Ensure(message))
            .Then("the generated correlation id is applied", result =>
                ScenarioExpect.Equal("corr-generated", result.Headers.CorrelationId))
            .AssertPassed();

    [Scenario("Correlate Reply Copies Request Correlation")]
    [Fact]
    public Task CorrelateReply_Copies_Request_Correlation()
        => Given("a request and reply", () => (
                Request: Message<Order>.Create(new("ord-1")).WithCorrelationId("corr-request"),
                Reply: Message<OrderAccepted>.Create(new("ord-1", true))))
            .When("the reply is correlated", pair =>
                CorrelationIdentifier<Order>.Create()
                    .Build()
                    .CorrelateReply(pair.Reply, pair.Request))
            .Then("the reply carries the request correlation id", result =>
                ScenarioExpect.Equal("corr-request", result.Headers.CorrelationId))
            .AssertPassed();

    [Scenario("Builder Validates Configuration")]
    [Fact]
    public Task Builder_Validates_Configuration()
        => Given("a correlation identifier builder", CorrelationIdentifier<Order>.Create)
            .When("invalid configuration is applied", builder => new
            {
                Header = ScenarioExpect.Throws<ArgumentException>(() => builder.Header("")),
                Selector = ScenarioExpect.Throws<ArgumentNullException>(() => builder.Select(null!)),
                Generator = ScenarioExpect.Throws<ArgumentNullException>(() => builder.GenerateWith(null!))
            })
            .Then("all invalid settings are rejected", errors =>
            {
                ScenarioExpect.NotNull(errors.Header);
                ScenarioExpect.NotNull(errors.Selector);
                ScenarioExpect.NotNull(errors.Generator);
            })
            .AssertPassed();

    public sealed record Order(string Id);

    public sealed record OrderAccepted(string Id, bool Accepted);
}
