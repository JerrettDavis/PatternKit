using BenchmarkDotNet.Attributes;
using PatternKit.Creational.Prototype;
using PatternKit.Examples.PrototypeDemo;
using PatternKit.Generators.Prototype;

namespace PatternKit.Benchmarks.Creational;

[BenchmarkCategory("Creational", "GoF", "Prototype")]
public class PrototypeBenchmarks
{
    private static readonly GeneratedPrototypeCharacter SourceGeneratedCharacter = new()
    {
        Id = "warrior-base",
        Name = "Warrior",
        Level = 1,
        Abilities = ["Slash", "Block"]
    };

    [Benchmark(Baseline = true, Description = "Fluent: create prototype registry")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Prototype<string, PrototypeDemo.GameCharacter> Fluent_CreatePrototypeRegistry()
        => PrototypeDemo.CreateCharacterFactory();

    [Benchmark(Description = "Generated: create prototype source")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedPrototypeCharacter Generated_CreatePrototypeSource()
        => new()
        {
            Id = SourceGeneratedCharacter.Id,
            Name = SourceGeneratedCharacter.Name,
            Level = SourceGeneratedCharacter.Level,
            Abilities = new List<string>(SourceGeneratedCharacter.Abilities)
        };

    [Benchmark(Description = "Fluent: clone character prototype")]
    [BenchmarkCategory("Fluent", "Execution")]
    public PrototypeDemo.GameCharacter Fluent_CloneCharacterPrototype()
        => PrototypeDemo.CreateCharacterFactory().Create("elite-warrior");

    [Benchmark(Description = "Generated: clone character prototype")]
    [BenchmarkCategory("Generated", "Execution")]
    public GeneratedPrototypeCharacter Generated_CloneCharacterPrototype()
        => SourceGeneratedCharacter.Clone();
}

[Prototype(Mode = PrototypeMode.Shallow)]
public sealed partial class GeneratedPrototypeCharacter
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Level { get; set; }

    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public List<string> Abilities { get; set; } = [];
}
