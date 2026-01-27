# Game Character Factory with Prototype Pattern

This demo shows how to build an efficient game entity spawning system using the Prototype pattern, demonstrating complex object cloning with nested structures, collections, and runtime mutations.

## What it demonstrates

- **Prototype registry** - store and retrieve named character templates
- **Deep cloning** - create independent copies of complex objects
- **Mutation chains** - customize clones at creation time
- **Performance** - avoid expensive initialization via cloning
- **Independence verification** - clones don't affect originals

## Where to look

- Code: `src/PatternKit.Examples/PrototypeDemo/PrototypeDemo.cs`
- Tests: `test/PatternKit.Tests/Creational/Prototype/PrototypeTests.cs`
- Generator: [Prototype Generator Documentation](../generators/prototype.md)

## Quick start

```csharp
using PatternKit.Creational.Prototype;

// Define your domain model
public sealed class GameCharacter
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Class { get; set; }
    public int Level { get; set; } = 1;
    public CharacterStats Stats { get; set; } = new();
    public Equipment Equipment { get; set; } = new();
    
    public static GameCharacter DeepClone(in GameCharacter source) => new()
    {
        Id = $"{source.Id}-{Guid.NewGuid():N}"[..16],
        Name = source.Name,
        Class = source.Class,
        Level = source.Level,
        Stats = source.Stats.Clone(),
        Equipment = source.Equipment.Clone()
    };
}

// Create a prototype registry
var factory = Prototype<string, GameCharacter>
    .Create()
    .Map("warrior", CreateWarriorPrototype(), GameCharacter.DeepClone)
    .Map("mage", CreateMagePrototype(), GameCharacter.DeepClone)
    .Build();

// Clone with optional mutations
var warrior = factory.Create("warrior");
var customWarrior = factory.Create("warrior", c => 
{
    c.Name = "Sir Galahad";
    c.Level = 25;
});
```

## The Problem

In game development, you often need to spawn many similar entities (NPCs, enemies, projectiles) with:

1. **Complex initialization** - nested objects (stats, equipment, abilities)
2. **Expensive setup** - loading assets, computing derived values
3. **Variations needed** - similar but not identical instances
4. **Performance critical** - spawn hundreds per frame

Traditional solutions have drawbacks:

```csharp
// ❌ Factory functions - run full initialization every time
public GameCharacter CreateWarrior()
{
    return new GameCharacter
    {
        Id = Guid.NewGuid().ToString(),
        Name = "Warrior",
        Stats = new CharacterStats { Health = 150, Strength = 15, /* ... */ },
        Equipment = new Equipment { Weapon = "Sword", /* ... */ },
        Abilities = ["Slash", "Block", "Charge"],
        // ... expensive initialization
    };
}

// ❌ Inheritance - class explosion for variants
public class Warrior : GameCharacter { }
public class EliteWarrior : Warrior { }
public class BossWarrior : Warrior { }
// ... one class per variant
```

## The Solution: Prototype Pattern

The Prototype pattern solves this by:

1. **Pre-configuring base prototypes** once
2. **Cloning them** (cheap memory copy)
3. **Mutating the clone** as needed

```csharp
// ✅ Configure once
var warriorPrototype = CreateWarriorPrototype();

// ✅ Clone many times (fast)
var w1 = warriorPrototype.Clone();
var w2 = warriorPrototype.Clone();

// ✅ Customize each clone
w1.Name = "Goblin Fighter #1";
w2.Name = "Goblin Fighter #2";
```

## Domain Model

### Character Statistics

```csharp
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
```

### Equipment System

```csharp
public sealed class Equipment
{
    public string Weapon { get; set; } = "Fists";
    public string Armor { get; set; } = "Cloth";
    public List<string> Accessories { get; set; } = [];

    public Equipment Clone() => new()
    {
        Weapon = Weapon,
        Armor = Armor,
        Accessories = new List<string>(Accessories) // New list, same items
    };
}
```

### Game Character

