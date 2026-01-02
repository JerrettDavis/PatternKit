# Migrating from Traditional Patterns

A guide to migrating from traditional object-oriented pattern implementations to PatternKit's fluent API.

---

## Why Migrate?

Traditional GoF pattern implementations often require:
- Multiple classes per pattern (interfaces, concrete handlers, builders)
- Significant boilerplate code
- Manual wiring of components
- Verbose configuration

PatternKit provides:
- **Fluent builders** — configure in one expression
- **Type safety** — compile-time validation
- **Immutability** — thread-safe by default
- **Composability** — patterns work together seamlessly
- **Less code** — typically 50-80% reduction

---

## Migration Guide by Pattern

### Strategy Pattern

#### Traditional Implementation

```csharp
// Interface
public interface IShippingStrategy
{
    decimal Calculate(Order order);
}

// Concrete strategies
public class StandardShipping : IShippingStrategy
{
    public decimal Calculate(Order order) =>
        order.Weight * 2.5m + 5.0m;
}

public class ExpressShipping : IShippingStrategy
{
    public decimal Calculate(Order order) =>
        order.Weight * 5.0m + 15.0m;
}

public class FreeShipping : IShippingStrategy
{
    public decimal Calculate(Order order) => 0m;
}

// Context class
public class ShippingCalculator
{
    private readonly Dictionary<ShippingMethod, IShippingStrategy> _strategies;

    public ShippingCalculator()
    {
        _strategies = new Dictionary<ShippingMethod, IShippingStrategy>
        {
            [ShippingMethod.Standard] = new StandardShipping(),
            [ShippingMethod.Express] = new ExpressShipping(),
            [ShippingMethod.Free] = new FreeShipping()
        };
    }

    public decimal Calculate(Order order)
    {
        return _strategies[order.ShippingMethod].Calculate(order);
    }
}

// Usage
var calculator = new ShippingCalculator();
var cost = calculator.Calculate(order);
```

**Lines of code**: ~40

#### PatternKit Implementation

```csharp
var shippingStrategy = Strategy<Order, decimal>.Create()
    .When(o => o.ShippingMethod == ShippingMethod.Standard)
        .Then(o => o.Weight * 2.5m + 5.0m)
    .When(o => o.ShippingMethod == ShippingMethod.Express)
        .Then(o => o.Weight * 5.0m + 15.0m)
    .When(o => o.ShippingMethod == ShippingMethod.Free)
        .Then(_ => 0m)
    .Default(o => o.Weight * 2.5m + 5.0m)
    .Build();

// Usage
var cost = shippingStrategy.Execute(order);
```

**Lines of code**: ~12 (70% reduction)

---

### Chain of Responsibility

#### Traditional Implementation

```csharp
// Base handler
public abstract class OrderValidator
{
    protected OrderValidator? _next;

    public OrderValidator SetNext(OrderValidator next)
    {
        _next = next;
        return next;
    }

    public abstract ValidationResult Handle(Order order);

    protected ValidationResult HandleNext(Order order)
    {
        return _next?.Handle(order) ?? new ValidationResult(true);
    }
}

// Concrete handlers
public class EmptyOrderValidator : OrderValidator
{
    public override ValidationResult Handle(Order order)
    {
        if (order.Items.Count == 0)
            return new ValidationResult(false, "Order is empty");
        return HandleNext(order);
    }
}

public class QuantityValidator : OrderValidator
{
    public override ValidationResult Handle(Order order)
    {
        if (order.Items.Any(i => i.Quantity <= 0))
            return new ValidationResult(false, "Invalid quantity");
        return HandleNext(order);
    }
}

public class CreditLimitValidator : OrderValidator
{
    public override ValidationResult Handle(Order order)
    {
        if (order.Total > order.Customer.CreditLimit)
            return new ValidationResult(false, "Exceeds credit limit");
        return HandleNext(order);
    }
}

// Wiring
var validator = new EmptyOrderValidator();
validator
    .SetNext(new QuantityValidator())
    .SetNext(new CreditLimitValidator());

// Usage
var result = validator.Handle(order);
```

**Lines of code**: ~50

#### PatternKit Implementation

```csharp
var validator = ResultChain<Order, ValidationResult>.Create()
    .When(o => o.Items.Count == 0)
        .Then(_ => new ValidationResult(false, "Order is empty"))
    .When(o => o.Items.Any(i => i.Quantity <= 0))
        .Then(_ => new ValidationResult(false, "Invalid quantity"))
    .When(o => o.Total > o.Customer.CreditLimit)
        .Then(_ => new ValidationResult(false, "Exceeds credit limit"))
    .Finally((in Order _, out ValidationResult? r, _) =>
    {
        r = new ValidationResult(true);
        return true;
    })
    .Build();

// Usage
var result = validator.Execute(order);
```

