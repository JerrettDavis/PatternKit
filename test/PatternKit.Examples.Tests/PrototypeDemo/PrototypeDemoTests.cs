using PatternKit.Examples.PrototypeDemo;
using static PatternKit.Examples.PrototypeDemo.PrototypeDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.PrototypeDemoTests;

public sealed class PrototypeDemoTests
{
    [Scenario("CharacterStats Clone Creates Independent Copy")]
    [Fact]
    public void CharacterStats_Clone_Creates_Independent_Copy()
    {
        var original = new CharacterStats
        {
            Health = 100,
            Mana = 50,
            Strength = 10,
            Agility = 8,
            Intelligence = 5
        };

        var clone = original.Clone();

        ScenarioExpect.NotSame(original, clone);
        ScenarioExpect.Equal(original.Health, clone.Health);

        clone.Health = 200;
        ScenarioExpect.Equal(100, original.Health);
    }

    [Scenario("Equipment Clone Creates Independent Copy")]
    [Fact]
    public void Equipment_Clone_Creates_Independent_Copy()
    {
        var original = new Equipment
        {
            Weapon = "Sword",
            Armor = "Plate",
            Accessories = ["Ring", "Amulet"]
        };

        var clone = original.Clone();

        ScenarioExpect.NotSame(original, clone);
        ScenarioExpect.NotSame(original.Accessories, clone.Accessories);
        ScenarioExpect.Equal(original.Weapon, clone.Weapon);
        ScenarioExpect.Equal(original.Accessories.Count, clone.Accessories.Count);

        clone.Accessories.Add("Cape");
        ScenarioExpect.Equal(2, original.Accessories.Count);
    }

    [Scenario("GameCharacter DeepClone Creates Independent Copy")]
    [Fact]
    public void GameCharacter_DeepClone_Creates_Independent_Copy()
    {
        var original = CreateWarriorPrototype();

        var clone = GameCharacter.DeepClone(original);

        ScenarioExpect.NotSame(original, clone);
        ScenarioExpect.NotEqual(original.Id, clone.Id);
        ScenarioExpect.NotSame(original.Stats, clone.Stats);
        ScenarioExpect.NotSame(original.Equipment, clone.Equipment);
        ScenarioExpect.NotSame(original.Abilities, clone.Abilities);
        ScenarioExpect.NotSame(original.Resistances, clone.Resistances);
    }

    [Scenario("CreateWarriorPrototype Has Correct Stats")]
    [Fact]
    public void CreateWarriorPrototype_Has_Correct_Stats()
    {
        var warrior = CreateWarriorPrototype();

        ScenarioExpect.Equal("Warrior", warrior.Name);
        ScenarioExpect.Equal("Warrior", warrior.Class);
        ScenarioExpect.Equal(150, warrior.Stats.Health);
        ScenarioExpect.Equal(30, warrior.Stats.Mana);
        ScenarioExpect.Equal(15, warrior.Stats.Strength);
        ScenarioExpect.Equal("Iron Sword", warrior.Equipment.Weapon);
    }

    [Scenario("CreateMagePrototype Has Correct Stats")]
    [Fact]
    public void CreateMagePrototype_Has_Correct_Stats()
    {
        var mage = CreateMagePrototype();

        ScenarioExpect.Equal("Mage", mage.Name);
        ScenarioExpect.Equal("Mage", mage.Class);
        ScenarioExpect.Equal(80, mage.Stats.Health);
        ScenarioExpect.Equal(150, mage.Stats.Mana);
        ScenarioExpect.Equal(18, mage.Stats.Intelligence);
        ScenarioExpect.Equal("Oak Staff", mage.Equipment.Weapon);
    }

    [Scenario("CreateRoguePrototype Has Correct Stats")]
    [Fact]
    public void CreateRoguePrototype_Has_Correct_Stats()
    {
        var rogue = CreateRoguePrototype();

        ScenarioExpect.Equal("Rogue", rogue.Name);
        ScenarioExpect.Equal("Rogue", rogue.Class);
        ScenarioExpect.Equal(100, rogue.Stats.Health);
        ScenarioExpect.Equal(16, rogue.Stats.Agility);
        ScenarioExpect.Equal("Twin Daggers", rogue.Equipment.Weapon);
        ScenarioExpect.Equal(50, rogue.Resistances["Poison"]);
    }

    [Scenario("CreateCharacterFactory Clones Warrior")]
    [Fact]
    public void CreateCharacterFactory_Clones_Warrior()
    {
        var factory = CreateCharacterFactory();

        var warrior = factory.Create("warrior");

        ScenarioExpect.Equal("Warrior", warrior.Name);
        ScenarioExpect.Equal("Warrior", warrior.Class);
    }

