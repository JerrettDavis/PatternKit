# Prototype Generator

The Prototype generator creates GoF-aligned clone methods with configurable strategies for safe object duplication. It requires only the `PatternKit.Generators` package at compile time—no runtime dependency on PatternKit.

> Modes: **Shallow with Warnings** (safe-by-default with diagnostics), **Shallow** (explicit shallow cloning), and **DeepWhenPossible** (attempts deep cloning for known types).

## Quickstart: Class Cloning

```csharp
using PatternKit.Generators.Prototype;

[Prototype]
public partial class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public List<string> Hobbies { get; set; } = new();
}
```

Generated shape (essentials):

- `Person Clone()` — creates a shallow clone of the instance
- Warnings for mutable reference types (e.g., `List<string>` copied by reference)
- Works with classes, structs, record classes, and record structs

Usage:

```csharp
var original = new Person { Name = "Alice", Age = 30 };
var clone = original.Clone();

clone.Name = "Bob";
Console.WriteLine(original.Name); // Still "Alice"
```

## Quickstart: Record Cloning

```csharp
using PatternKit.Generators.Prototype;

[Prototype]
public partial record class Person(string Name, int Age);
```

Generated shape:

- `Person Duplicate()` — for records, default method name is "Duplicate" to avoid conflicts with compiler-generated `Clone()`
- Uses record `with` expressions for efficient cloning
- Override method name with `CloneMethodName` if needed

Usage:

```csharp
var original = new Person("Alice", 30);
var clone = original.Duplicate();

Console.WriteLine(clone.Name); // "Alice"
Console.WriteLine(ReferenceEquals(original, clone)); // False
```

## Basic Usage Examples

### Class with Value Types

```csharp
[Prototype]
public partial class Vector
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

var v1 = new Vector { X = 1, Y = 2, Z = 3 };
var v2 = v1.Clone(); // Deep copy - all value types
```

### Struct

```csharp
[Prototype]
public partial struct Point
{
    public int X { get; set; }
    public int Y { get; set; }
}

var p1 = new Point { X = 10, Y = 20 };
var p2 = p1.Clone();
```

### Record Class

```csharp
[Prototype]
public partial record class Customer(string Id, string Name, int Credits);

var c1 = new Customer("C001", "Alice", 100);
var c2 = c1.Duplicate(); // Uses record with-expression
```

### Record Struct

```csharp
[Prototype]
public partial record struct Temperature(double Celsius)
{
    public double Fahrenheit => Celsius * 9.0 / 5.0 + 32.0;
}

var t1 = new Temperature(25.0);
var t2 = t1.Duplicate();
```

## Configuration Options

### PrototypeMode

Controls default cloning behavior for reference types:

```csharp
// ShallowWithWarnings (default) - warns about mutable reference types
[Prototype(Mode = PrototypeMode.ShallowWithWarnings)]
public partial class Container
{
    public List<string> Items { get; set; } = new(); // WARNING: PKPRO003
}

// Shallow - no warnings, explicit shallow cloning
[Prototype(Mode = PrototypeMode.Shallow)]
public partial class Container
{
    public List<string> Items { get; set; } = new(); // No warning
}

// DeepWhenPossible - attempts deep cloning for known types
[Prototype(Mode = PrototypeMode.DeepWhenPossible)]
public partial class Container
{
    public List<string> Items { get; set; } = new(); // Creates new List<string>(original)
}
```

### CloneMethodName

Customize the generated method name:

```csharp
[Prototype(CloneMethodName = "Copy")]
public partial class Document
{
    public string Title { get; set; } = "";
}

var doc = new Document { Title = "Report" };
var copy = doc.Copy(); // Not Clone()
```

For records, the default is automatically "Duplicate" to avoid conflicts with compiler-generated `Clone()`:

```csharp
[Prototype] // CloneMethodName defaults to "Duplicate" for records
public partial record class Item(string Name);

[Prototype(CloneMethodName = "MakeCopy")] // Override if desired
public partial record class Item2(string Name);
```

### IncludeExplicit

Control member selection mode:

