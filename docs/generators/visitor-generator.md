# Visitor Pattern Generator

The Visitor Pattern Generator automatically generates fluent, type-safe visitor infrastructure for hierarchies marked with the `[GenerateVisitor]` attribute. It supports class, interface, struct, and record hierarchies, eliminating boilerplate code and providing modern C# ergonomics including async/await support, ValueTask, and generic type inference.

## Overview

The generator produces:

- **Visitor interfaces** for all four visitor variants (sync/async, result/action)
- **Accept methods** on all types in the hierarchy
- **Fluent builder APIs** for composing visitors with type-safe handlers
- **Zero runtime overhead** through source generation

## Quick Start

### 1. Define Your Visitable Hierarchy

Mark your base class with `[GenerateVisitor]`:

```csharp
using PatternKit.Generators;

[GenerateVisitor]
public partial class AstNode
{
}

public partial class Expression : AstNode
{
}

public partial class Statement : AstNode
{
}

public partial class NumberExpression : Expression
{
    public int Value { get; init; }
}
```

### 2. Build Your Project

The generator runs during compilation and produces:
- `IAstNodeVisitor<TResult>` - sync result visitor interface
- `IAstNodeVisitorAction` - sync action visitor interface  
- `IAstNodeVisitorAsync<TResult>` - async result visitor interface
- `IAstNodeVisitorAsyncAction` - async action visitor interface
- `Accept` and `AcceptAsync` methods on all types
- Builder classes for composing visitors

### 3. Use the Generated Visitors

#### Sync Result Visitor

```csharp
var evaluator = new AstNodeVisitorBuilder<int>()
    .When<NumberExpression>(n => n.Value)
    .When<AddExpression>(add => 
    {
        var left = add.Left.Accept(evaluator);
        var right = add.Right.Accept(evaluator);
        return left + right;
    })
    .Default(_ => 0)
    .Build();

var result = myExpression.Accept(evaluator);
```

#### Sync Action Visitor

```csharp
var printer = new AstNodeActionVisitorBuilder()
    .When<NumberExpression>(n => Console.WriteLine($"Number: {n.Value}"))
    .When<AddExpression>(add => Console.WriteLine("Add"))
    .Default(node => Console.WriteLine("Unknown"))
    .Build();

myExpression.Accept(printer);
```

#### Async Result Visitor

```csharp
var asyncEvaluator = new AstNodeAsyncVisitorBuilder<string>()
    .WhenAsync<NumberExpression>(async (n, ct) => 
    {
        await LogAsync($"Evaluating {n.Value}", ct);
        return n.Value.ToString();
    })
    .WhenAsync<AddExpression>(async (add, ct) =>
    {
        var left = await add.Left.AcceptAsync(asyncEvaluator, ct);
        var right = await add.Right.AcceptAsync(asyncEvaluator, ct);
        return $"({left} + {right})";
    })
    .DefaultAsync(async (node, ct) =>
    {
        await LogAsync("Unknown node", ct);
        return "?";
    })
    .Build();

var result = await myExpression.AcceptAsync(asyncEvaluator);
```

#### Async Action Visitor

```csharp
var asyncLogger = new AstNodeAsyncActionVisitorBuilder()
    .WhenAsync<NumberExpression>(async (n, ct) => 
        await LogToDbAsync($"Number: {n.Value}", ct))
    .WhenAsync<AddExpression>(async (add, ct) => 
        await LogToDbAsync("Addition operation", ct))
    .Build();

await myExpression.AcceptAsync(asyncLogger);
```

## Supported Hierarchy Types

The visitor generator supports multiple types of hierarchies, providing flexibility in design:

### Class-Based Hierarchies

Traditional class inheritance hierarchies are fully supported:

```csharp
[GenerateVisitor]
public abstract partial class Animal
{
}

public partial class Dog : Animal
{
    public string Breed { get; init; }
}

public partial class Cat : Animal
{
    public bool IsIndoor { get; init; }
}
```

### Interface-Based Hierarchies

Hierarchies based on interfaces work seamlessly:

```csharp
[GenerateVisitor]
public partial interface IShape
{
}

public partial class Circle : IShape
{
    public double Radius { get; init; }
}

public partial class Rectangle : IShape
{
    public double Width { get; init; }
    public double Height { get; init; }
}

public partial class Triangle : IShape
{
    public double Base { get; init; }
    public double Height { get; init; }
}
```

**Note:** For interface base types, the generated visitor interface name is intelligently derived. `IShape` generates `IShapeVisitor` (not `IIShapeVisitor`).

### Struct-Based Hierarchies

Value types can implement visitable interfaces for allocation-free visitor patterns:

```csharp
[GenerateVisitor]
public partial interface IValue
{
}

public partial struct IntValue : IValue
{
    public int Value { get; init; }
}

public partial struct DoubleValue : IValue
{
    public double Value { get; init; }
}

// No boxing occurs during visitation
var visitor = new IValueVisitorBuilder<string>()
    .When<IntValue>(i => $"Int:{i.Value}")
    .When<DoubleValue>(d => $"Double:{d.Value:F2}")
    .Build();

var intVal = new IntValue { Value = 42 };
var result = intVal.Accept(visitor); // "Int:42"
```

### Record Types

Records are also supported:

```csharp
[GenerateVisitor]
public abstract partial record Message;

public partial record TextMessage(string Content) : Message;
public partial record ImageMessage(byte[] Data, string Format) : Message;
```

### Mixed Hierarchies

You can mix interfaces, classes, and structs in complex hierarchies:

```csharp
[GenerateVisitor]
public partial interface INode
{
}

public abstract partial class Expression : INode
{
}

public partial class Literal : Expression
{
    public object Value { get; init; }
}

public partial struct Position : INode
{
    public int Line { get; init; }
    public int Column { get; init; }
}
```

## Attribute Options

The `[GenerateVisitor]` attribute supports several options:

```csharp
[GenerateVisitor(
    VisitorInterfaceName = "ICustomVisitor",    // Custom interface name
    GenerateAsync = true,                        // Generate async variants (default: true)
    GenerateActions = true,                      // Generate action variants (default: true)
    AutoDiscoverDerivedTypes = true              // Auto-discover derived types (default: true)
)]
public partial class MyBase
{
}
```

### VisitorInterfaceName

Customizes the generated interface name. Default is `I{BaseTypeName}Visitor`.

```csharp
[GenerateVisitor(VisitorInterfaceName = "INodeProcessor")]
public partial class Node { }

// Generates: INodeProcessor<TResult>, INodeProcessorAction, etc.
```

### GenerateAsync

Controls whether async visitor variants are generated.

```csharp
[GenerateVisitor(GenerateAsync = false)]
public partial class SyncOnlyNode { }

// Only generates sync visitors
```

### GenerateActions

Controls whether action (void-returning) visitor variants are generated.

```csharp
[GenerateVisitor(GenerateActions = false)]
public partial class ResultOnlyNode { }

// Only generates result-returning visitors
```

### AutoDiscoverDerivedTypes

When true (default), the generator automatically finds all types deriving from the base type in the same assembly. Set to false if you want to manually control which types are visitable.

## Generated Code Structure

For a base type `Document`, the generator produces:

### Interfaces

```csharp
public interface IDocumentVisitor<TResult>
{
    TResult Visit(Document document);
    TResult Visit(PdfDocument pdfDocument);
    TResult Visit(WordDocument wordDocument);
}

public interface IDocumentVisitorAction
{
    void Visit(Document document);
    void Visit(PdfDocument pdfDocument);
    void Visit(WordDocument wordDocument);
}

public interface IDocumentVisitorAsync<TResult>
{
    ValueTask<TResult> VisitAsync(Document document, CancellationToken cancellationToken = default);
    ValueTask<TResult> VisitAsync(PdfDocument pdfDocument, CancellationToken cancellationToken = default);
    ValueTask<TResult> VisitAsync(WordDocument wordDocument, CancellationToken cancellationToken = default);
}

public interface IDocumentVisitorAsyncAction
{
    ValueTask VisitAsync(Document document, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(PdfDocument pdfDocument, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(WordDocument wordDocument, CancellationToken cancellationToken = default);
}
```