    [Scenario("CreateCharacterFactory Clones Mage")]
    [Fact]
    public void CreateCharacterFactory_Clones_Mage()
    {
        var factory = CreateCharacterFactory();

        var mage = factory.Create("mage");

        ScenarioExpect.Equal("Mage", mage.Name);
        ScenarioExpect.Equal("Mage", mage.Class);
    }

    [Scenario("CreateCharacterFactory Clones Rogue")]
    [Fact]
    public void CreateCharacterFactory_Clones_Rogue()
    {
        var factory = CreateCharacterFactory();

        var rogue = factory.Create("rogue");

        ScenarioExpect.Equal("Rogue", rogue.Name);
        ScenarioExpect.Equal("Rogue", rogue.Class);
    }

    [Scenario("CreateCharacterFactory Clones EliteWarrior")]
    [Fact]
    public void CreateCharacterFactory_Clones_EliteWarrior()
    {
        var factory = CreateCharacterFactory();

        var elite = factory.Create("elite-warrior");

        ScenarioExpect.Equal("Elite Warrior", elite.Name);
        ScenarioExpect.Equal(10, elite.Level);
        ScenarioExpect.Equal(300, elite.Stats.Health); // 150 * 2
    }

    [Scenario("CreateCharacterFactory Clones EliteMage")]
    [Fact]
    public void CreateCharacterFactory_Clones_EliteMage()
    {
        var factory = CreateCharacterFactory();

        var elite = factory.Create("elite-mage");

        ScenarioExpect.Equal("Archmage", elite.Name);
        ScenarioExpect.Equal(15, elite.Level);
        ScenarioExpect.Equal(450, elite.Stats.Mana); // 150 * 3
    }

    [Scenario("CreateCharacterFactory Clones Boss")]
    [Fact]
    public void CreateCharacterFactory_Clones_Boss()
    {
        var factory = CreateCharacterFactory();

        var boss = factory.Create("boss-dragon-knight");

        ScenarioExpect.Equal("Dragon Knight", boss.Name);
        ScenarioExpect.Equal(50, boss.Level);
        ScenarioExpect.Equal(5000, boss.Stats.Health);
        ScenarioExpect.Equal(80, boss.Stats.Strength);
        ScenarioExpect.Equal("Dragonbone Greatsword", boss.Equipment.Weapon);
        ScenarioExpect.Equal(100, boss.Resistances["Fire"]);
    }

    [Scenario("CreateCharacterFactory With Mutation")]
    [Fact]
    public void CreateCharacterFactory_With_Mutation()
    {
        var factory = CreateCharacterFactory();

        var custom = factory.Create("warrior", c =>
        {
            c.Name = "Sir Galahad";
            c.Level = 25;
        });

        ScenarioExpect.Equal("Sir Galahad", custom.Name);
        ScenarioExpect.Equal(25, custom.Level);
    }

    [Scenario("CreateCharacterFactory Default Returns Warrior")]
    [Fact]
    public void CreateCharacterFactory_Default_Returns_Warrior()
    {
        var factory = CreateCharacterFactory();

        var unknown = factory.Create("unknown-key");

        ScenarioExpect.Equal("Warrior", unknown.Class);
    }

    [Scenario("CreateNpcSpawner Creates Clones")]
    [Fact]
    public void CreateNpcSpawner_Creates_Clones()
    {
        var goblin = new GameCharacter
        {
            Id = "goblin",
            Name = "Goblin",
            Class = "Monster",
            Level = 3,
            Stats = new CharacterStats { Health = 30 }
        };

        var spawner = CreateNpcSpawner(goblin);

        var spawn1 = spawner.Create();
        var spawn2 = spawner.Create();

        ScenarioExpect.NotSame(spawn1, spawn2);
        ScenarioExpect.Equal("Goblin", spawn1.Name);
        ScenarioExpect.Equal("Goblin", spawn2.Name);
    }

    [Scenario("CreateNpcSpawner With Mutation")]
    [Fact]
    public void CreateNpcSpawner_With_Mutation()
    {
        var goblin = new GameCharacter
        {
            Id = "goblin",
            Name = "Goblin",
            Class = "Monster",
            Level = 3,
            Stats = new CharacterStats { Health = 30 }
        };

        var spawner = CreateNpcSpawner(goblin);

        var spawn = spawner.Create(g => g.Name = "Elite Goblin");

        ScenarioExpect.Equal("Elite Goblin", spawn.Name);
    }

    [Scenario("GameCharacter ToString Works")]
    [Fact]
    public void GameCharacter_ToString_Works()
    {
        var warrior = CreateWarriorPrototype();

        var str = warrior.ToString();

        ScenarioExpect.Contains("Warrior", str);
        ScenarioExpect.Contains("HP:150", str);
        ScenarioExpect.Contains("STR:15", str);
    }

    [Scenario("Run Executes Without Errors")]
    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.PrototypeDemo.PrototypeDemo.Run();
    }
}