**Lines of code**: ~15 (70% reduction)

---

### Decorator Pattern

#### Traditional Implementation

```csharp
// Interface
public interface INotifier
{
    void Send(Message message);
}

// Base implementation
public class EmailNotifier : INotifier
{
    public void Send(Message message) =>
        Console.WriteLine($"Email: {message.Text}");
}

// Decorators
public abstract class NotifierDecorator : INotifier
{
    protected readonly INotifier _wrapped;

    protected NotifierDecorator(INotifier wrapped)
    {
        _wrapped = wrapped;
    }

    public abstract void Send(Message message);
}

public class SmsDecorator : NotifierDecorator
{
    public SmsDecorator(INotifier wrapped) : base(wrapped) { }

    public override void Send(Message message)
    {
        _wrapped.Send(message);
        Console.WriteLine($"SMS: {message.Text}");
    }
}

public class SlackDecorator : NotifierDecorator
{
    public SlackDecorator(INotifier wrapped) : base(wrapped) { }

    public override void Send(Message message)
    {
        _wrapped.Send(message);
        Console.WriteLine($"Slack: {message.Text}");
    }
}

public class LoggingDecorator : NotifierDecorator
{
    public LoggingDecorator(INotifier wrapped) : base(wrapped) { }

    public override void Send(Message message)
    {
        Console.WriteLine($"[LOG] Sending: {message.Text}");
        _wrapped.Send(message);
        Console.WriteLine($"[LOG] Sent successfully");
    }
}

// Wiring
INotifier notifier = new EmailNotifier();
notifier = new SmsDecorator(notifier);
notifier = new SlackDecorator(notifier);
notifier = new LoggingDecorator(notifier);

// Usage
notifier.Send(new Message("Hello"));
```

**Lines of code**: ~60

#### PatternKit Implementation

```csharp
var notifier = ActionDecorator<Message>.Create(
        msg => Console.WriteLine($"Email: {msg.Text}"))
    .After(msg => Console.WriteLine($"SMS: {msg.Text}"))
    .After(msg => Console.WriteLine($"Slack: {msg.Text}"))
    .Around((msg, next) =>
    {
        Console.WriteLine($"[LOG] Sending: {msg.Text}");
        next(msg);
        Console.WriteLine($"[LOG] Sent successfully");
    })
    .Build();

// Usage
notifier.Execute(new Message("Hello"));
```

**Lines of code**: ~14 (77% reduction)

---

### Factory Pattern

#### Traditional Implementation

```csharp
// Interface
public interface IShape
{
    void Draw();
}

// Concrete products
public class Circle : IShape
{
    public void Draw() => Console.WriteLine("Drawing circle");
}

public class Square : IShape
{
    public void Draw() => Console.WriteLine("Drawing square");
}

public class Triangle : IShape
{
    public void Draw() => Console.WriteLine("Drawing triangle");
}

// Factory
public class ShapeFactory
{
    public IShape Create(string type)
    {
        return type.ToLower() switch
        {
            "circle" => new Circle(),
            "square" => new Square(),
            "triangle" => new Triangle(),
            _ => throw new ArgumentException($"Unknown shape: {type}")
        };
    }
}

// Usage
var factory = new ShapeFactory();
var shape = factory.Create("circle");
shape.Draw();
```

**Lines of code**: ~35

#### PatternKit Implementation

```csharp
var factory = Factory<string, IShape>.Create()
    .Map("circle", () => new Circle())
    .Map("square", () => new Square())
    .Map("triangle", () => new Triangle())
    .Build();

// Usage
var shape = factory.Create("circle");
shape.Draw();
```

**Lines of code**: ~8 (77% reduction)

---

### Observer Pattern

#### Traditional Implementation

```csharp
// Subject interface
public interface ISubject<T>
{
    void Attach(IObserver<T> observer);
    void Detach(IObserver<T> observer);
    void Notify(T data);
}

// Observer interface
public interface IObserver<T>
{
    void Update(T data);
}

// Concrete subject
public class StockTicker : ISubject<StockPrice>
{
    private readonly List<IObserver<StockPrice>> _observers = new();

    public void Attach(IObserver<StockPrice> observer) =>
        _observers.Add(observer);

    public void Detach(IObserver<StockPrice> observer) =>
        _observers.Remove(observer);

    public void Notify(StockPrice price)
    {
        foreach (var observer in _observers)
            observer.Update(price);
    }

    public void UpdatePrice(StockPrice price) =>
        Notify(price);
}

// Concrete observers
public class EmailAlert : IObserver<StockPrice>
{
    public void Update(StockPrice price) =>
        Console.WriteLine($"Email: {price.Symbol} is now {price.Value}");
}

public class SmsAlert : IObserver<StockPrice>
{
    public void Update(StockPrice price) =>
        Console.WriteLine($"SMS: {price.Symbol} is now {price.Value}");
}

// Usage
var ticker = new StockTicker();
ticker.Attach(new EmailAlert());
ticker.Attach(new SmsAlert());
ticker.UpdatePrice(new StockPrice("AAPL", 150.00m));
```

