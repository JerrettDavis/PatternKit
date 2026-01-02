using PatternKit.Examples.PrototypeDemo;
using static PatternKit.Examples.PrototypeDemo.PrototypeDemo;

namespace PatternKit.Examples.Tests.PrototypeDemoTests;

public sealed class PrototypeDemoTests
{
    [Fact]
    public void CharacterStats_Clone_Creates_Independent_Copy()
    {
        var original = new CharacterStats
        {
            Health = 100, Mana = 50, Strength = 10, Agility = 8, Intelligence = 5
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Health, clone.Health);

        clone.Health = 200;
        Assert.Equal(100, original.Health);
    }

    [Fact]
    public void Equipment_Clone_Creates_Independent_Copy()
    {
        var original = new Equipment
        {
            Weapon = "Sword", Armor = "Plate", Accessories = ["Ring", "Amulet"]
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.NotSame(original.Accessories, clone.Accessories);
        Assert.Equal(original.Weapon, clone.Weapon);
        Assert.Equal(original.Accessories.Count, clone.Accessories.Count);

        clone.Accessories.Add("Cape");
        Assert.Equal(2, original.Accessories.Count);
    }

    [Fact]
    public void GameCharacter_DeepClone_Creates_Independent_Copy()
    {
        var original = CreateWarriorPrototype();

        var clone = GameCharacter.DeepClone(original);

        Assert.NotSame(original, clone);
        Assert.NotEqual(original.Id, clone.Id);
        Assert.NotSame(original.Stats, clone.Stats);
        Assert.NotSame(original.Equipment, clone.Equipment);
        Assert.NotSame(original.Abilities, clone.Abilities);
        Assert.NotSame(original.Resistances, clone.Resistances);
    }

    [Fact]
    public void CreateWarriorPrototype_Has_Correct_Stats()
    {
        var warrior = CreateWarriorPrototype();

        Assert.Equal("Warrior", warrior.Name);
        Assert.Equal("Warrior", warrior.Class);
        Assert.Equal(150, warrior.Stats.Health);
        Assert.Equal(30, warrior.Stats.Mana);
        Assert.Equal(15, warrior.Stats.Strength);
        Assert.Equal("Iron Sword", warrior.Equipment.Weapon);
    }

    [Fact]
    public void CreateMagePrototype_Has_Correct_Stats()
    {
        var mage = CreateMagePrototype();

        Assert.Equal("Mage", mage.Name);
        Assert.Equal("Mage", mage.Class);
        Assert.Equal(80, mage.Stats.Health);
        Assert.Equal(150, mage.Stats.Mana);
        Assert.Equal(18, mage.Stats.Intelligence);
        Assert.Equal("Oak Staff", mage.Equipment.Weapon);
    }

    [Fact]
    public void CreateRoguePrototype_Has_Correct_Stats()
    {
        var rogue = CreateRoguePrototype();

        Assert.Equal("Rogue", rogue.Name);
        Assert.Equal("Rogue", rogue.Class);
        Assert.Equal(100, rogue.Stats.Health);
        Assert.Equal(16, rogue.Stats.Agility);
        Assert.Equal("Twin Daggers", rogue.Equipment.Weapon);
        Assert.Equal(50, rogue.Resistances["Poison"]);
    }

    [Fact]
    public void CreateCharacterFactory_Clones_Warrior()
    {
        var factory = CreateCharacterFactory();

        var warrior = factory.Create("warrior");

        Assert.Equal("Warrior", warrior.Name);
        Assert.Equal("Warrior", warrior.Class);
    }

    [Fact]
    public void CreateCharacterFactory_Clones_Mage()
    {
        var factory = CreateCharacterFactory();

        var mage = factory.Create("mage");

        Assert.Equal("Mage", mage.Name);
        Assert.Equal("Mage", mage.Class);
    }

    [Fact]
    public void CreateCharacterFactory_Clones_Rogue()
    {
        var factory = CreateCharacterFactory();

        var rogue = factory.Create("rogue");

        Assert.Equal("Rogue", rogue.Name);
        Assert.Equal("Rogue", rogue.Class);
    }

    [Fact]
    public void CreateCharacterFactory_Clones_EliteWarrior()
    {
        var factory = CreateCharacterFactory();

        var elite = factory.Create("elite-warrior");

        Assert.Equal("Elite Warrior", elite.Name);
        Assert.Equal(10, elite.Level);
        Assert.Equal(300, elite.Stats.Health); // 150 * 2
    }

    [Fact]
    public void CreateCharacterFactory_Clones_EliteMage()
    {
        var factory = CreateCharacterFactory();

        var elite = factory.Create("elite-mage");

        Assert.Equal("Archmage", elite.Name);
        Assert.Equal(15, elite.Level);
        Assert.Equal(450, elite.Stats.Mana); // 150 * 3
    }

    [Fact]
    public void CreateCharacterFactory_Clones_Boss()
    {
        var factory = CreateCharacterFactory();

        var boss = factory.Create("boss-dragon-knight");

        Assert.Equal("Dragon Knight", boss.Name);
        Assert.Equal(50, boss.Level);
        Assert.Equal(5000, boss.Stats.Health);
        Assert.Equal(80, boss.Stats.Strength);
        Assert.Equal("Dragonbone Greatsword", boss.Equipment.Weapon);
        Assert.Equal(100, boss.Resistances["Fire"]);
    }

    [Fact]
    public void CreateCharacterFactory_With_Mutation()
    {
        var factory = CreateCharacterFactory();

        var custom = factory.Create("warrior", c =>
        {
            c.Name = "Sir Galahad";
            c.Level = 25;
        });

        Assert.Equal("Sir Galahad", custom.Name);
        Assert.Equal(25, custom.Level);
    }

    [Fact]
    public void CreateCharacterFactory_Default_Returns_Warrior()
    {
        var factory = CreateCharacterFactory();

        var unknown = factory.Create("unknown-key");

        Assert.Equal("Warrior", unknown.Class);
    }

    [Fact]
    public void CreateNpcSpawner_Creates_Clones()
    {
        var goblin = new GameCharacter
        {
            Id = "goblin", Name = "Goblin", Class = "Monster", Level = 3,
            Stats = new CharacterStats { Health = 30 }
        };

        var spawner = CreateNpcSpawner(goblin);

        var spawn1 = spawner.Create();
        var spawn2 = spawner.Create();

        Assert.NotSame(spawn1, spawn2);
        Assert.Equal("Goblin", spawn1.Name);
        Assert.Equal("Goblin", spawn2.Name);
    }

    [Fact]
    public void CreateNpcSpawner_With_Mutation()
    {
        var goblin = new GameCharacter
        {
            Id = "goblin", Name = "Goblin", Class = "Monster", Level = 3,
            Stats = new CharacterStats { Health = 30 }
        };

        var spawner = CreateNpcSpawner(goblin);

        var spawn = spawner.Create(g => g.Name = "Elite Goblin");

        Assert.Equal("Elite Goblin", spawn.Name);
    }

    [Fact]
    public void GameCharacter_ToString_Works()
    {
        var warrior = CreateWarriorPrototype();

        var str = warrior.ToString();

        Assert.Contains("Warrior", str);
        Assert.Contains("HP:150", str);
        Assert.Contains("STR:15", str);
    }

    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.PrototypeDemo.PrototypeDemo.Run();
    }
}
