# Interpreter Pattern Real-World Examples

Production-ready examples demonstrating the Interpreter pattern in real-world scenarios.

---

## Example 1: E-Commerce Pricing Engine

### The Problem

An e-commerce platform needs to evaluate complex discount rules that vary by customer tier, cart total, promotional periods, and promo codes. Rules need to be:

- Defined by business analysts, not developers
- Composable (combine multiple discounts)
- Testable without code changes
- Executable in real-time during checkout

### The Solution

Use the Interpreter pattern to create a pricing DSL that evaluates discount expressions against order context.

### The Code

```csharp
public sealed class PricingContext
{
    public decimal CartTotal { get; set; }
    public int ItemCount { get; set; }
    public string CustomerTier { get; set; } = "Standard";
    public bool IsHoliday { get; set; }
    public string PromoCode { get; set; } = "";
    public Dictionary<string, decimal> Variables { get; set; } = new();
}

public static Interpreter<PricingContext, decimal> CreatePricingInterpreter()
{
    return Interpreter.Create<PricingContext, decimal>()
        // Terminals: Read values from context
        .Terminal("number", token => decimal.Parse(token))
        .Terminal("percent", token => decimal.Parse(token.TrimEnd('%')) / 100m)
        .Terminal("var", (token, ctx) => token switch
        {
            "cart_total" => ctx.CartTotal,
            "item_count" => ctx.ItemCount,
            "tier_discount" => ctx.CustomerTier switch
            {
                "Gold" => 0.10m,
                "Platinum" => 0.15m,
                "Diamond" => 0.20m,
                _ => 0m
            },
            _ => ctx.Variables.GetValueOrDefault(token, 0m)
        })

        // Non-terminals: Arithmetic
        .Binary("add", (l, r) => l + r)
        .Binary("sub", (l, r) => l - r)
        .Binary("mul", (l, r) => l * r)
        .Binary("div", (l, r) => r != 0 ? l / r : 0m)
        .Binary("min", Math.Min)
        .Binary("max", Math.Max)
        .Unary("round", v => Math.Round(v, 2))

        // Non-terminals: Comparisons (return 1 for true, 0 for false)
        .Binary("gt", (l, r) => l > r ? 1m : 0m)
        .Binary("gte", (l, r) => l >= r ? 1m : 0m)
        .Binary("lt", (l, r) => l < r ? 1m : 0m)
        .Binary("eq", (l, r) => l == r ? 1m : 0m)

        // Non-terminal: Conditional
        .NonTerminal("if", (args, _) =>
            args[0] > 0 ? args[1] : args[2])

        .Build();
}

// Usage
var interpreter = CreatePricingInterpreter();

// Rule: 10% tier discount on cart total
var tierDiscountRule = NonTerminal("round",
    NonTerminal("mul",
        Terminal("var", "cart_total"),
        Terminal("var", "tier_discount")));

// Rule: $5 off if cart > $50
var thresholdRule = NonTerminal("if",
    NonTerminal("gt", Terminal("var", "cart_total"), Terminal("number", "50")),
    Terminal("number", "5"),
    Terminal("number", "0"));

var context = new PricingContext
{
    CartTotal = 150m,
    CustomerTier = "Gold"
};

var tierDiscount = interpreter.Interpret(tierDiscountRule, context); // 15.00
var thresholdDiscount = interpreter.Interpret(thresholdRule, context); // 5.00
```

### Why This Pattern

- **Business agility**: Rules can be stored in a database and changed without deployment
- **Testability**: Each rule can be unit tested in isolation
- **Composability**: Complex discounts are built from simple, reusable expressions
- **Type safety**: The interpreter ensures expressions evaluate to the correct type

### Alternative Approaches

| Approach | Trade-offs |
|----------|------------|
| Hardcoded rules | Faster but requires code changes for every rule update |
| Dynamic compilation | More powerful but security and complexity concerns |
| Expression trees | More .NET-native but steeper learning curve |
| Rule engine library | Feature-rich but external dependency |

---

## Example 2: Query Filter Language

### The Problem

A REST API needs to support complex filtering on list endpoints. Users should be able to filter with expressions like:

```
status = 'active' AND (priority > 5 OR assignee = 'admin')
```

### The Solution