### Accept Methods

Each type gets Accept methods:

```csharp
public partial class Document
{
    public TResult Accept<TResult>(IDocumentVisitor<TResult> visitor)
        => visitor.Visit(this);

    public void Accept(IDocumentVisitorAction visitor)
        => visitor.Visit(this);

    public ValueTask<TResult> AcceptAsync<TResult>(IDocumentVisitorAsync<TResult> visitor, CancellationToken cancellationToken = default)
        => visitor.VisitAsync(this, cancellationToken);

    public ValueTask AcceptAsync(IDocumentVisitorAsyncAction visitor, CancellationToken cancellationToken = default)
        => visitor.VisitAsync(this, cancellationToken);
}
```

### Fluent Builders

```csharp
public sealed class DocumentVisitorBuilder<TResult>
{
    public DocumentVisitorBuilder<TResult> When<T>(Func<T, TResult> handler) where T : Document { ... }
    public DocumentVisitorBuilder<TResult> Default(Func<Document, TResult> handler) { ... }
    public IDocumentVisitor<TResult> Build() { ... }
}

public sealed class DocumentActionVisitorBuilder
{
    public DocumentActionVisitorBuilder When<T>(Action<T> handler) where T : Document { ... }
    public DocumentActionVisitorBuilder Default(Action<Document> handler) { ... }
    public IDocumentVisitorAction Build() { ... }
}

public sealed class DocumentAsyncVisitorBuilder<TResult>
{
    public DocumentAsyncVisitorBuilder<TResult> WhenAsync<T>(Func<T, CancellationToken, ValueTask<TResult>> handler) where T : Document { ... }
    public DocumentAsyncVisitorBuilder<TResult> DefaultAsync(Func<Document, CancellationToken, ValueTask<TResult>> handler) { ... }
    public IDocumentVisitorAsync<TResult> Build() { ... }
}

public sealed class DocumentAsyncActionVisitorBuilder
{
    public DocumentAsyncActionVisitorBuilder WhenAsync<T>(Func<T, CancellationToken, ValueTask> handler) where T : Document { ... }
    public DocumentAsyncActionVisitorBuilder DefaultAsync(Func<Document, CancellationToken, ValueTask> handler) { ... }
    public IDocumentVisitorAsyncAction Build() { ... }
}
```

## Best Practices

### 1. Use Partial Classes

All types in your visitable hierarchy must be declared as `partial`:

```csharp
[GenerateVisitor]
public partial class MyBase { }

public partial class MyDerived : MyBase { }
```

### 2. Register Specific Types First

When using builders, register more specific types before base types to ensure proper dispatch:

```csharp
var visitor = new NodeVisitorBuilder<string>()
    .When<SpecificNode>(n => "specific")
    .When<BaseNode>(n => "base")    // More general - register after specific
    .Build();
```

### 3. Provide Default Handlers

Always provide a default handler for robustness:

```csharp
var visitor = new NodeVisitorBuilder<string>()
    .When<KnownNode>(n => "known")
    .Default(n => $"Unknown: {n.GetType().Name}")  // Graceful fallback
    .Build();
```

### 4. Use CancellationToken

For async visitors, always wire through cancellation tokens:

```csharp
var visitor = new NodeAsyncVisitorBuilder<string>()
    .WhenAsync<Node>(async (node, ct) => 
    {
        await ProcessAsync(node, ct);  // Pass ct through
        return "done";
    })
    .Build();

await node.AcceptAsync(visitor, cancellationToken);
```

### 5. Compose Visitors

Build complex processing pipelines by composing multiple visitors:

```csharp
// Validate
var validationResult = doc.Accept(validator);
if (!validationResult.IsValid) return;

// Transform
var transformed = doc.Accept(transformer);

// Process async
await transformed.AcceptAsync(asyncProcessor);
```

## Performance Considerations

