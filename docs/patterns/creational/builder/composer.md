# Composer\<TState, TOut>

A tiny, explicit **functional** builder: you accumulate immutable state (usually a small struct) via **pure transformations**, optionally add **validations**, then **project** the final state into your output type.

---

## Mental model

* **Seed → Transform → Validate → Project.**
* `With` composes functions **left-to-right** (i.e., `b(a(seed))`).
* `Require` chains validators; the **first non-null message** throws.
* Nothing happens until `Build` — that’s when transforms and validators run.

---

## API at a glance

```csharp
// Create with a seed factory (prefer static to avoid captures)
var c = Composer<State, Dto>.New(static () => default);

// Add transforms (pure functions State -> State)
c.With(static s => /* change s */);

// Add validators (State -> string?); return null when OK
c.Require(static s => /* message-or-null */);

// Finish: transform final State into your output type
Dto dto = c.Build(static s => new Dto(/* from s */));
```

### Threading & immutability

* The composer instance is mutable **until** `Build`. You can keep calling `With`/`Require` and `Build` repeatedly.
* The *state* you produce should be treated as immutable; prefer small `record struct`s for perf.

---

## Minimal examples

### 1) Basic composition

```csharp
public readonly record struct PersonState(string? Name, int Age);
public sealed record PersonDto(string Name, int Age);

var dto = Composer<PersonState, PersonDto>
    .New(static () => default) // (Name=null, Age=0)
    .With(static s => s with { Name = "Ada" })
    .With(static s => s with { Age = 30 })
    .Require(static s => string.IsNullOrWhiteSpace(s.Name) ? "Name is required." : null)
    .Build(static s => new PersonDto(s.Name!, s.Age));
// -> PersonDto("Ada", 30)
```

### 2) Left-to-right transform order

```csharp
static PersonState A(PersonState s) => s with { Age = 10 };
static PersonState B(PersonState s) => s with { Age = 20 };

var dto = Composer<PersonState, PersonDto>
    .New(static () => default)
    .With(A)     // sets Age to 10
    .With(B)     // then overrides to 20
    .Require(static _ => null)
    .Build(static s => new PersonDto(s.Name ?? "?", s.Age));
// Age == 20
```

### 3) Multiple validators (first failure wins)

```csharp
static string? NameRequired(PersonState s)
    => string.IsNullOrWhiteSpace(s.Name) ? "Name is required." : null;

static string? AgeInRange(PersonState s)
    => s.Age is < 0 or > 130 ? $"Age must be within [0..130] but was {s.Age}." : null;

var ex = Assert.Throws<InvalidOperationException>(() =>
    Composer<PersonState, PersonDto>.New(static () => new(null, -5))
        .Require(NameRequired)  // fails first -> throws this message
        .Require(AgeInRange)
        .Build(static s => new PersonDto(s.Name!, s.Age)));
Assert.Equal("Name is required.", ex.Message);
```

### 4) Reuse a composer

```csharp
var comp = Composer<PersonState, PersonDto>
    .New(static () => default)
    .With(static s => s with { Name = "Ada" })
    .Require(static _ => null);

var dto1 = comp.Build(static s => new PersonDto(s.Name!, s.Age));         // ("Ada", 0)
var dto2 = comp.With(static s => s with { Age = 30 })
               .Build(static s => new PersonDto(s.Name!, s.Age));          // ("Ada", 30)
```

---

## Patterns & tips

* **Prefer method pointers** over capturing lambdas for AOT/JIT friendliness:

  ```csharp
  static PersonState SetName(PersonState s, string n) => s with { Name = n };
  c.With(static s => SetName(s, "Ada"));
  ```
* **Validation as composition**: chain small rules with `Require`; return `null` on success.
* **One projection, many outputs**: You can build multiple outputs by calling `Build` with different projectors.
* **No side effects in transforms**: keep `With` pure (deterministic, no I/O) for easy reasoning and testing.

---

## Error handling

* `Build` throws `InvalidOperationException` with the **first** validation message that is not `null`/empty.
* If there are no validators, `Build` always succeeds.

---

## Performance notes

* `With` composes delegates; composition cost is O(#With) executed once per `Build`.
* Use small, shallow `record struct` state to minimize copying.
* Prefer `static` lambdas / method groups to avoid allocations from captures.

---

## Reference (public API)

```csharp
public sealed class Composer<TState, TOut>
{
    public static Composer<TState, TOut> New(Func<TState> seed);

    public Composer<TState, TOut> With(Func<TState, TState> transform);

    public Composer<TState, TOut> Require(Func<TState, string?> validate);

    public TOut Build(Func<TState, TOut> project);
}
```

---

## See also

* **ChainBuilder\<T>** – collect items, project to a product.
* **BranchBuilder\<TPred,THandler>** – collect predicate/handler pairs + optional default.
* **Strategy / TryStrategy / AsyncStrategy** – consumers of these creational patterns.
