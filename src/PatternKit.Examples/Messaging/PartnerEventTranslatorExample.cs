using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates the Message Translator pattern with partner event normalization.
/// </summary>
public static class PartnerEventTranslatorExample
{
    public static PartnerOrderImportSummary RunFluent()
    {
        var translator = PartnerOrderTranslatorPolicies.CreateFluentTranslator();
        var message = CreatePartnerMessage("partner-a", "EXT-100", 125m);
        var result = translator.Translate(message);

        return PartnerOrderImportSummary.From("fluent", result);
    }

    public static PartnerOrderImportSummary RunGenerated()
    {
        var translator = GeneratedPartnerOrderTranslator.Create();
        var message = CreatePartnerMessage("partner-a", "EXT-100", 125m);
        var result = translator.Translate(message);

        return PartnerOrderImportSummary.From("source-generated", result);
    }

    public static Message<PartnerOrderAccepted> CreatePartnerMessage(string partnerId, string externalOrderId, decimal amount)
        => Message<PartnerOrderAccepted>
            .Create(new PartnerOrderAccepted(partnerId, externalOrderId, amount, "USD"))
            .WithMessageId($"partner:{externalOrderId}")
            .WithCorrelationId(externalOrderId)
            .WithHeader("partner-id", partnerId)
            .WithHeader("raw-signature", "demo-signature");
}

public static class PartnerOrderTranslatorPolicies
{
    public static MessageTranslator<PartnerOrderAccepted, CommerceOrderAccepted> CreateFluentTranslator()
        => MessageTranslator<PartnerOrderAccepted, CommerceOrderAccepted>
            .Create("partner-order-translator")
            .TranslateWith(static (message, _) => new CommerceOrderAccepted(
                $"commerce-{message.Payload.ExternalOrderId}",
                message.Payload.Amount,
                message.Payload.PartnerId))
            .DropHeader("raw-signature")
            .SetHeader(MessageHeaderNames.ContentType, "application/vnd.patternkit.commerce-order-accepted+json")
            .Build();
}

public sealed class PartnerOrderImportService(MessageTranslator<PartnerOrderAccepted, CommerceOrderAccepted> translator)
{
    public PartnerOrderImportSummary Import(Message<PartnerOrderAccepted> message)
        => PartnerOrderImportSummary.From("di", translator.Translate(message));
}

public static class PartnerEventTranslatorServiceCollectionExtensions
{
    public static IServiceCollection AddPartnerEventTranslatorExample(this IServiceCollection services)
    {
        services.AddSingleton(PartnerOrderTranslatorPolicies.CreateFluentTranslator());
        services.AddSingleton<PartnerOrderImportService>();
        services.AddSingleton(new PartnerEventTranslatorExampleRunner(
            PartnerEventTranslatorExample.RunFluent,
            PartnerEventTranslatorExample.RunGenerated));
        return services;
    }
}

public sealed record PartnerEventTranslatorExampleRunner(
    Func<PartnerOrderImportSummary> RunFluent,
    Func<PartnerOrderImportSummary> RunGenerated);

[GenerateMessageTranslator(typeof(PartnerOrderAccepted), typeof(CommerceOrderAccepted), TranslatorName = "partner-order-translator")]
[MessageTranslatorDropHeader("raw-signature")]
[MessageTranslatorHeader(MessageHeaderNames.ContentType, "application/vnd.patternkit.commerce-order-accepted+json")]
public static partial class GeneratedPartnerOrderTranslator
{
    [MessageTranslatorHandler]
    private static CommerceOrderAccepted Translate(Message<PartnerOrderAccepted> message, MessageContext context)
        => new($"commerce-{message.Payload.ExternalOrderId}", message.Payload.Amount, message.Payload.PartnerId);
}

public sealed record PartnerOrderAccepted(string PartnerId, string ExternalOrderId, decimal Amount, string Currency);
public sealed record CommerceOrderAccepted(string OrderId, decimal Total, string SourcePartnerId);

public sealed record PartnerOrderImportSummary(
    string Path,
    bool Accepted,
    string? OrderId,
    decimal? Total,
    string? SourcePartnerId,
    string? CorrelationId,
    string? ContentType,
    bool RawSignatureRemoved)
{
    public static PartnerOrderImportSummary From(string path, MessageTranslationResult<CommerceOrderAccepted> result)
    {
        var message = result.Message;
        return new(
            path,
            result.Translated,
            message?.Payload.OrderId,
            message?.Payload.Total,
            message?.Payload.SourcePartnerId,
            message?.Headers.CorrelationId,
            message?.Headers.ContentType,
            message?.Headers.ContainsKey("raw-signature") == false);
    }
}