```csharp
// Default: IncludeAll mode - all members cloned unless marked [PrototypeIgnore]
[Prototype]
public partial class User
{
    public string Username { get; set; } = "";
    
    [PrototypeIgnore] // Excluded from clone
    public string Password { get; set; } = "";
}

// ExplicitOnly mode - only members marked [PrototypeInclude] are cloned
[Prototype(IncludeExplicit = true)]
public partial class Config
{
    [PrototypeInclude] // Included in clone
    public string ApiKey { get; set; } = "";
    
    public string InternalState { get; set; } = ""; // Not cloned
}
```

## Per-Member Cloning Strategies

Override the default strategy for specific members using `[PrototypeStrategy]`:

### ByReference

Copy the reference as-is (shallow copy):

```csharp
[Prototype]
public partial class Logger
{
    [PrototypeStrategy(PrototypeCloneStrategy.ByReference)]
    public ILogSink Sink { get; set; } = null!;
}
```

**When to use:**
- Immutable types (strings, records with readonly members)
- Shared services (loggers, database connections)
- Flyweight instances

**Warning:** For mutable types, changes affect both original and clone.

### ShallowCopy

Create a new collection with the same element references:

```csharp
[Prototype]
public partial class Playlist
{
    [PrototypeStrategy(PrototypeCloneStrategy.ShallowCopy)]
    public List<Song> Songs { get; set; } = new();
}

// Generated:
// Songs = new List<Song>(original.Songs)
```

**When to use:**
- Collections where you want independent collection instances
- Element references can be shared (immutable elements)

**Limitation:** Elements themselves are not cloned.

### Clone

Use a known clone mechanism (ICloneable, Clone() method, copy constructor, or collection copy constructor):

```csharp
public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    
    public Address Clone() => new() { Street = Street, City = City };
}

[Prototype]
public partial class Person
{
    public string Name { get; set; } = "";
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public Address HomeAddress { get; set; } = new();
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public List<string> PhoneNumbers { get; set; } = new();
}

// Generated:
// HomeAddress = original.HomeAddress.Clone()
// PhoneNumbers = new List<string>(original.PhoneNumbers)
```

**When to use:**
- Types with explicit Clone() methods
- Types implementing ICloneable
- Collections (List, Dictionary, HashSet, etc.)

**Generator checks:**
- For custom types: must have `Clone()` method or `ICloneable`
- For collections: uses copy constructor `new List<T>(original)`
- Emits **PKPRO004** error if no suitable mechanism found

### DeepCopy

**Status:** Not yet implemented – selecting this strategy will emit diagnostic **PKPRO007** ("DeepCopy strategy not yet implemented").

Planned for recursive deep cloning of complex object graphs.

### Custom

Provide your own cloning logic via partial method. You must declare and implement the partial method yourself:

```csharp
[Prototype]
public partial class GameEntity
{
    [PrototypeStrategy(PrototypeCloneStrategy.Custom)]
    public EntityStats Stats { get; set; } = new();
    
    // Declare the partial method
    private static partial EntityStats CloneStats(EntityStats value);
}

// Provide the implementation in your partial class:
public partial class GameEntity
{
    private static partial EntityStats CloneStats(EntityStats value)
    {
        return new EntityStats
        {
            Health = value.Health,
            Mana = value.Mana,
            // Custom logic here
        };
    }
}
```

**When to use:**
- Complex cloning logic not expressible with other strategies
- Integration with existing clone systems
- Performance-critical custom implementations

**Generator checks:**
- Emits **PKPRO005** error if partial method `private static partial TMember Clone{MemberName}(TMember value)` not declared and implemented

## Member Selection

### Default: IncludeAll Mode

All eligible members are cloned unless marked `[PrototypeIgnore]`:

```csharp
[Prototype]
public partial class Session
{
    public string SessionId { get; set; } = ""; // Cloned
    public DateTime CreatedAt { get; set; } // Cloned
    
    [PrototypeIgnore]
    public DateTime LastAccess { get; set; } // NOT cloned
    
    [PrototypeIgnore]
    public int AccessCount { get; set; } // NOT cloned
}
```

### ExplicitOnly Mode

Only members marked `[PrototypeInclude]` are cloned:

```csharp
[Prototype(IncludeExplicit = true)]
public partial class SecureConfig
{
    [PrototypeInclude]
    public string ApiEndpoint { get; set; } = ""; // Cloned
    
    [PrototypeInclude]
    public int Timeout { get; set; } // Cloned
    
    public string ApiSecret { get; set; } = ""; // NOT cloned
    public byte[] EncryptionKey { get; set; } = Array.Empty<byte>(); // NOT cloned
}
```

