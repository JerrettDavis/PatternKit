# Builder Generator

The builder generator writes GoF-aligned builders so you can stop hand-rolling fluent setter pipelines, validation, and async variants. It requires only the `PatternKit.Generators` package at compile time—no runtime dependency on PatternKit.

> Models: **Mutable Instance** (classic builder over a mutable type) and **State + Projection** (compose immutable state, then project to a DTO/result).

## Quickstart: Mutable Instance

```csharp
using PatternKit.Generators.Builders;

[GenerateBuilder(BuilderTypeName = "PersonBuilder", GenerateBuilderMethods = true)]
public partial class Person
{
    [BuilderRequired(Message = "Name is required.")]
    public string? Name { get; set; }
    public int Age { get; set; }
}
```

Generated shape (essentials):

- `PersonBuilder.New()`
- `WithName(string? value)`, `WithAge(int value)`
- `With(Action<Person>)`, `WithAsync(Func<Person, ValueTask>)`
- `Require(Func<Person, string?>)`, `RequireAsync(Func<Person, ValueTask<string?>>)`
- `Build()` and `BuildAsync()`
- Optional helpers on `Person` when `GenerateBuilderMethods = true`:
  - `Person Build(Action<PersonBuilder> configure)`
  - `ValueTask<Person> BuildAsync(Func<PersonBuilder, ValueTask> configure)`

Usage:

```csharp
var p = Person.Build(b => b.WithName("Ada").WithAge(37));
var asyncPerson = await Person.BuildAsync(b =>
{
    b.WithName("Quinn");
    b.WithAsync(person => { person.Age = 31; return new ValueTask(); });
    return new ValueTask();
});
```

Validation:

- Required members (`[BuilderRequired]`) flip a flag when set; `Build()` and `BuildAsync()` throw `InvalidOperationException` when missing.
- Requirements run in registration order; the first non-null string aborts with `InvalidOperationException`.

## Quickstart: State + Projection

Use when you want to avoid mutating the final product during composition or when projecting to another type.

```csharp
using PatternKit.Generators.Builders;

public readonly record struct PersonState(string? Name, int Age);
public sealed record PersonDto(string Name, int Age);

[GenerateBuilder(Model = BuilderModel.StateProjection, GenerateBuilderMethods = true)]
public static partial class PersonDtoBuilderHost
{
    public static PersonState Seed() => default;

    [BuilderProjector]
    public static PersonDto Project(PersonState s) => new(s.Name!, s.Age);
}
```

Generated shape:

- `PersonDtoBuilderHostBuilder.New()` seeds `_state` with `Seed()`.
- `With(Func<PersonState, PersonState>)`, `WithAsync(Func<PersonState, ValueTask<PersonState>>)`
- `Require(Func<PersonState, string?>)`, `RequireAsync(Func<PersonState, ValueTask<string?>>)`
- `Build(Func<PersonState, TResult> projector)`; when `[BuilderProjector]` is present, a parameterless `Build()` uses it.
- `BuildAsync(Func<PersonState, ValueTask<TResult>> projector)`; when a default projector is present, a parameterless `BuildAsync()` is added.
- Optional helpers on the annotated partial class when `GenerateBuilderMethods = true`.

Usage:

```csharp
var dto = PersonDtoBuilderHost.Build(b =>
{
    b.With(s => s with { Name = "Eve" });
    b.With(s => s with { Age = 42 });
});

var dtoAsync = await PersonDtoBuilderHost.BuildAsync(b =>
{
    b.WithAsync(s => new ValueTask<PersonState>(s with { Age = 43 }));
    return new ValueTask();
});
```

## Attributes

`GenerateBuilderAttribute`

- `BuilderTypeName` (default: `<TypeName>Builder`)
- `NewMethodName` (default: `New`)
- `BuildMethodName` (default: `Build`)
- `Model` (`MutableInstance` | `StateProjection`)
- `GenerateBuilderMethods` (emit static helpers on the target type)
- `ForceAsync` (forces async APIs even without async callbacks)
- `IncludeFields` (include public settable fields)

Other attributes:

- `[BuilderConstructor]` — mark the preferred constructor for mutable builders.
- `[BuilderIgnore]` — skip a property/field.
- `[BuilderRequired(Message?)]` — mark a member as required; message surfaces at runtime when missing.
- `[BuilderProjector]` — designate the default projector for state builders.

## Diagnostics

- `B001` — Type must be partial and non-generic.
- `B002` — Builder type name conflicts with an existing type in the namespace.
- `B003` — No usable constructor for mutable builder.
- `B004` — Multiple constructors marked `[BuilderConstructor]`.
- `B005` — Member is not assignable by the builder.
- `B006` — Generated builder methods would collide with existing members.
- `BR001` — Required member is never set (runtime guard emitted).
- `BR002` — `[BuilderRequired]` applied to unsupported member.
- `BR003` — Required member type incompatible with generated setter.
- `BP001` — State+Projection builders require a `Seed()` method.
- `BP002` — Multiple `[BuilderProjector]` methods.
- `BP003` — Invalid projector signature.
- `BA001` — Async generation requested but disabled.
- `BA002` — Invalid async signature.

## Async shape

- Async steps/requirements/projectors use `ValueTask` to minimize allocations.
- Async APIs are emitted when any async hook is registered or when `ForceAsync = true`.
- The generator does not rewrite `Task` to `ValueTask`; supply `ValueTask` delegates if you want async support.
