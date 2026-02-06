using PatternKit.Creational.Prototype;

namespace PatternKit.Examples.PrototypeDemo;

/// <summary>
/// Demonstrates the Prototype pattern for cloning complex configuration objects.
/// This example shows a game character configuration system with presets and mutations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-world scenario:</b> A game engine that needs to create many similar entities
/// (NPCs, enemies, projectiles) quickly without expensive initialization.
/// </para>
/// <para>
/// <b>Key GoF concepts demonstrated:</b>
/// <list type="bullet">
/// <item>Prototype registry - store named prototypes for quick cloning</item>
/// <item>Deep cloning - copies are independent of the original</item>
/// <item>Mutation chain - modify clones with fluent transformations</item>
/// </list>
/// </para>
/// </remarks>
public static class PrototypeDemo
{
    // ─────────────────────────────────────────────────────────────────────────
    // Game Entity Types - Complex objects to clone
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class CharacterStats
    {
        public int Health { get; set; }
        public int Mana { get; set; }
        public int Strength { get; set; }
        public int Agility { get; set; }
        public int Intelligence { get; set; }

        public CharacterStats Clone() => new()
        {
            Health = Health,
            Mana = Mana,
            Strength = Strength,
            Agility = Agility,
            Intelligence = Intelligence
        };
    }

    public sealed class Equipment
    {
        public string Weapon { get; set; } = "Fists";
        public string Armor { get; set; } = "Cloth";
        public List<string> Accessories { get; set; } = [];

        public Equipment Clone() => new()
        {
            Weapon = Weapon,
            Armor = Armor,
            Accessories = new List<string>(Accessories)
        };
    }

    public sealed class GameCharacter
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string Class { get; set; }
        public int Level { get; set; } = 1;
        public CharacterStats Stats { get; set; } = new();
        public Equipment Equipment { get; set; } = new();
        public List<string> Abilities { get; set; } = [];
        public Dictionary<string, int> Resistances { get; set; } = new();

        public static GameCharacter DeepClone(in GameCharacter source) => new()
        {
            Id = $"{source.Id}-{Guid.NewGuid():N}"[..16],
            Name = source.Name,
            Class = source.Class,
            Level = source.Level,
            Stats = source.Stats.Clone(),
            Equipment = source.Equipment.Clone(),
            Abilities = new List<string>(source.Abilities),
            Resistances = new Dictionary<string, int>(source.Resistances)
        };

        public override string ToString() =>
            $"{Name} (Lv.{Level} {Class}) - HP:{Stats.Health} MP:{Stats.Mana} " +
            $"STR:{Stats.Strength} AGI:{Stats.Agility} INT:{Stats.Intelligence}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Character Presets - Base prototypes
    // ─────────────────────────────────────────────────────────────────────────

    public static GameCharacter CreateWarriorPrototype() => new()
    {
        Id = "warrior-base",
        Name = "Warrior",
        Class = "Warrior",
        Level = 1,
        Stats = new CharacterStats
        {
            Health = 150,
            Mana = 30,
            Strength = 15,
            Agility = 8,
            Intelligence = 5
        },
        Equipment = new Equipment
        {
            Weapon = "Iron Sword",
            Armor = "Chainmail",
            Accessories = ["Shield"]
        },
        Abilities = ["Slash", "Block", "Charge"],
        Resistances = new Dictionary<string, int> { ["Physical"] = 20, ["Magic"] = -10 }
    };

    public static GameCharacter CreateMagePrototype() => new()
    {
        Id = "mage-base",
        Name = "Mage",
        Class = "Mage",
        Level = 1,
        Stats = new CharacterStats
        {
            Health = 80,
            Mana = 150,
            Strength = 5,
            Agility = 7,
            Intelligence = 18
        },
        Equipment = new Equipment
        {
            Weapon = "Oak Staff",
            Armor = "Silk Robes",
            Accessories = ["Spellbook", "Amulet"]
        },
        Abilities = ["Fireball", "Ice Shield", "Teleport"],
        Resistances = new Dictionary<string, int> { ["Physical"] = -10, ["Magic"] = 25 }
    };