Create a boolean expression interpreter that evaluates filter conditions against entity properties.

### The Code

```csharp
public sealed class FilterContext
{
    public Dictionary<string, object> Entity { get; set; } = new();

    public object GetProperty(string name) =>
        Entity.TryGetValue(name, out var value) ? value : null!;
}

public static Interpreter<FilterContext, bool> CreateFilterInterpreter()
{
    return Interpreter.Create<FilterContext, bool>()
        // Terminals
        .Terminal("true", _ => true)
        .Terminal("false", _ => false)
        .Terminal("string", token => throw new InvalidOperationException("Use in comparison"))

        // Property comparisons
        .NonTerminal("eq", (args, ctx) => Compare(args, ctx, "eq"))
        .NonTerminal("neq", (args, ctx) => Compare(args, ctx, "neq"))
        .NonTerminal("gt", (args, ctx) => Compare(args, ctx, "gt"))
        .NonTerminal("gte", (args, ctx) => Compare(args, ctx, "gte"))
        .NonTerminal("lt", (args, ctx) => Compare(args, ctx, "lt"))
        .NonTerminal("lte", (args, ctx) => Compare(args, ctx, "lte"))
        .NonTerminal("contains", (args, ctx) => Compare(args, ctx, "contains"))

        // Logical operators
        .Binary("and", (l, r) => l && r)
        .Binary("or", (l, r) => l || r)
        .Unary("not", v => !v)

        .Build();
}

// Helper for property access in comparisons
private static bool Compare(bool[] _, FilterContext ctx, string op)
{
    // In practice, you'd use a different approach to pass property/value pairs
    // This is simplified for demonstration
    throw new NotImplementedException("Use typed comparison expressions");
}

// Alternative: Use a different result type for comparisons
public record ComparisonResult(string Property, string Op, object Value);

public static Interpreter<FilterContext, object> CreateFlexibleFilterInterpreter()
{
    return Interpreter.Create<FilterContext, object>()
        // Terminals
        .Terminal("prop", (name, _) => name)
        .Terminal("string", (value, _) => value)
        .Terminal("number", (value, _) => double.Parse(value))
        .Terminal("bool", (value, _) => bool.Parse(value))

        // Comparisons - return bool
        .Binary("eq", (prop, value, ctx) =>
        {
            var actual = ctx.GetProperty((string)prop);
            return actual?.Equals(value) ?? false;
        })
        .Binary("gt", (prop, value, ctx) =>
        {
            var actual = ctx.GetProperty((string)prop);
            if (actual is IComparable c && value is IComparable v)
                return c.CompareTo(v) > 0;
            return false;
        })

        // Logical - expect bools
        .Binary("and", (l, r) => (bool)l && (bool)r)
        .Binary("or", (l, r) => (bool)l || (bool)r)
        .Unary("not", v => !(bool)v)

        .Build();
}
```

### Why This Pattern

- **User-defined queries**: End users can create their own filter logic
- **Secure evaluation**: No direct code execution, just expression interpretation
- **Extensible operators**: Easy to add new comparison or logical operators

---

## Example 3: Mathematical Formula Evaluator

### The Problem

A spreadsheet application needs to evaluate cell formulas like `=SUM(A1:A10) * 1.08` with support for functions, cell references, and arithmetic.

### The Solution

Build an interpreter for spreadsheet formulas with support for cell references, ranges, and functions.

### The Code

