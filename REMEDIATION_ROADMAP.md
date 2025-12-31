# PatternKit Remediation Roadmap

## Overview

This roadmap provides actionable work items to address all gaps identified in the audit. Items are organized by priority and include implementation guidance.

---

## Phase 1: Critical Fixes (P0)

### 1.1 Resolve Visitor Pattern Issue

**Goal:** Ensure Visitor pattern matches GoF definition or is accurately named

#### Option A: Rename Current Implementation (Recommended)

**Scope:** Rename `Visitor` to `TypeDispatcher`

**Files to Modify:**
- `src/PatternKit.Core/Behavioral/Visitor/Visitor.cs` -> `TypeDispatcher.cs`
- `src/PatternKit.Core/Behavioral/Visitor/ActionVisitor.cs` -> `ActionTypeDispatcher.cs`
- `src/PatternKit.Core/Behavioral/Visitor/AsyncVisitor.cs` -> `AsyncTypeDispatcher.cs`
- `src/PatternKit.Core/Behavioral/Visitor/AsyncActionVisitor.cs` -> `AsyncActionTypeDispatcher.cs`
- All tests in `test/PatternKit.Tests/Behavioral/Visitor/`
- All examples in `src/PatternKit.Examples/VisitorDemo/`
- All docs in `docs/patterns/behavioral/visitor/`

**Breaking Changes:**
- Namespace change
- Class name changes
- Using directive updates required

**Migration Guide Required:** Yes

#### Option B: Implement True Visitor (Supplementary)

**New Files to Create:**
```
src/PatternKit.Core/Behavioral/Visitor/
├── IVisitable.cs           # Accept interface
├── IVisitor.cs             # Visit interface (generated per hierarchy)
├── VisitorGenerator.cs     # Source generator for visitor interfaces
└── VisitorBase.cs          # Optional base class

src/PatternKit.Generators/
└── VisitorInterfaceGenerator.cs  # Generates IVisitor per marked hierarchy
```

**API Design:**
```csharp
// Mark element hierarchy
[GenerateVisitor]
public interface IMediaElement { }

public class VideoH265 : IMediaElement { }
public class AudioFlac : IMediaElement { }

// Generated visitor interface
public interface IMediaElementVisitor<TResult>
{
    TResult Visit(VideoH265 element);
    TResult Visit(AudioFlac element);
}

// User implements visitor
public class CompressionVisitor : IMediaElementVisitor<byte[]>
{
    public byte[] Visit(VideoH265 video) => CompressH265(video);
    public byte[] Visit(AudioFlac audio) => CompressFlac(audio);
}

// Usage with double dispatch
var visitor = new CompressionVisitor();
byte[] result = element.Accept(visitor);
```

**Implementation Complexity:** High

---

### 1.2 Document Visitor Clarification

**Goal:** Clear documentation explaining the difference

**New Documentation:**
```
docs/patterns/behavioral/visitor/
├── visitor-vs-typedispatcher.md  # Comparison guide
├── when-to-use-which.md          # Decision guide
└── migration-guide.md            # For upgrading
```

---

## Phase 2: Missing Patterns (P1)

### 2.1 Implement Abstract Factory

**Goal:** Add Abstract Factory pattern with fluent API

**New Files:**
```
src/PatternKit.Core/Creational/AbstractFactory/
├── AbstractFactory.cs       # Main implementation
└── FamilyBuilder.cs         # Family configuration

docs/patterns/creational/abstractfactory/
├── abstractfactory.md       # Reference docs
└── index.md                 # Overview

docs/examples/
└── ui-theme-factory.md      # Enterprise example

src/PatternKit.Examples/AbstractFactoryDemo/
├── UIThemeFactory.cs        # Example implementation
└── DatabaseProviderFactory.cs

test/PatternKit.Tests/Creational/
└── AbstractFactoryTests.cs
```

**API Design:**
```csharp
public class AbstractFactory<TFamily>
{
    public static Builder Create() => new Builder();

    public class Builder
    {
        public FamilyBuilder<TProduct> Family<TProduct>(string name);
        public AbstractFactory<TFamily> Build();
    }

    public TFamily GetFamily(string name);
    public bool TryGetFamily(string name, out TFamily family);
}

// Fluent usage
var factory = AbstractFactory<IUIComponents>.Create()
    .Family<IUIComponents>("light")
        .Create<IButton>(() => new LightButton())
        .Create<ITextBox>(() => new LightTextBox())
        .Create<IMenu>(() => new LightMenu())
        .EndFamily()
    .Family<IUIComponents>("dark")
        .Create<IButton>(() => new DarkButton())
        .Create<ITextBox>(() => new DarkTextBox())
        .Create<IMenu>(() => new DarkMenu())
        .EndFamily()
    .Build();

var components = factory.GetFamily("dark");
var button = components.Create<IButton>();
```

**Implementation Complexity:** High

---