The generated visitors use:
- **Dictionary-based dispatch** for O(1) handler lookup
- **No reflection** - all types known at compile time
- **Minimal allocations** - builder reuses dictionary, implementations are sealed
- **ValueTask for async** - reduces allocations for synchronously-completing operations

## Common Patterns

### Tree Traversal

```csharp
var calculator = new ExpressionVisitorBuilder<int>()
    .When<NumberExpr>(n => n.Value)
    .When<BinaryExpr>(bin => 
    {
        var left = bin.Left.Accept(calculator);
        var right = bin.Right.Accept(calculator);
        return bin.Operator switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            _ => 0
        };
    })
    .Build();
```

### Data Collection

```csharp
var collector = new List<string>();
var gatherer = new NodeActionVisitorBuilder()
    .When<TextNode>(t => collector.Add(t.Text))
    .When<ContainerNode>(c => 
    {
        foreach (var child in c.Children)
            child.Accept(gatherer);
    })
    .Build();

root.Accept(gatherer);
// collector now contains all text
```

### Validation

```csharp
var validator = new DocumentVisitorBuilder<ValidationResult>()
    .When<PdfDocument>(pdf => 
        pdf.PageCount > 0 
            ? ValidationResult.Valid 
            : ValidationResult.Invalid("No pages"))
    .When<WordDocument>(word => 
        word.WordCount > 0 
            ? ValidationResult.Valid 
            : ValidationResult.Invalid("Empty document"))
    .Default(_ => ValidationResult.Invalid("Unknown type"))
    .Build();
```

## Diagnostics

The generator provides helpful diagnostics to catch common issues:

### PKVIS001: No concrete types found

**Severity:** Warning

This warning appears when the generator cannot find any concrete types implementing or deriving from the marked base type.

```csharp
[GenerateVisitor]
public partial interface IEmptyHierarchy { }

// Warning PKVIS001: No concrete types implementing or deriving from 'IEmptyHierarchy' were found
```

**Solutions:**
- Add concrete types that implement the interface or derive from the class
- Set `AutoDiscoverDerivedTypes = false` if you're building types manually

### PKVIS002: Type must be partial

**Severity:** Error

The base type (class or struct, not interface) must be declared as `partial` to allow Accept method generation.

```csharp
[GenerateVisitor]
public class NonPartialBase { } // Error!

// Fix:
[GenerateVisitor]
public partial class PartialBase { } // Correct
```

**Solution:** Add the `partial` keyword to the type declaration.

### PKVIS004: Derived type must be partial

**Severity:** Error

All derived types must be `partial` to allow Accept method generation.

```csharp
[GenerateVisitor]
public partial class Base { }

public class Derived : Base { } // Error!

// Fix:
public partial class Derived : Base { } // Correct
```

**Solution:** Add the `partial` keyword to all derived types in the hierarchy.

## Troubleshooting

### "No handler registered for type X"

This exception means a visitor encountered a type without a matching handler and no default was provided. Solutions:

1. Add a handler for the specific type
2. Add a default handler
3. Ensure `AutoDiscoverDerivedTypes = true` if using derived types

### "Type must be partial"

The generator requires all visitable types to be declared as `partial`. Add the `partial` keyword:

```csharp
public partial class MyType { }  // ✓ Correct
public class MyType { }          // ✗ Error
```

### Generated code not updating

Try:
1. Clean and rebuild: `dotnet clean && dotnet build`
2. Delete `obj` and `bin` folders
3. Restart your IDE

### Warnings about hiding inherited members

These warnings are expected when derived types have their own Accept methods. They can be safely suppressed or ignored as they don't affect functionality.

## See Also

- [Visitor Pattern (Runtime)](../../../patterns/behavioral/visitor/visitor.md)
- [ActionVisitor Pattern](../../../patterns/behavioral/visitor/actionvisitor.md)
- [AsyncVisitor Pattern](../../../patterns/behavioral/visitor/asyncvisitor.md)
- [AsyncActionVisitor Pattern](../../../patterns/behavioral/visitor/asyncactionvisitor.md)
- [Document Processing Example](../../../examples/document-processing-visitor.md)