```csharp
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

    // Deep clone - recursively clones all nested objects
    public static GameCharacter DeepClone(in GameCharacter source) => new()
    {
        Id = $"{source.Id}-{Guid.NewGuid():N}"[..16], // Generate new ID
        Name = source.Name,
        Class = source.Class,
        Level = source.Level,
        Stats = source.Stats.Clone(), // Clone nested object
        Equipment = source.Equipment.Clone(), // Clone nested object
        Abilities = new List<string>(source.Abilities), // Clone collection
        Resistances = new Dictionary<string, int>(source.Resistances) // Clone dict
    };
}
```

**Key points:**
- `DeepClone` creates truly independent copies
- Each nested object is also cloned (not just the reference)
- Collections are copied (new collection, elements may be shared if immutable)
- IDs are regenerated to ensure uniqueness

## Base Prototypes

### Warrior Prototype

```csharp
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
    Resistances = new Dictionary<string, int> 
    { 
        ["Physical"] = 20, 
        ["Magic"] = -10 
    }
};
```

**Design:**
- High health and strength
- Low intelligence and mana
- Physical resistance, magic weakness

### Mage Prototype

```csharp
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
    Resistances = new Dictionary<string, int> 
    { 
        ["Physical"] = -10, 
        ["Magic"] = 25 
    }
};
```

**Design:**
- High mana and intelligence
- Low health and strength
- Magic resistance, physical weakness

### Rogue Prototype

```csharp
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
    Resistances = new Dictionary<string, int> 
    { 
        ["Physical"] = 5, 
        ["Magic"] = 5, 
        ["Poison"] = 50 
    }
};
```

**Design:**
- High agility for speed/evasion
- Balanced stats
- Poison immunity

## Prototype Registry Setup

### Basic Character Factory

```csharp
public static Prototype<string, GameCharacter> CreateCharacterFactory()
{
    return Prototype<string, GameCharacter>
        .Create()
        
        // Register base class prototypes
        .Map("warrior", CreateWarriorPrototype(), GameCharacter.DeepClone)
        .Map("mage", CreateMagePrototype(), GameCharacter.DeepClone)
        .Map("rogue", CreateRoguePrototype(), GameCharacter.DeepClone)
        
        // Register elite variants with pre-configured mutations
        .Map("elite-warrior", CreateWarriorPrototype(), GameCharacter.DeepClone)
        .Mutate("elite-warrior", c => 
        { 
            c.Name = "Elite Warrior"; 
            c.Level = 10; 
            c.Stats.Health *= 2; 
        })
        
        .Map("elite-mage", CreateMagePrototype(), GameCharacter.DeepClone)
        .Mutate("elite-mage", c => 
        { 
            c.Name = "Archmage"; 
            c.Level = 15; 
            c.Stats.Mana *= 3; 
        })
        
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
        
        // Set default fallback
        .Default(CreateWarriorPrototype(), GameCharacter.DeepClone)
        
        .Build();
}
```

**Key features:**
- **Named templates** - retrieve by key ("warrior", "mage", etc.)
- **Pre-configured mutations** - elite variants have baked-in transformations
- **Default fallback** - returned if key not found
- **Fluent API** - chain configuration calls

### Single Prototype Spawner

For mass-spawning many copies of the same entity:

```csharp
public static Prototype<GameCharacter> CreateNpcSpawner(GameCharacter baseNpc)
{
    return Prototype<GameCharacter>
        .Create(baseNpc, GameCharacter.DeepClone)
        .Build();
}
```

## Usage Scenarios

### Scenario 1: Clone Base Classes

```csharp
var factory = CreateCharacterFactory();

var warrior = factory.Create("warrior");
Console.WriteLine(warrior); 
// Output: Warrior (Lv.1 Warrior) - HP:150 MP:30 STR:15 AGI:8 INT:5

var mage = factory.Create("mage");
Console.WriteLine(mage);
// Output: Mage (Lv.1 Mage) - HP:80 MP:150 STR:5 AGI:7 INT:18
```

**What happens:**
1. Registry looks up "warrior" key
2. Calls `GameCharacter.DeepClone()` on the stored prototype
3. Returns independent copy
4. Each clone gets a new unique ID

### Scenario 2: Clone with Runtime Mutations