**Lines of code**: ~50

#### PatternKit Implementation

```csharp
var ticker = Observer<StockPrice>.Create().Build();

// Subscribe handlers directly
ticker.Subscribe(price =>
    Console.WriteLine($"Email: {price.Symbol} is now {price.Value}"));

ticker.Subscribe(price =>
    Console.WriteLine($"SMS: {price.Symbol} is now {price.Value}"));

// Filtered subscription
ticker.Subscribe(
    price => price.Value > 200,
    price => Console.WriteLine($"ALERT: {price.Symbol} exceeded $200!"));

// Usage
ticker.Publish(new StockPrice("AAPL", 150.00m));
```

**Lines of code**: ~12 (76% reduction)

---

### Command Pattern

#### Traditional Implementation

```csharp
// Command interface
public interface ICommand
{
    void Execute();
    void Undo();
}

// Receiver
public class TextEditor
{
    public string Text { get; set; } = "";
}

// Concrete commands
public class InsertTextCommand : ICommand
{
    private readonly TextEditor _editor;
    private readonly string _text;
    private readonly int _position;

    public InsertTextCommand(TextEditor editor, string text, int position)
    {
        _editor = editor;
        _text = text;
        _position = position;
    }

    public void Execute() =>
        _editor.Text = _editor.Text.Insert(_position, _text);

    public void Undo() =>
        _editor.Text = _editor.Text.Remove(_position, _text.Length);
}

public class DeleteTextCommand : ICommand
{
    private readonly TextEditor _editor;
    private readonly int _start;
    private readonly int _length;
    private string _deletedText = "";

    public DeleteTextCommand(TextEditor editor, int start, int length)
    {
        _editor = editor;
        _start = start;
        _length = length;
    }

    public void Execute()
    {
        _deletedText = _editor.Text.Substring(_start, _length);
        _editor.Text = _editor.Text.Remove(_start, _length);
    }

    public void Undo() =>
        _editor.Text = _editor.Text.Insert(_start, _deletedText);
}

// Invoker
public class CommandHistory
{
    private readonly Stack<ICommand> _history = new();

    public void Execute(ICommand command)
    {
        command.Execute();
        _history.Push(command);
    }

    public void Undo()
    {
        if (_history.TryPop(out var command))
            command.Undo();
    }
}

// Usage
var editor = new TextEditor();
var history = new CommandHistory();
history.Execute(new InsertTextCommand(editor, "Hello", 0));
history.Execute(new InsertTextCommand(editor, " World", 5));
history.Undo();
```

**Lines of code**: ~70

#### PatternKit Implementation

```csharp
var editor = new TextEditor();
var history = new Stack<Command<TextEditor>>();

// Create command
Command<TextEditor> CreateInsertCommand(string text, int position)
{
    var capturedText = text;
    var capturedPos = position;

    return Command<TextEditor>.Create()
        .Execute(e => e.Text = e.Text.Insert(capturedPos, capturedText))
        .Undo(e => e.Text = e.Text.Remove(capturedPos, capturedText.Length))
        .Build();
}

// Usage
var insertHello = CreateInsertCommand("Hello", 0);
insertHello.Execute(editor);
history.Push(insertHello);

var insertWorld = CreateInsertCommand(" World", 5);
insertWorld.Execute(editor);
history.Push(insertWorld);

// Undo
history.Pop().Undo(editor);
```

**Lines of code**: ~20 (71% reduction)

---

### Visitor Pattern

#### Traditional Implementation

```csharp
// Element interface
public interface IElement
{
    void Accept(IVisitor visitor);
}

// Concrete elements
public class Circle : IElement
{
    public double Radius { get; }
    public Circle(double radius) => Radius = radius;
    public void Accept(IVisitor visitor) => visitor.Visit(this);
}

public class Rectangle : IElement
{
    public double Width { get; }
    public double Height { get; }
    public Rectangle(double w, double h) { Width = w; Height = h; }
    public void Accept(IVisitor visitor) => visitor.Visit(this);
}

// Visitor interface
public interface IVisitor
{
    void Visit(Circle circle);
    void Visit(Rectangle rectangle);
}

// Concrete visitors
public class AreaCalculator : IVisitor
{
    public double TotalArea { get; private set; }

    public void Visit(Circle circle) =>
        TotalArea += Math.PI * circle.Radius * circle.Radius;

    public void Visit(Rectangle rectangle) =>
        TotalArea += rectangle.Width * rectangle.Height;
}

public class PerimeterCalculator : IVisitor
{
    public double TotalPerimeter { get; private set; }

    public void Visit(Circle circle) =>
        TotalPerimeter += 2 * Math.PI * circle.Radius;

    public void Visit(Rectangle rectangle) =>
        TotalPerimeter += 2 * (rectangle.Width + rectangle.Height);
}

// Usage
var shapes = new List<IElement> { new Circle(5), new Rectangle(4, 6) };
var areaCalc = new AreaCalculator();
foreach (var shape in shapes)
    shape.Accept(areaCalc);
Console.WriteLine($"Total area: {areaCalc.TotalArea}");
```