**When to use ExplicitOnly:**
- Security-sensitive types (secrets, keys)
- Large types where most members shouldn't be cloned
- Explicit opt-in clarity

## Clone Construction Strategies

The generator uses different strategies based on the type:

### Record with-expression (Highest Priority)

For record classes and record structs:

```csharp
[Prototype]
public partial record class Person(string Name, int Age);

// Generated:
public Person Duplicate()
{
    return this with { };
}
```

**Advantages:**
- Most efficient for records
- Preserves record semantics
- Compiler-optimized

### Copy Constructor

If a constructor accepting the same type exists:

```csharp
[Prototype]
public partial class Point
{
    public int X { get; set; }
    public int Y { get; set; }
    
    public Point() { }
    public Point(Point other) // Copy constructor detected
    {
        X = other.X;
        Y = other.Y;
    }
}

// Generated:
public Point Clone()
{
    return new Point(this);
}
```

### Parameterless Constructor + Assignment (Fallback)

When no copy constructor exists:

```csharp
[Prototype]
public partial class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

// Generated:
public Person Clone()
{
    var clone = new Person();
    clone.Name = this.Name;
    clone.Age = this.Age;
    return clone;
}
```

**Diagnostic:**
- Emits **PKPRO002** error if no construction path is available (no parameterless constructor, no copy constructor, not a record)

## Diagnostics Reference

### PKPRO001: Type Not Partial

**Severity:** Error

Type marked with `[Prototype]` must be declared as `partial`.

```csharp
// ❌ Error
[Prototype]
public class Person { }

// ✅ Fixed
[Prototype]
public partial class Person { }
```

### PKPRO002: No Construction Path

**Severity:** Error

Cannot construct clone - no supported construction mechanism found.

```csharp
// ❌ Error: No parameterless constructor
[Prototype]
public partial class Person
{
    public Person(string name) { } // Only parameterized constructor
}

// ✅ Fixed: Add parameterless constructor
[Prototype]
public partial class Person
{
    public Person() { }
    public Person(string name) { }
}
```

### PKPRO003: Unsafe Reference Capture

**Severity:** Warning

Member is a mutable reference type copied by reference - mutations affect both original and clone.

```csharp
// ⚠️ Warning
[Prototype(Mode = PrototypeMode.ShallowWithWarnings)]
public partial class Container
{
    public List<string> Items { get; set; } = new(); // PKPRO003
}

// ✅ Fixed: Use Clone strategy
[Prototype(Mode = PrototypeMode.ShallowWithWarnings)]
public partial class Container
{
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public List<string> Items { get; set; } = new();
}

// ✅ Alternative: Use Shallow mode (acknowledges shallow cloning)
[Prototype(Mode = PrototypeMode.Shallow)]
public partial class Container
{
    public List<string> Items { get; set; } = new(); // No warning
}
```

### PKPRO004: Clone Mechanism Missing

**Severity:** Error

Member has `[PrototypeStrategy(Clone)]` but no suitable clone mechanism is available.

```csharp
public class CustomType
{
    public int Value { get; set; }
    // No Clone() method, no ICloneable
}

// ❌ Error
[Prototype]
public partial class Container
{
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public CustomType Data { get; set; } = new(); // PKPRO004
}

// ✅ Fixed: Add Clone() method to CustomType
public class CustomType
{
    public int Value { get; set; }
    public CustomType Clone() => new() { Value = Value };
}
```

### PKPRO005: Custom Strategy Missing Hook

**Severity:** Error

Member has `[PrototypeStrategy(Custom)]` but no partial clone hook found.

```csharp
// ❌ Error
[Prototype]
public partial class Entity
{
    [PrototypeStrategy(PrototypeCloneStrategy.Custom)]
    public EntityData Data { get; set; } = new(); // PKPRO005
}

// ✅ Fixed: Add partial method
[Prototype]
public partial class Entity
{
    [PrototypeStrategy(PrototypeCloneStrategy.Custom)]
    public EntityData Data { get; set; } = new();
    
    private static partial EntityData CloneData(EntityData value);
}

// Implement in another partial file:
public partial class Entity
{
    private static partial EntityData CloneData(EntityData value)
    {
        return new EntityData { /* custom logic */ };
    }
}
```

