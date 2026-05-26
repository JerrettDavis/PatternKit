using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;

namespace PatternKit.Examples.Messaging;

public sealed record InventoryMaintenanceCommand(string Sku, string Reason, DateTimeOffset ReceivedAt);

public sealed record InventoryChannelPurgeSummary(
    int PurgedCount,
    int RemainingCount,
    IReadOnlyList<string> PurgedSkus,
    IReadOnlyList<string> AuditTrail);

public sealed class InventoryChannelPurgerService(
    MessageChannel<InventoryMaintenanceCommand> channel,
    ChannelPurger<InventoryMaintenanceCommand> purger,
    InventoryChannelPurgeAudit audit)
{
    public void Enqueue(InventoryMaintenanceCommand command)
        => channel.Send(Message<InventoryMaintenanceCommand>.Create(command).WithCorrelationId(command.Sku));

    public InventoryChannelPurgeSummary PurgeMaintenanceBacklog()
    {
        var result = purger.Purge();
        return new(
            result.PurgedCount,
            result.RemainingCount,
            result.PurgedMessages.Select(message => message.Payload.Sku).ToArray(),
            audit.Entries);
    }
}

public sealed class InventoryChannelPurgeAudit
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Record(ChannelPurgeRecord<InventoryMaintenanceCommand> record)
        => _entries.Add($"{record.ChannelName}:{record.Message.Payload.Sku}:{record.Message.Payload.Reason}");
}

public static class InventoryChannelPurgers
{
    public static ChannelPurger<InventoryMaintenanceCommand> Create(
        MessageChannel<InventoryMaintenanceCommand> channel,
        InventoryChannelPurgeAudit audit)
        => ChannelPurger<InventoryMaintenanceCommand>.Create("inventory-maintenance-purger")
            .From(channel)
            .AuditWith(audit.Record)
            .Build();
}

[GenerateChannelPurger(typeof(InventoryMaintenanceCommand), FactoryName = "Create", PurgerName = "inventory-maintenance-purger")]
public static partial class GeneratedInventoryChannelPurger;

public sealed class InventoryChannelPurgerExampleRunner(InventoryChannelPurgerService service)
{
    public InventoryChannelPurgeSummary RunGenerated(IEnumerable<InventoryMaintenanceCommand> commands)
    {
        foreach (var command in commands)
            service.Enqueue(command);

        return service.PurgeMaintenanceBacklog();
    }

    public static InventoryChannelPurgeSummary RunFluent(IEnumerable<InventoryMaintenanceCommand> commands)
    {
        var channel = CreateChannel();
        var audit = new InventoryChannelPurgeAudit();
        var service = new InventoryChannelPurgerService(channel, InventoryChannelPurgers.Create(channel, audit), audit);
        foreach (var command in commands)
            service.Enqueue(command);

        return service.PurgeMaintenanceBacklog();
    }

    public static MessageChannel<InventoryMaintenanceCommand> CreateChannel()
        => MessageChannel<InventoryMaintenanceCommand>.Create("inventory-maintenance")
            .WithCapacity(128)
            .Build();
}

public static class InventoryChannelPurgerExampleServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryChannelPurgerDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => InventoryChannelPurgerExampleRunner.CreateChannel());
        services.AddSingleton<InventoryChannelPurgeAudit>();
        services.AddSingleton(sp => GeneratedInventoryChannelPurger.Create(
            sp.GetRequiredService<MessageChannel<InventoryMaintenanceCommand>>()));
        services.AddSingleton<InventoryChannelPurgerService>();
        services.AddSingleton<InventoryChannelPurgerExampleRunner>();
        return services;
    }
}
