using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates a generated content-router factory for message routing.
/// </summary>
public static class ContentRouterGeneratorExample
{
    /// <summary>Routes a generated content-router example order.</summary>
    public static string Run(string channel)
        => GeneratedOrderRouter.Create().Route(Message<GeneratedOrder>.Create(new GeneratedOrder(channel)));
}

/// <summary>Generated content-router example payload.</summary>
public sealed record GeneratedOrder(string Channel);

/// <summary>Generated content-router demo routes.</summary>
[GenerateContentRouter(typeof(GeneratedOrder), typeof(string))]
public static partial class GeneratedOrderRouter
{
    private static bool IsWholesale(Message<GeneratedOrder> message, MessageContext context)
        => message.Payload.Channel == "wholesale";

    private static bool IsRetail(Message<GeneratedOrder> message, MessageContext context)
        => message.Payload.Channel == "retail";

    [ContentRoute("wholesale", 10, nameof(IsWholesale))]
    private static string Wholesale(Message<GeneratedOrder> message, MessageContext context) => "wholesale";

    [ContentRoute("retail", 20, nameof(IsRetail))]
    private static string Retail(Message<GeneratedOrder> message, MessageContext context) => "retail";

    [ContentRouteDefault]
    private static string Default(Message<GeneratedOrder> message, MessageContext context) => "default";
}