    public static GameCharacter CreateRoguePrototype() => new()
    {
        Id = "rogue-base",
        Name = "Rogue",
        Class = "Rogue",
        Level = 1,
        Stats = new CharacterStats
        {
            Health = 100,
            Mana = 60,
            Strength = 10,
            Agility = 16,
            Intelligence = 10
        },
        Equipment = new Equipment
        {
            Weapon = "Twin Daggers",
            Armor = "Leather Vest",
            Accessories = ["Lockpicks", "Smoke Bombs"]
        },
        Abilities = ["Backstab", "Stealth", "Poison"],
        Resistances = new Dictionary<string, int> { ["Physical"] = 5, ["Magic"] = 5, ["Poison"] = 50 }
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Prototype Configuration using PatternKit
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a prototype registry with character class presets.
    /// </summary>
    public static Prototype<string, GameCharacter> CreateCharacterFactory()
    {
        return Prototype<string, GameCharacter>
            .Create()

            // Register base class prototypes
            .Map("warrior", CreateWarriorPrototype(), GameCharacter.DeepClone)
            .Map("mage", CreateMagePrototype(), GameCharacter.DeepClone)
            .Map("rogue", CreateRoguePrototype(), GameCharacter.DeepClone)

            // Register elite variants with mutations
            .Map("elite-warrior", CreateWarriorPrototype(), GameCharacter.DeepClone)
            .Mutate("elite-warrior", c => { c.Name = "Elite Warrior"; c.Level = 10; c.Stats.Health *= 2; })

            .Map("elite-mage", CreateMagePrototype(), GameCharacter.DeepClone)
            .Mutate("elite-mage", c => { c.Name = "Archmage"; c.Level = 15; c.Stats.Mana *= 3; })

            // Register boss variant
            .Map("boss-dragon-knight", CreateWarriorPrototype(), GameCharacter.DeepClone)
            .Mutate("boss-dragon-knight", c =>
            {
                c.Name = "Dragon Knight";
                c.Level = 50;
                c.Stats.Health = 5000;
                c.Stats.Strength = 80;
                c.Equipment.Weapon = "Dragonbone Greatsword";
                c.Equipment.Armor = "Dragon Scale Plate";
                c.Resistances["Fire"] = 100;
            })

            // Set default
            .Default(CreateWarriorPrototype(), GameCharacter.DeepClone)

            .Build();
    }

    /// <summary>
    /// Creates a single prototype for spawning NPCs.
    /// </summary>
    public static Prototype<GameCharacter> CreateNpcSpawner(GameCharacter baseNpc)
    {
        return Prototype<GameCharacter>
            .Create(baseNpc, GameCharacter.DeepClone)
            .Build();
    }

    private static void PrintCharacter(GameCharacter c)
    {
        Console.WriteLine($"    {c}");
        Console.WriteLine($"      Weapon: {c.Equipment.Weapon} | Armor: {c.Equipment.Armor}");
        Console.WriteLine($"      Abilities: {string.Join(", ", c.Abilities)}");
        Console.WriteLine($"      Resistances: {string.Join(", ", c.Resistances.Select(r => $"{r.Key}:{r.Value}%"))}");
    }

    /// <summary>
    /// Runs the complete Prototype pattern demonstration.
    /// </summary>
    public static void Run()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           PROTOTYPE PATTERN DEMONSTRATION                     ║");
        Console.WriteLine("║   Game Character Factory with Cloning and Mutations          ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

        var factory = CreateCharacterFactory();

        // ── Scenario 1: Clone base classes ──
        Console.WriteLine("▶ Scenario 1: Clone Base Character Classes");
        Console.WriteLine(new string('─', 50));

        var warrior = factory.Create("warrior");
        Console.WriteLine("  Created Warrior:");
        PrintCharacter(warrior);

        var mage = factory.Create("mage");
        Console.WriteLine("\n  Created Mage:");
        PrintCharacter(mage);

        var rogue = factory.Create("rogue");
        Console.WriteLine("\n  Created Rogue:");
        PrintCharacter(rogue);

        // ── Scenario 2: Clone with mutations ──
        Console.WriteLine("\n▶ Scenario 2: Clone with Runtime Mutations");
        Console.WriteLine(new string('─', 50));

        var customWarrior = factory.Create("warrior", c =>
        {
            c.Name = "Sir Galahad";
            c.Level = 25;
            c.Stats.Strength += 20;
            c.Equipment.Weapon = "Excalibur";
            c.Abilities.Add("Holy Strike");
        });
        Console.WriteLine("  Custom Warrior (with mutations):");
        PrintCharacter(customWarrior);

        // ── Scenario 3: Clone elite variants ──
        Console.WriteLine("\n▶ Scenario 3: Clone Pre-configured Elite Variants");
        Console.WriteLine(new string('─', 50));

        var eliteWarrior = factory.Create("elite-warrior");
        Console.WriteLine("  Elite Warrior:");
        PrintCharacter(eliteWarrior);

        var archmage = factory.Create("elite-mage");
        Console.WriteLine("\n  Archmage:");
        PrintCharacter(archmage);

        // ── Scenario 4: Clone boss ──
        Console.WriteLine("\n▶ Scenario 4: Clone Boss Character");
        Console.WriteLine(new string('─', 50));

        var boss = factory.Create("boss-dragon-knight");
        Console.WriteLine("  Dragon Knight Boss:");
        PrintCharacter(boss);

        // ── Scenario 5: Mass spawn NPCs ──
        Console.WriteLine("\n▶ Scenario 5: Mass Spawn NPCs (Performance Demo)");
        Console.WriteLine(new string('─', 50));

        var goblinPrototype = new GameCharacter
        {
            Id = "goblin",
            Name = "Goblin",
            Class = "Monster",
            Level = 3,
            Stats = new CharacterStats { Health = 30, Mana = 10, Strength = 8, Agility = 12, Intelligence = 3 },
            Equipment = new Equipment { Weapon = "Rusty Dagger", Armor = "Rags" },
            Abilities = ["Scratch", "Flee"]
        };

        var spawner = CreateNpcSpawner(goblinPrototype);

        Console.WriteLine("  Spawning 5 goblins with variations...");
        for (int i = 0; i < 5; i++)
        {
            var goblin = spawner.Create(g =>
            {
                g.Name = $"Goblin #{i + 1}";
                g.Stats.Health += Random.Shared.Next(-5, 10); // Slight variation
            });
            Console.WriteLine($"    [{goblin.Id[..8]}] {goblin.Name} HP:{goblin.Stats.Health}");
        }

        // ── Scenario 6: Verify independence ──
        Console.WriteLine("\n▶ Scenario 6: Verify Clone Independence");
        Console.WriteLine(new string('─', 50));

        var original = factory.Create("warrior");
        var clone = factory.Create("warrior", c => c.Name = "Clone Warrior");

        Console.WriteLine($"  Original: {original.Name}");
        Console.WriteLine($"  Clone: {clone.Name}");
        Console.WriteLine($"  Are same object? {ReferenceEquals(original, clone)} (should be False)");
        Console.WriteLine($"  Original unchanged? {original.Name == "Warrior"} (should be True)");

        Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
        Console.WriteLine("Pattern Benefits Demonstrated:");
        Console.WriteLine("  • Fast object creation via cloning (no expensive initialization)");
        Console.WriteLine("  • Named prototype registry for common configurations");
        Console.WriteLine("  • Mutation chains for customization at clone time");
        Console.WriteLine("  • Deep cloning ensures independence of copies");
        Console.WriteLine("  • Runtime flexibility - create variants without new classes");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }
}
