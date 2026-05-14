using PatternKit.Examples.Messaging;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class MailboxExampleTests
{
    [Fact]
    public async Task RunAsync_ProcessesMessagesInOrder()
    {
        var processed = await MailboxExample.RunAsync();

        Assert.Equal(["batch-42:prepare", "batch-42:ship"], processed);
    }
}
