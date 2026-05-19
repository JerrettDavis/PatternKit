using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class MailboxExampleTests
{
    [Scenario("RunAsync ProcessesMessagesInOrder")]
    [Fact]
    public async Task RunAsync_ProcessesMessagesInOrder()
    {
        var processed = await MailboxExample.RunAsync();

        ScenarioExpect.Equal(["batch-42:prepare", "batch-42:ship"], processed);
    }
}