```csharp
var customWarrior = factory.Create("warrior", c =>
{
    c.Name = "Sir Galahad";
    c.Level = 25;
    c.Stats.Strength += 20;
    c.Equipment.Weapon = "Excalibur";
    c.Abilities.Add("Holy Strike");
});

Console.WriteLine(customWarrior);
// Output: Sir Galahad (Lv.25 Warrior) - HP:150 MP:30 STR:35 AGI:8 INT:5
// Weapon: Excalibur | Armor: Chainmail
// Abilities: Slash, Block, Charge, Holy Strike
```

**What happens:**
1. Clone the base "warrior" prototype
2. Apply the mutation lambda to the clone
3. Return the customized instance

**Benefits:**
- Start from a base template
- Customize only what's needed
- No need to set every property manually

### Scenario 3: Clone Pre-configured Elite Variants

```csharp
var eliteWarrior = factory.Create("elite-warrior");
Console.WriteLine(eliteWarrior);
// Output: Elite Warrior (Lv.10 Warrior) - HP:300 MP:30 STR:15 AGI:8 INT:5

var archmage = factory.Create("elite-mage");
Console.WriteLine(archmage);
// Output: Archmage (Lv.15 Mage) - HP:80 MP:450 STR:5 AGI:7 INT:18
```

**What happens:**
1. Elite variants are stored with pre-applied mutations
2. When you clone "elite-warrior", you get the mutated version
3. No runtime mutation cost - it's baked into the prototype

**When to use:**
- Common variations you spawn frequently
- Avoid repeating the same mutations
- Performance optimization

### Scenario 4: Clone Boss Characters

```csharp
var boss = factory.Create("boss-dragon-knight");
Console.WriteLine(boss);
// Output: Dragon Knight (Lv.50 Warrior) - HP:5000 MP:30 STR:80 AGI:8 INT:5
// Weapon: Dragonbone Greatsword | Armor: Dragon Scale Plate
// Resistances: Physical:20%, Magic:-10%, Fire:100%
```

**Design pattern:**
- Bosses are heavily modified variants
- Start from a base class prototype (Warrior)
- Transform stats, equipment, abilities
- Still benefit from cloning mechanism

### Scenario 5: Mass Spawn NPCs

```csharp
// Create a single prototype
var goblinPrototype = new GameCharacter
{
    Id = "goblin", 
    Name = "Goblin", 
    Class = "Monster", 
    Level = 3,
    Stats = new CharacterStats 
    { 
        Health = 30, 
        Mana = 10, 
        Strength = 8, 
        Agility = 12, 
        Intelligence = 3 
    },
    Equipment = new Equipment { Weapon = "Rusty Dagger", Armor = "Rags" },
    Abilities = ["Scratch", "Flee"]
};

var spawner = CreateNpcSpawner(goblinPrototype);

// Spawn many with slight variations
Console.WriteLine("Spawning 5 goblins with variations...");
for (int i = 0; i < 5; i++)
{
    var goblin = spawner.Create(g =>
    {
        g.Name = $"Goblin #{i + 1}";
        g.Stats.Health += Random.Shared.Next(-5, 10); // Randomize
    });
    Console.WriteLine($"[{goblin.Id[..8]}] {goblin.Name} HP:{goblin.Stats.Health}");
}

// Output:
// [a7b2c3d4] Goblin #1 HP:27
// [e5f6g7h8] Goblin #2 HP:35
// [i9j0k1l2] Goblin #3 HP:32
// [m3n4o5p6] Goblin #4 HP:29
// [q7r8s9t0] Goblin #5 HP:38
```

**Performance benefits:**
- Clone operation is O(1) for value types, O(n) for collections
- No expensive initialization logic
- No asset loading or computation
- Can spawn hundreds per frame

### Scenario 6: Verify Clone Independence