### PKPRO006: Attribute Misuse

**Severity:** Warning

`[PrototypeInclude]` or `[PrototypeIgnore]` used incorrectly.

```csharp
// ⚠️ Warning: PrototypeInclude in IncludeAll mode (default)
[Prototype] // IncludeExplicit = false by default
public partial class Config
{
    [PrototypeInclude] // PKPRO006: ignored in this mode
    public string Name { get; set; } = "";
}

// ✅ Fixed: Use IncludeExplicit = true
[Prototype(IncludeExplicit = true)]
public partial class Config
{
    [PrototypeInclude]
    public string Name { get; set; } = "";
}
```

### PKPRO007: DeepCopy Not Implemented

**Severity:** Error

The `DeepCopy` strategy is not yet implemented.

```csharp
// ❌ Error
[Prototype]
public partial class Container
{
    [PrototypeStrategy(PrototypeCloneStrategy.DeepCopy)]
    public ComplexObject Data { get; set; } = new(); // PKPRO007
}

// ✅ Workaround: Use Clone or Custom
[Prototype]
public partial class Container
{
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public ComplexObject Data { get; set; } = new();
}
```

## Best Practices and Tips

### 1. Start with ShallowWithWarnings Mode

The default mode is safe-by-default and alerts you to potential issues:

```csharp
[Prototype] // Default: ShallowWithWarnings
public partial class MyClass
{
    public List<string> Items { get; set; } = new(); // You'll get a warning
}
```

Address warnings by choosing the appropriate strategy for each mutable reference type.

### 2. Use ByReference for Shared Services

```csharp
[Prototype]
public partial class RequestHandler
{
    [PrototypeStrategy(PrototypeCloneStrategy.ByReference)]
    public ILogger Logger { get; set; } = null!; // Shared, don't clone
    
    [PrototypeStrategy(PrototypeCloneStrategy.ByReference)]
    public IDatabase Database { get; set; } = null!; // Shared, don't clone
}
```

### 3. Clone Collections When Elements Are Immutable

```csharp
[Prototype]
public partial class Report
{
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public List<string> Tags { get; set; } = new(); // Strings are immutable
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public Dictionary<string, int> Metrics { get; set; } = new();
}
```

### 4. Use Custom Strategy for Complex Scenarios

```csharp
[Prototype]
public partial class GameState
{
    [PrototypeStrategy(PrototypeCloneStrategy.Custom)]
    public EntityRegistry Entities { get; set; } = new();
    
    private static partial EntityRegistry CloneEntities(EntityRegistry value)
    {
        var clone = new EntityRegistry();
        foreach (var entity in value.GetAll())
        {
            // Deep clone each entity with custom logic
            clone.Add(entity.DeepClone());
        }
        return clone;
    }
}
```

### 5. Ignore Transient/Computed Members

```csharp
[Prototype]
public partial class CachedData
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    
    [PrototypeIgnore] // Don't clone cache
    public DateTime LastAccess { get; set; }
    
    [PrototypeIgnore] // Don't clone derived value
    public int CachedSize => Data.Length;
}
```

### 6. Use ExplicitOnly for Security-Sensitive Types

```csharp
[Prototype(IncludeExplicit = true)]
public partial class Credentials
{
    [PrototypeInclude]
    public string Username { get; set; } = "";
    
    // Secrets NOT cloned by default
    public string Password { get; set; } = "";
    public string ApiKey { get; set; } = "";
}
```

### 7. Prefer Records for Immutable Data

Records get efficient cloning via `with` expressions:

```csharp
[Prototype]
public partial record class User(string Id, string Name, DateTime Created);

// Efficient: uses record with-expression
var clone = user.Duplicate();
```

### 8. Test Clone Independence

Always verify clones are truly independent:

```csharp
[Prototype]
public partial class Document
{
    public string Title { get; set; } = "";
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public List<string> Tags { get; set; } = new();
}

var original = new Document { Title = "Report", Tags = ["draft"] };
var clone = original.Clone();

// Verify independence
clone.Title = "Copy";
clone.Tags.Add("final");

Assert.Equal("Report", original.Title); // Original unchanged
Assert.Single(original.Tags); // Original list unchanged
```

## Real-World Scenarios

### Scenario 1: Configuration Snapshots