### 2.2 Implement Interpreter

**Goal:** Add Interpreter pattern with fluent API

**New Files:**
```
src/PatternKit.Core/Behavioral/Interpreter/
├── Interpreter.cs           # Main implementation
├── Expression.cs            # Expression tree nodes
├── Terminal.cs              # Terminal expressions
└── NonTerminal.cs           # Non-terminal expressions

docs/patterns/behavioral/interpreter/
├── interpreter.md           # Reference docs
└── index.md                 # Overview

docs/examples/
├── expression-evaluator.md  # Math expressions
└── filter-dsl.md            # Query DSL example

src/PatternKit.Examples/InterpreterDemo/
├── MathExpressionDemo.cs
└── FilterQueryDemo.cs

test/PatternKit.Tests/Behavioral/
└── InterpreterTests.cs
```

**API Design:**
```csharp
public class Interpreter<TContext, TResult>
{
    public static Builder Create() => new Builder();

    public class Builder
    {
        public Builder Terminal<T>(string name, Func<T, TContext, TResult> interpret);
        public Builder NonTerminal(string name, Func<TResult[], TContext, TResult> combine);
        public Builder WithParser(IExpressionParser parser);
        public Interpreter<TContext, TResult> Build();
    }

    public TResult Interpret(string expression, TContext context);
    public bool TryInterpret(string expression, TContext context, out TResult result);
}

// Usage
var calc = Interpreter<object, double>.Create()
    .Terminal("number", (token, ctx) => double.Parse(token))
    .NonTerminal("add", (args, ctx) => args[0] + args[1])
    .NonTerminal("mul", (args, ctx) => args[0] * args[1])
    .Build();

var result = calc.Interpret("(add 1 (mul 2 3))", null); // 7.0
```

**Implementation Complexity:** High

---

## Phase 3: Documentation & Examples (P2-P3)

### 3.1 Add Missing Enterprise Examples

| Pattern | Example | Files to Create |
|---------|---------|-----------------|
| Composite | File system | `FileSystemComposite.cs`, `file-system-composite.md` |
| Bridge | Cross-platform renderer | `CrossPlatformRenderer.cs`, `cross-platform-bridge.md` |
| Factory | Plugin loader | `PluginFactory.cs`, `plugin-factory.md` |
| Iterator | DB pagination | `DatabaseCursor.cs`, `database-pagination.md` |

### 3.2 Enhance Pattern Documentation

**Add to each pattern doc:**
- Related Patterns section
- UML diagram (Mermaid or image)
- Performance notes
- Anti-patterns section

**Template:**
```markdown
## Related Patterns

- **[Strategy](../strategy/strategy.md)**: Similar but...
- **[Command](../command/command.md)**: Often used together...

## When NOT to Use

- Don't use this pattern when...
- Consider [Alternative] instead if...

## Performance Characteristics

- Memory: O(n) where n is...
- Time: O(1) for lookup...
- Thread safety: Safe after Build()
```

### 3.3 Add Integration Tests

**New test files:**
```
test/PatternKit.Integration.Tests/
├── PatternComposition/
│   ├── StrategyWithDecoratorTests.cs
│   ├── ChainWithMediatorTests.cs
│   └── BuilderWithFactoryTests.cs
├── Performance/
│   └── AllocationBenchmarks.cs
└── Concurrency/
    └── ThreadSafetyTests.cs
```

---

## Implementation Order

```
Week 1-2:   Phase 1.1 - Rename Visitor to TypeDispatcher
Week 2-3:   Phase 1.2 - Document the change
Week 3-5:   Phase 2.1 - Implement Abstract Factory
Week 5-7:   Phase 2.2 - Implement Interpreter
Week 7-8:   Phase 3.1 - Add missing examples
Week 8-9:   Phase 3.2 - Enhance documentation
Week 9-10:  Phase 3.3 - Add integration tests
```

---

## Definition of Done

### For New Patterns
- [ ] Core implementation with fluent API
- [ ] At least 2 builder overloads (simple and full)
- [ ] Thread-safe after Build()
- [ ] Comprehensive unit tests
- [ ] Reference documentation
- [ ] At least 1 enterprise example
- [ ] Example walkthrough in docs/examples/

### For Fixes
- [ ] Breaking changes documented
- [ ] Migration guide provided
- [ ] All tests updated and passing
- [ ] All docs updated
- [ ] CHANGELOG entry

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Breaking changes upset users | Medium | High | Clear migration guide, deprecation period |
| Abstract Factory complexity | Medium | Medium | Start with simple API, iterate |
| Interpreter scope creep | High | Medium | Define minimal viable grammar first |
| Documentation maintenance | Low | Low | Automate where possible |

---

## Success Metrics

- All 23 GoF patterns implemented
- All patterns have fluent APIs
- All patterns have documentation
- All patterns have enterprise examples
- Zero P0/P1 gaps remaining
- Test coverage > 90%
