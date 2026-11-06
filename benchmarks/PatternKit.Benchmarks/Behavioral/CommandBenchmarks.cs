using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.Command;
using PatternKit.Generators.Command;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "GoF", "Command")]
public class CommandBenchmarks
{
    private readonly FulfillmentCommandService _handler = new();
    private static readonly ShipOrderCommand Request = new("SO-100", 3);

    [Benchmark(Baseline = true, Description = "Fluent: create command")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Command<FulfillmentContext> Fluent_CreateCommand()
        => Command<FulfillmentContext>.Create()
            .Do(static context => context.Reserve(3))
            .Undo(static context => context.Release(3))
            .Build();

    [Benchmark(Description = "Generated: create command handler")]
    [BenchmarkCategory("Generated", "Construction")]
    public FulfillmentCommandService Generated_CreateCommandHandler()
        => new();

    [Benchmark(Description = "Fluent: execute command")]
    [BenchmarkCategory("Fluent", "Execution")]
    public int Fluent_ExecuteCommand()
    {
        var context = new FulfillmentContext();
        Fluent_CreateCommand().Execute(context).GetAwaiter().GetResult();
        return context.Reserved;
    }

    [Benchmark(Description = "Generated: execute command")]
    [BenchmarkCategory("Generated", "Execution")]
    public int Generated_ExecuteCommand()
    {
        ShipOrderCommandCommand.Execute(_handler, in Request);
        return _handler.TotalReserved;
    }
}

public sealed class FulfillmentContext
{
    public int Reserved { get; private set; }

    public void Reserve(int quantity) => Reserved += quantity;

    public void Release(int quantity) => Reserved -= quantity;
}

[Command]
public readonly partial record struct ShipOrderCommand(string OrderId, int Quantity);

public sealed class FulfillmentCommandService
{
    public int TotalReserved { get; private set; }

    [CommandHandler]
    public void Handle(in ShipOrderCommand command) => TotalReserved += command.Quantity;
}