**Lines of code**: ~55

#### PatternKit Implementation

```csharp
// Using TypeDispatcher for visitor behavior
var areaCalculator = TypeDispatcher<Shape, double>.Create()
    .On<Circle>(c => Math.PI * c.Radius * c.Radius)
    .On<Rectangle>(r => r.Width * r.Height)
    .Default(_ => 0)
    .Build();

var perimeterCalculator = TypeDispatcher<Shape, double>.Create()
    .On<Circle>(c => 2 * Math.PI * c.Radius)
    .On<Rectangle>(r => 2 * (r.Width + r.Height))
    .Default(_ => 0)
    .Build();

// Usage
var shapes = new List<Shape> { new Circle(5), new Rectangle(4, 6) };
var totalArea = shapes.Sum(s => areaCalculator.Dispatch(s));
Console.WriteLine($"Total area: {totalArea}");
```

**Lines of code**: ~15 (73% reduction)

---

## Migration Checklist

### Before Starting

- [ ] Identify patterns in existing codebase
- [ ] Review PatternKit equivalents
- [ ] Plan migration order (start with isolated patterns)
- [ ] Ensure test coverage for existing implementations

### During Migration

- [ ] Migrate one pattern at a time
- [ ] Maintain same public API initially
- [ ] Add tests for PatternKit implementation
- [ ] Run both implementations in parallel (if possible)
- [ ] Verify behavior matches

### After Migration

- [ ] Remove old implementations
- [ ] Update documentation
- [ ] Review for composition opportunities
- [ ] Profile performance

---

## Common Migration Scenarios

### Multiple Classes → Single Builder

Traditional patterns often span 5-10 classes. PatternKit consolidates into one fluent expression:

```csharp
// Before: IHandler, HandlerBase, ConcreteHandler1, ConcreteHandler2, ...
// After:
var handler = ResultChain<Request, Response>.Create()
    .When(...).Then(...)
    .When(...).Then(...)
    .Build();
```

### Constructor Injection → Delegate Passing

```csharp
// Before
public class Decorator
{
    private readonly IComponent _wrapped;
    public Decorator(IComponent wrapped) => _wrapped = wrapped;
}

// After
var decorator = Decorator<Input, Output>.Create(wrappedFunc)
    .After(...)
    .Build();
```

### Inheritance Hierarchy → Composition

```csharp
// Before: Abstract base with virtual methods
public abstract class TemplateMethod
{
    public void Execute()
    {
        Step1();
        Step2();
        Step3();
    }
    protected abstract void Step1();
    protected abstract void Step2();
    protected virtual void Step3() { }
}

// After: Compose steps directly
var template = Template<Context>.Create()
    .Step(Step1Action)
    .Step(Step2Action)
    .Step(Step3Action)
    .Build();
```

---

## Preserving Existing APIs

If you need to maintain backward compatibility:

```csharp
// Legacy interface
public interface IShippingCalculator
{
    decimal Calculate(Order order);
}

// Adapter using PatternKit internally
public class ShippingCalculatorAdapter : IShippingCalculator
{
    private readonly Strategy<Order, decimal> _strategy;

    public ShippingCalculatorAdapter()
    {
        _strategy = Strategy<Order, decimal>.Create()
            .When(o => o.IsExpress).Then(o => o.Weight * 5m)
            .Default(o => o.Weight * 2.5m)
            .Build();
    }

    public decimal Calculate(Order order) =>
        _strategy.Execute(order);
}
```

---

## Performance Considerations

PatternKit typically performs comparably to hand-written code:

| Metric | Traditional | PatternKit |
|--------|-------------|------------|
| Execution overhead | Minimal | ~10-20 ns |
| Memory (runtime) | Similar | Similar |
| Lines of code | 5-10× more | Baseline |
| Build time | N/A | One-time |

For hot paths (> 1M calls/sec), benchmark both implementations.

---

## See Also

- [Choosing Patterns](choosing-patterns.md)
- [Composing Patterns](composing-patterns.md)
- [Performance Guide](performance.md)
- [Testing Patterns](testing.md)
