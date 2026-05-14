using PatternKit.Examples.Messaging;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ContentRouterGeneratorExampleTests
{
    [Theory]
    [InlineData("wholesale", "wholesale")]
    [InlineData("retail", "retail")]
    [InlineData("unknown", "default")]
    public void Run_RoutesGeneratedContentRouter(string channel, string expected)
    {
        Assert.Equal(expected, ContentRouterGeneratorExample.Run(channel));
    }
}