```csharp
public sealed class SpreadsheetContext
{
    private readonly Dictionary<string, double> _cells = new();

    public double GetCell(string reference) =>
        _cells.TryGetValue(reference.ToUpper(), out var value) ? value : 0;

    public void SetCell(string reference, double value) =>
        _cells[reference.ToUpper()] = value;

    public IEnumerable<double> GetRange(string start, string end)
    {
        // Simplified: assumes same column, sequential rows
        // e.g., A1:A5 returns A1, A2, A3, A4, A5
        var col = start[0];
        var startRow = int.Parse(start[1..]);
        var endRow = int.Parse(end[1..]);

        for (var row = startRow; row <= endRow; row++)
            yield return GetCell($"{col}{row}");
    }
}

public static Interpreter<SpreadsheetContext, double> CreateFormulaInterpreter()
{
    return Interpreter.Create<SpreadsheetContext, double>()
        // Terminals
        .Terminal("number", token => double.Parse(token))
        .Terminal("cell", (ref_, ctx) => ctx.GetCell(ref_))

        // Arithmetic
        .Binary("add", (l, r) => l + r)
        .Binary("sub", (l, r) => l - r)
        .Binary("mul", (l, r) => l * r)
        .Binary("div", (l, r) => r != 0 ? l / r : double.NaN)
        .Unary("neg", v => -v)
        .Binary("pow", Math.Pow)

        // Functions (non-terminals with variable children)
        .NonTerminal("sum", (args, _) => args.Sum())
        .NonTerminal("avg", (args, _) => args.Average())
        .NonTerminal("min", (args, _) => args.Min())
        .NonTerminal("max", (args, _) => args.Max())
        .NonTerminal("count", (args, _) => args.Length)

        // Conditional
        .NonTerminal("if", (args, _) =>
            args[0] != 0 ? args[1] : args[2])

        // Comparison
        .Binary("gt", (l, r) => l > r ? 1 : 0)
        .Binary("eq", (l, r) => l == r ? 1 : 0)

        .Build();
}

// Usage
var interpreter = CreateFormulaInterpreter();
var ctx = new SpreadsheetContext();

ctx.SetCell("A1", 10);
ctx.SetCell("A2", 20);
ctx.SetCell("A3", 30);

// Formula: SUM(A1, A2, A3) * 1.08
var formula = NonTerminal("mul",
    NonTerminal("sum",
        Terminal("cell", "A1"),
        Terminal("cell", "A2"),
        Terminal("cell", "A3")),
    Terminal("number", "1.08"));

var result = interpreter.Interpret(formula, ctx); // 64.8
```

### Why This Pattern

- **Dynamic formulas**: Users create formulas that reference other cells
- **Recalculation**: When cells change, dependent formulas are re-evaluated
- **Extensible functions**: Easy to add new spreadsheet functions

---

## Example 4: Access Control Policy Language

### The Problem

An enterprise application needs fine-grained access control with policies like:

```
allow if (role = 'admin') or (role = 'manager' and department = resource.department)
```

### The Solution

Create a policy interpreter that evaluates access decisions based on user and resource attributes.

### The Code

```csharp
public sealed class PolicyContext
{
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "";
    public string Department { get; set; } = "";
    public Dictionary<string, string> ResourceAttributes { get; set; } = new();

    public string GetUserAttr(string name) => name switch
    {
        "role" => Role,
        "department" => Department,
        "id" => UserId,
        _ => ""
    };

    public string GetResourceAttr(string name) =>
        ResourceAttributes.TryGetValue(name, out var v) ? v : "";
}

public static Interpreter<PolicyContext, bool> CreatePolicyInterpreter()
{
    return Interpreter.Create<PolicyContext, bool>()
        // Terminals
        .Terminal("true", _ => true)
        .Terminal("false", _ => false)
        .Terminal("user", (attr, ctx) => ctx.GetUserAttr(attr))
        .Terminal("resource", (attr, ctx) => ctx.GetResourceAttr(attr))
        .Terminal("string", (value, _) => value)

        // String comparison (returns bool)
        .NonTerminal("eq", (args, _) =>
        {
            // args are actually objects - need type-aware handling
            return args[0].Equals(args[1]);
        })

        // Logical
        .Binary("and", (l, r) => l && r)
        .Binary("or", (l, r) => l || r)
        .Unary("not", v => !v)

        .Build();
}
```

### Why This Pattern

- **Declarative policies**: Security policies are expressed as data, not code
- **Auditable**: Policies can be logged and reviewed
- **Externalized**: Policies can be stored and updated without deployment

---

## Key Takeaways

1. **Choose the right abstraction**: The Interpreter pattern works best for simple, well-defined grammars

2. **Keep expressions simple**: Complex logic is better handled by multiple simple interpreters

3. **Use context for state**: Pass environment through context, keep expressions pure

4. **Consider persistence**: Expressions are data and can be stored, versioned, and audited

5. **Combine with other patterns**: Use Factory for interpreter creation, Strategy for selecting interpreters

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
- [InterpreterDemo.cs](/src/PatternKit.Examples/InterpreterDemo/InterpreterDemo.cs) - Full working example
