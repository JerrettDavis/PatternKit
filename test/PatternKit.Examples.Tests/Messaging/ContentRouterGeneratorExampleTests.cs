using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ContentRouterGeneratorExampleTests
{
    [Scenario("Run RoutesGeneratedContentRouter")]
    [Theory]
    [InlineData("wholesale", "wholesale")]
    [InlineData("retail", "retail")]
    [InlineData("unknown", "default")]
    public void Run_RoutesGeneratedContentRouter(string channel, string expected)
    {
        ScenarioExpect.Equal(expected, ContentRouterGeneratorExample.Run(channel));
    }
}