```csharp
var original = factory.Create("warrior");
var clone = factory.Create("warrior", c => c.Name = "Clone Warrior");

Console.WriteLine($"Original: {original.Name}"); // "Warrior"
Console.WriteLine($"Clone: {clone.Name}"); // "Clone Warrior"
Console.WriteLine($"Are same object? {ReferenceEquals(original, clone)}"); // False
Console.WriteLine($"Original unchanged? {original.Name == "Warrior"}"); // True

// Verify deep independence
clone.Stats.Health = 999;
Console.WriteLine($"Original health: {original.Stats.Health}"); // Still 150
Console.WriteLine($"Clone health: {clone.Stats.Health}"); // 999

clone.Abilities.Add("New Ability");
Console.WriteLine($"Original abilities: {original.Abilities.Count}"); // Still 3
Console.WriteLine($"Clone abilities: {clone.Abilities.Count}"); // 4
```

**What this proves:**
- Clones are separate instances (different references)
- Mutations to the clone don't affect the original
- Nested objects (Stats, Equipment) are also independent
- Collections (Abilities, Resistances) are independent

## Before/After Comparison

### Before: Manual Creation

```csharp
// ❌ Manual creation - verbose, error-prone, expensive
public void SpawnEnemies()
{
    for (int i = 0; i < 100; i++)
    {
        var goblin = new GameCharacter
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Goblin {i}",
            Class = "Monster",
            Level = 3,
            Stats = new CharacterStats 
            { 
                Health = 30, 
                Mana = 10, 
                Strength = 8, 
                Agility = 12, 
                Intelligence = 3 
            },
            Equipment = new Equipment 
            { 
                Weapon = "Rusty Dagger", 
                Armor = "Rags",
                Accessories = []
            },
            Abilities = ["Scratch", "Flee"],
            Resistances = new Dictionary<string, int>()
        };
        
        // ... expensive initialization
        LoadAssets(goblin);
        ComputeDerivedStats(goblin);
        
        _entities.Add(goblin);
    }
}
```

**Problems:**
- 🐌 **Slow** - runs full initialization 100 times
- 📝 **Verbose** - lots of repetitive code
- 🐛 **Error-prone** - easy to miss a property
- 🚫 **Not reusable** - can't easily create variants

### After: Prototype Pattern

```csharp
// ✅ Prototype pattern - fast, concise, flexible
public void SpawnEnemies()
{
    // Configure prototype once
    var goblinPrototype = CreateGoblinPrototype();
    LoadAssets(goblinPrototype); // Load once
    ComputeDerivedStats(goblinPrototype); // Compute once
    
    var spawner = CreateNpcSpawner(goblinPrototype);
    
    // Clone 100 times (fast)
    for (int i = 0; i < 100; i++)
    {
        var goblin = spawner.Create(g => g.Name = $"Goblin {i}");
        _entities.Add(goblin);
    }
}
```

**Benefits:**
- ⚡ **Fast** - clone is much cheaper than initialization
- 📦 **Concise** - configuration in one place
- ✅ **Safe** - single source of truth
- 🔄 **Reusable** - clone with variations easily

## Pattern Benefits Demonstrated

### 1. Fast Object Creation

Cloning is significantly faster than running initialization logic:

```csharp
// Initialization: O(complexity)
var character = CreateAndInitializeCharacter(); // Slow

// Cloning: O(size)
var clone = prototype.Create(); // Fast
```

**When initialization is expensive:**
- Loading assets from disk
- Database queries
- Complex computations
- Network calls

**Cloning just copies memory** - much faster.

### 2. Named Prototype Registry

Store common configurations with semantic names:

```csharp
factory.Create("warrior");      // Get a warrior
factory.Create("elite-warrior"); // Get elite variant
factory.Create("boss-dragon-knight"); // Get boss
```

**Benefits:**
- **Self-documenting** - name describes what you get
- **Centralized** - all prototypes in one place
- **Easy to extend** - add new variants without changing calling code

### 3. Mutation Chains

Customize clones at creation time:

```csharp
var custom = factory.Create("warrior", c =>
{
    c.Name = "Custom Name";
    c.Level = 25;
    c.Stats.Strength *= 2;
});
```

**Benefits:**
- **Fluent** - readable customization
- **Flexible** - override any property
- **Composable** - chain multiple mutations

### 4. Deep Cloning Ensures Independence

Clones don't share mutable state with the original:

```csharp
var c1 = factory.Create("warrior");
var c2 = factory.Create("warrior");

c1.Stats.Health = 999;
Console.WriteLine(c2.Stats.Health); // Still 150, not affected
```

**Critical for:**
- Game entities (independent behavior)
- Undo/redo systems (save/restore state)
- Concurrent processing (no shared mutations)

### 5. Runtime Flexibility

Create variants without defining new classes:

```csharp
// No need for EliteWarrior, BossWarrior classes
// Just mutate the base prototype

factory.Map("custom-variant", basePrototype, clone)
       .Mutate("custom-variant", c => { /* custom logic */ });
```

**Reduces:**
- Class explosion
- Inheritance hierarchies
- Coupling between types

## Use Cases

### Game Development

- **Enemy spawning** - clone enemy templates
- **Particle systems** - clone particle configurations
- **Item generation** - clone base items with modifications
- **Character presets** - clone character templates

### Document Processing

- **Document templates** - clone base documents
- **Form pre-filling** - clone form configurations
- **Report generation** - clone report structures

### Configuration Management

- **Environment configs** - clone base configs per environment
- **User preferences** - clone default preferences
- **Feature flags** - clone flag sets

### Testing

- **Test fixtures** - clone base test data
- **Mock objects** - clone mock configurations
- **Test scenarios** - clone scenario templates

## Performance Notes

### Cloning is Fast

For the game character example:

- **Prototype creation:** Once per base type (~5-10 types)
- **Cloning cost:** O(n) where n = number of properties + collection sizes
- **vs. Initialization:** Often 10-100x faster when initialization involves I/O

### Memory Efficiency

```csharp
// Prototype stored once in registry
var prototype = CreateWarriorPrototype(); // ~1KB memory

// Each clone is independent but shallow for immutable data
var clone1 = prototype.Clone(); // ~1KB (deep copy)
var clone2 = prototype.Clone(); // ~1KB (deep copy)

// Strings are interned, so "Warrior" string is shared (but immutable)
```

### When NOT to Use Prototype

❌ **Simple value objects** - just use constructors:
```csharp
var point = new Point(x: 10, y: 20); // Simpler than cloning
```

❌ **Immutable types** - use `with` expressions (records):
```csharp
var p2 = p1 with { X = 20 }; // Built-in cloning
```

❌ **Very large objects** - cloning might be expensive:
```csharp
// If object is 100MB, cloning copies 100MB
// Consider lazy loading or copy-on-write strategies
```

## Code Generator Support

The Prototype pattern generator can automate clone method generation:

```csharp
using PatternKit.Generators.Prototype;

[Prototype]
public partial class GameCharacter
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public CharacterStats Stats { get; set; } = new();
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public Equipment Equipment { get; set; } = new();
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public List<string> Abilities { get; set; } = [];
}

// Generated automatically:
// public GameCharacter Clone() { /* implementation */ }
```

**Benefits:**
- No manual clone method writing
- Configurable per-member strategies
- Compile-time diagnostics for unsafe cloning
- Supports classes, structs, records

See [Prototype Generator Documentation](../generators/prototype.md) for details.

## Key Takeaways

✅ **Use prototypes when:**
- Object creation is expensive (I/O, computation)
- You need many similar but not identical instances
- Runtime flexibility is important (no new classes)
- You want a registry of common configurations

✅ **Remember:**
- Deep clone for independence (copy nested objects)
- Shallow clone may share references (understand implications)
- Mutations apply to the clone, not the prototype
- Verify clone independence in tests

✅ **Performance wins:**
- Initialization once, clone many times
- Memory copy is faster than logic execution
- Registry lookup is O(1)

## Run the Demo

```bash
# From the repo root
dotnet build PatternKit.slnx -c Debug
dotnet run --project src/PatternKit.Examples --framework net9.0

# Select "Prototype Demo" from the menu
```

## Further Reading

- [Prototype Generator](../generators/prototype.md) - Automated clone method generation
- [PatternKit.Creational.Prototype](../../src/PatternKit/Creational/Prototype/) - Runtime prototype API
- [Gang of Four Design Patterns](https://en.wikipedia.org/wiki/Design_Patterns) - Original pattern catalog