```csharp
[Prototype]
public partial class AppConfig
{
    public string Environment { get; set; } = "development";
    public int MaxConnections { get; set; } = 10;
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public Dictionary<string, string> Settings { get; set; } = new();
}

// Take snapshot before changes
var originalConfig = GetCurrentConfig();
var snapshot = originalConfig.Clone();

try
{
    ApplyNewSettings(originalConfig);
    ValidateConfig(originalConfig);
}
catch
{
    // Restore from snapshot
    RestoreConfig(snapshot);
}
```

### Scenario 2: Document Versioning

```csharp
[Prototype]
public partial class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public int Version { get; set; } = 1;
    
    [PrototypeIgnore] // Don't copy timestamps
    public DateTime Created { get; set; }
}

public Document CreateNewVersion(Document current)
{
    var newVersion = current.Clone();
    newVersion.Id = Guid.NewGuid().ToString(); // New ID
    newVersion.Version++; // Increment version
    return newVersion;
}
```

### Scenario 3: Game Entity Spawning

```csharp
[Prototype]
public partial class Enemy
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public int Health { get; set; }
    public int Damage { get; set; }
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public List<string> Abilities { get; set; } = new();
    
    [PrototypeStrategy(PrototypeCloneStrategy.ByReference)]
    public IEnemyAI AI { get; set; } = null!; // Shared behavior
}

// Prototype registry
var goblinPrototype = new Enemy 
{ 
    Type = "Goblin", 
    Health = 50, 
    Abilities = ["Scratch", "Flee"] 
};

// Spawn many enemies efficiently
for (int i = 0; i < 100; i++)
{
    var enemy = goblinPrototype.Clone();
    enemy.Id = $"goblin-{i}";
    SpawnInWorld(enemy);
}
```

### Scenario 4: Test Data Builders

```csharp
[Prototype]
public partial class TestUser
{
    public string Username { get; set; } = "test-user";
    public string Email { get; set; } = "test@example.com";
    public bool IsActive { get; set; } = true;
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public List<string> Roles { get; set; } = new() { "user" };
}

// Base test fixtures
var baseUser = new TestUser();
var adminUser = baseUser.Clone();
adminUser.Roles.Add("admin");

var inactiveUser = baseUser.Clone();
inactiveUser.IsActive = false;
```

### Scenario 5: Form State Management

```csharp
[Prototype]
public partial class FormState
{
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public Dictionary<string, string> Values { get; set; } = new();
    
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public Dictionary<string, string> Errors { get; set; } = new();
    
    public bool IsDirty { get; set; }
}

// Undo/Redo implementation
var history = new Stack<FormState>();

void SaveState(FormState current)
{
    history.Push(current.Clone());
}

FormState Undo()
{
    return history.Count > 0 ? history.Pop() : null;
}
```

## Performance Considerations

### Cloning is Fast for Value Types

```csharp
[Prototype]
public partial struct Point3D
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

// Very fast - just copies bytes
var clone = point.Clone();
```

### Record with-expression is Optimized

```csharp
[Prototype]
public partial record class Customer(string Id, string Name);

// Compiler-optimized shallow copy
var clone = customer.Duplicate();
```

### Collection Cloning Has Overhead

```csharp
[Prototype]
public partial class Container
{
    [PrototypeStrategy(PrototypeCloneStrategy.Clone)]
    public List<string> Items { get; set; } = new(); // O(n) copy
}

// Consider ByReference for read-only scenarios or immutable collections
```

### Custom Strategy for Optimization

```csharp
[Prototype]
public partial class LargeObject
{
    [PrototypeStrategy(PrototypeCloneStrategy.Custom)]
    public byte[] Data { get; set; } = Array.Empty<byte>();
    
    private static partial byte[] CloneData(byte[] value)
    {
        if (value.Length == 0) return Array.Empty<byte>();
        
        var clone = new byte[value.Length];
        Buffer.BlockCopy(value, 0, clone, 0, value.Length); // Fast copy
        return clone;
    }
}
```

## See Also

- [Prototype Demo Example](../examples/prototype-demo.md) - Real-world usage with game entities
- [PatternKit.Core.Creational.Prototype](../../src/PatternKit.Core/Creational/Prototype/) - Runtime prototype registry
- [Builder Generator](builder.md) - For fluent object construction
