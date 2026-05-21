using PatternKit.Application.AuditLog;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.AuditLog;

[Feature("Audit Log")]
public sealed partial class AuditLogTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Audit Log appends and queries immutable entries")]
    [Fact]
    public Task Audit_Log_Appends_And_Queries_Immutable_Entries()
        => Given("an order audit log", () => InMemoryAuditLog<OrderAuditEntry, string>
            .Create("order-audit", static entry => entry.EntryId)
            .Build())
        .When("entries are appended and queried", (Func<IAuditLog<OrderAuditEntry, string>, ValueTask<AuditScenario>>)(async log =>
        {
            var submitted = new OrderAuditEntry("audit-1", "order-100", "submitted", "api");
            var approved = new OrderAuditEntry("audit-2", "order-100", "approved", "risk");
            var appendSubmitted = await log.AppendAsync(submitted);
            var appendApproved = await log.AppendAsync(approved);
            var duplicate = await log.AppendAsync(submitted);
            var orderEntries = await log.QueryAsync(static entry => entry.OrderId == "order-100");
            var loaded = await log.GetAsync(approved.EntryId);
            return new(appendSubmitted, appendApproved, duplicate, orderEntries, loaded);
        }))
        .Then("the log preserves append order and rejects duplicate entry keys", result =>
        {
            ScenarioExpect.True(result.Submitted.Appended);
            ScenarioExpect.True(result.Approved.Appended);
            ScenarioExpect.Equal(AuditLogAppendStatus.Duplicate, result.Duplicate.Status);
            ScenarioExpect.Equal(["submitted", "approved"], result.OrderEntries.Select(static entry => entry.Action));
            ScenarioExpect.Equal("approved", result.Loaded?.Action);
        })
        .AssertPassed();

    [Scenario("Audit Log validates required configuration")]
    [Fact]
    public Task Audit_Log_Validates_Required_Configuration()
        => Given("audit log builders", () => true)
        .Then("invalid arguments are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => InMemoryAuditLog<OrderAuditEntry, string>.Create("", static entry => entry.EntryId));
            ScenarioExpect.Throws<ArgumentNullException>(() => InMemoryAuditLog<OrderAuditEntry, string>.Create("audit", null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => InMemoryAuditLog<OrderAuditEntry, string>.Create("audit", static entry => entry.EntryId).UseComparer(null!));
            ScenarioExpect.Throws<ArgumentException>(() => AuditLogAppendResult<OrderAuditEntry>.Duplicate(new OrderAuditEntry("audit-1", "order-1", "submitted", "api"), ""));
            var log = InMemoryAuditLog<OrderAuditEntry, string>.Create("audit", static entry => entry.EntryId).Build();
            ScenarioExpect.Throws<ArgumentNullException>(() => log.AppendAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentNullException>(() => log.GetAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentNullException>(() => log.QueryAsync(null!).AsTask().GetAwaiter().GetResult());
        })
        .AssertPassed();

    private sealed record OrderAuditEntry(string EntryId, string OrderId, string Action, string Actor);

    private sealed record AuditScenario(
        AuditLogAppendResult<OrderAuditEntry> Submitted,
        AuditLogAppendResult<OrderAuditEntry> Approved,
        AuditLogAppendResult<OrderAuditEntry> Duplicate,
        IReadOnlyList<OrderAuditEntry> OrderEntries,
        OrderAuditEntry? Loaded);
}
