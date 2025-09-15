# MutableBuilder\<T>

A small, allocation-light builder for creating and configuring mutable instances. Use `MutableBuilder<T>` when you want to compose a sequence of in-place mutations and validations against instances produced by a factory, then produce the configured instance with a single `Build()` call.

File: `docs/patterns/creational/builder/mutablebuilder.md`

## TL;DR

```csharp
var person = MutableBuilder<Person>
    .New(static () => new Person())
    .With(p => p.Name = "Ada")
    .With(p => p.Age = 30)
    .Require(p => p.Name is not null && p.Name != "" ? null : "Name must be non-empty.")
    .Build();
```

## What it is

MutableBuilder\<T> is a tiny DSL for:

- collecting mutation actions (`With`),
- collecting validators that return an optional error message (`Require`),
- applying mutations in registration order to a fresh instance from a factory,
- failing fast on the first validation that returns a non-`null` message.

It favors explicit, reflection-free configuration and is optimized for minimal allocations. Prefer `static` lambdas to avoid captured closures.

## Key semantics

- Registration order is preserved: mutations are executed in the order added.
- Validations run in the order registered during `Build()` and the first non-`null` message causes `Build()` to throw `InvalidOperationException` with that message.
- `Build()` calls the configured factory for each build; the builder can be reused to produce multiple instances (later builds reflect additional registered mutations/validators).
- Builders are not thread-safe.

## API at a glance

- `static MutableBuilder<T> New(Func<T> factory)` — create a builder that calls `factory()` for each `Build()`.
- `MutableBuilder<T> With(Action<T> mutation)` — append an in-place mutation.
- `MutableBuilder<T> Require(Func<T, string?> validator)` — append a validator that returns `null` for success or an error message for failure.
- `T Build()` — create an instance via the factory, apply mutations, run validators, return the instance or throw `InvalidOperationException` on first validator failure.

Extension-style conveniences (project-specific, common patterns):

- `RequireNotEmpty(Func<T, string?> selector, string name)` — validate string properties are not empty.
- `RequireRange(Func<T, int> selector, int min, int max, string name)` — inclusive numeric range validator.

## Examples

1) Simple configuration

```csharp
var p = MutableBuilder<Person>
    .New(static () => new Person())
    .With(p => p.Name = "Ada")
    .With(p => p.Age = 30)
    .Build(); // { Name = "Ada", Age = 30 }
```

2) Mutations applied in order

```csharp
var b = MutableBuilder<Person>.New(() => new Person())
    .With(p => p.Steps.Add("A"))
    .With(p => p.Steps.Add("B"));

var first = b.Build(); // Steps == ["A","B"]
b.With(p => p.Steps.Add("C"));
var second = b.Build(); // Steps == ["A","B","C"]
```

3) Validation failure

```csharp
var b = MutableBuilder<Person>.New(() => new Person())
    .With(p => p.Name = "")
    .Require(p => string.IsNullOrEmpty(p.Name) ? "Name must be non-empty." : null);

_ = Record.Exception(() => b.Build()); // InvalidOperationException with message
```

## Testing tips

- Test mutation order by appending actions that record to a shared list on the instance.
- Test validation by registering multiple validators and asserting the first failing validator message is thrown.
- Verify builder reuse by calling `Build()` multiple times after registering additional mutations.

## Why use it

- Explicit, readable configuration in tests and factories.
- Predictable behavior: deterministic mutation and validation order.
- Low overhead: only lists during configuration and one factory call + validators per `Build()`.

## Gotchas

- Builders are mutable and not thread-safe. Freeze semantics are not provided — callers must avoid concurrent mutations.
- Prefer `static` lambdas to avoid closure allocations in hot paths.
- Validators must return `null` on success; any non-`null` string is treated as the error message returned to the caller via `InvalidOperationException`.