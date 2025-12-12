using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public sealed class MessagingModule : IAppModule
{
    public void Configure(IHostApplicationBuilder builder, IList<string> log)
    {
        builder.Services.AddSingleton<INotificationPublisher, QueueNotificationPublisher>();
        builder.Services.AddOptions<NotificationOptions>();
        log.Add("module:messaging");
    }
}