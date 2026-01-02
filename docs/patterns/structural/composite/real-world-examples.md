# Composite Pattern Real-World Examples

Production-ready examples demonstrating the Composite pattern in real-world scenarios.

---

## Example 1: Organization Hierarchy Reporting

### The Problem

A company needs to calculate various metrics (headcount, salary budget, vacation days) across its organization hierarchy, with departments containing teams containing employees.

### The Solution

Use Composite to model the organization tree and aggregate metrics uniformly.

### The Code

```csharp
public abstract class OrganizationUnit
{
    public string Name { get; }
    public abstract Composite<ReportContext, OrgMetrics>.Builder ToComposite();
}

public class Employee : OrganizationUnit
{
    public decimal Salary { get; }
    public int VacationDaysRemaining { get; }
    public string Role { get; }

    public override Composite<ReportContext, OrgMetrics>.Builder ToComposite() =>
        Composite<ReportContext, OrgMetrics>.Leaf((in ReportContext ctx) =>
            new OrgMetrics
            {
                Headcount = 1,
                TotalSalary = Salary,
                TotalVacationDays = VacationDaysRemaining,
                SeniorCount = Role.Contains("Senior") ? 1 : 0,
                ManagerCount = Role.Contains("Manager") ? 1 : 0
            });
}

public class Team : OrganizationUnit
{
    public List<Employee> Members { get; } = new();

    public override Composite<ReportContext, OrgMetrics>.Builder ToComposite()
    {
        var builder = Composite<ReportContext, OrgMetrics>
            .Node(
                static (in ReportContext _) => new OrgMetrics(),
                static (in ReportContext _, OrgMetrics acc, OrgMetrics child) =>
                    new OrgMetrics
                    {
                        Headcount = acc.Headcount + child.Headcount,
                        TotalSalary = acc.TotalSalary + child.TotalSalary,
                        TotalVacationDays = acc.TotalVacationDays + child.TotalVacationDays,
                        SeniorCount = acc.SeniorCount + child.SeniorCount,
                        ManagerCount = acc.ManagerCount + child.ManagerCount
                    });

        foreach (var member in Members)
            builder.AddChild(member.ToComposite());

        return builder;
    }
}

public class Department : OrganizationUnit
{
    public List<OrganizationUnit> Units { get; } = new(); // Teams or sub-departments

    public override Composite<ReportContext, OrgMetrics>.Builder ToComposite()
    {
        var builder = Composite<ReportContext, OrgMetrics>
            .Node(
                static (in ReportContext _) => new OrgMetrics(),
                static (in ReportContext _, OrgMetrics acc, OrgMetrics child) =>
                    new OrgMetrics
                    {
                        Headcount = acc.Headcount + child.Headcount,
                        TotalSalary = acc.TotalSalary + child.TotalSalary,
                        TotalVacationDays = acc.TotalVacationDays + child.TotalVacationDays,
                        SeniorCount = acc.SeniorCount + child.SeniorCount,
                        ManagerCount = acc.ManagerCount + child.ManagerCount
                    });

        foreach (var unit in Units)
            builder.AddChild(unit.ToComposite());

        return builder;
    }
}

public record ReportContext(DateTime ReportDate, bool IncludeContractors);
public record OrgMetrics
{
    public int Headcount { get; init; }
    public decimal TotalSalary { get; init; }
    public int TotalVacationDays { get; init; }
    public int SeniorCount { get; init; }
    public int ManagerCount { get; init; }
    public decimal AverageSalary => Headcount > 0 ? TotalSalary / Headcount : 0;
}

// Usage
var engineering = new Department
{
    Name = "Engineering",
    Units = {
        new Team {
            Name = "Platform",
            Members = {
                new Employee { Name = "Alice", Role = "Senior Engineer", Salary = 150000, VacationDaysRemaining = 15 },
                new Employee { Name = "Bob", Role = "Engineer", Salary = 120000, VacationDaysRemaining = 20 }
            }
        },
        new Team {
            Name = "Product",
            Members = {
                new Employee { Name = "Carol", Role = "Engineering Manager", Salary = 180000, VacationDaysRemaining = 10 },
                new Employee { Name = "Dave", Role = "Senior Engineer", Salary = 160000, VacationDaysRemaining = 12 }
            }
        }
    }
};

var reporter = engineering.ToComposite().Build();
var metrics = reporter.Execute(new ReportContext(DateTime.Today, true));
// Headcount: 4, TotalSalary: 610000, AverageSalary: 152500, SeniorCount: 2, ManagerCount: 1
```

### Why This Pattern

- **Uniform aggregation**: Same interface for employee, team, department
- **Hierarchical computation**: Metrics roll up naturally
- **Extensible**: Add new organization types easily
- **Composable**: Build trees dynamically from data

---

## Example 2: Document Rendering System

### The Problem

A document editor needs to render documents with nested structures (sections, paragraphs, inline elements) to various output formats while calculating layout metrics.

### The Solution

Use Composite to model document structure and render recursively.

### The Code

```csharp
public abstract class DocumentElement
{
    public abstract Composite<RenderContext, RenderResult>.Builder ToComposite();
}

public class TextRun : DocumentElement
{
    public string Text { get; }
    public TextStyle Style { get; }

    public override Composite<RenderContext, RenderResult>.Builder ToComposite() =>
        Composite<RenderContext, RenderResult>.Leaf((in RenderContext ctx) =>
        {
            var formatted = ctx.Format switch
            {
                OutputFormat.Html => $"<span style=\"{Style.ToCss()}\">{HtmlEncode(Text)}</span>",
                OutputFormat.Markdown => Style.ToMarkdown(Text),
                OutputFormat.PlainText => Text,
                _ => Text
            };

            return new RenderResult
            {
                Output = formatted,
                CharCount = Text.Length,
                WordCount = Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
            };
        });
}

public class Paragraph : DocumentElement
{
    public List<DocumentElement> Children { get; } = new();

    public override Composite<RenderContext, RenderResult>.Builder ToComposite()
    {
        var builder = Composite<RenderContext, RenderResult>
            .Node(
                static (in RenderContext ctx) => new RenderResult
                {
                    Output = ctx.Format == OutputFormat.Html ? "<p>" : "",
                    CharCount = 0,
                    WordCount = 0
                },
                static (in RenderContext ctx, RenderResult acc, RenderResult child) =>
                    new RenderResult
                    {
                        Output = acc.Output + child.Output,
                        CharCount = acc.CharCount + child.CharCount,
                        WordCount = acc.WordCount + child.WordCount
                    });

        foreach (var child in Children)
            builder.AddChild(child.ToComposite());

        // Add closing tag after children
        return Composite<RenderContext, RenderResult>
            .Node(
                static (in RenderContext _) => new RenderResult(),
                static (in RenderContext ctx, RenderResult acc, RenderResult child) =>
                    new RenderResult
                    {
                        Output = child.Output + (ctx.Format == OutputFormat.Html ? "</p>\n" : "\n\n"),
                        CharCount = child.CharCount,
                        WordCount = child.WordCount
                    })
            .AddChild(builder);
    }
}

public class Section : DocumentElement
{
    public string Title { get; }
    public int Level { get; }
    public List<DocumentElement> Children { get; } = new();

    public override Composite<RenderContext, RenderResult>.Builder ToComposite()
    {
        var builder = Composite<RenderContext, RenderResult>
            .Node(
                (in RenderContext ctx) =>
                {
                    var header = ctx.Format switch
                    {
                        OutputFormat.Html => $"<h{Level}>{HtmlEncode(Title)}</h{Level}>\n",
                        OutputFormat.Markdown => $"{new string('#', Level)} {Title}\n\n",
                        OutputFormat.PlainText => $"{Title.ToUpper()}\n{new string('=', Title.Length)}\n\n",
                        _ => Title + "\n"
                    };
                    return new RenderResult { Output = header, CharCount = Title.Length, WordCount = Title.Split(' ').Length };
                },
                static (in RenderContext _, RenderResult acc, RenderResult child) =>
                    new RenderResult
                    {
                        Output = acc.Output + child.Output,
                        CharCount = acc.CharCount + child.CharCount,
                        WordCount = acc.WordCount + child.WordCount
                    });

        foreach (var child in Children)
            builder.AddChild(child.ToComposite());

        return builder;
    }
}

public class Document : DocumentElement
{
    public string Title { get; }
    public List<Section> Sections { get; } = new();

    public override Composite<RenderContext, RenderResult>.Builder ToComposite()
    {
        var builder = Composite<RenderContext, RenderResult>
            .Node(
                (in RenderContext ctx) =>
                {
                    var header = ctx.Format switch
                    {
                        OutputFormat.Html => $"<!DOCTYPE html>\n<html>\n<head><title>{Title}</title></head>\n<body>\n",
                        OutputFormat.Markdown => $"# {Title}\n\n",
                        _ => $"{Title}\n\n"
                    };
                    return new RenderResult { Output = header };
                },
                static (in RenderContext _, RenderResult acc, RenderResult child) =>
                    new RenderResult
                    {
                        Output = acc.Output + child.Output,
                        CharCount = acc.CharCount + child.CharCount,
                        WordCount = acc.WordCount + child.WordCount
                    });

        foreach (var section in Sections)
            builder.AddChild(section.ToComposite());

        return Composite<RenderContext, RenderResult>
            .Node(
                static (in RenderContext _) => new RenderResult(),
                static (in RenderContext ctx, RenderResult acc, RenderResult child) =>
                {
                    var footer = ctx.Format == OutputFormat.Html ? "</body>\n</html>" : "";
                    return new RenderResult
                    {
                        Output = child.Output + footer,
                        CharCount = child.CharCount,
                        WordCount = child.WordCount
                    };
                })
            .AddChild(builder);
    }
}

public record RenderContext(OutputFormat Format);
public record RenderResult
{
    public string Output { get; init; } = "";
    public int CharCount { get; init; }
    public int WordCount { get; init; }
}

// Usage
var doc = new Document
{
    Title = "Annual Report",
    Sections = {
        new Section {
            Title = "Executive Summary",
            Level = 2,
            Children = {
                new Paragraph {
                    Children = {
                        new TextRun { Text = "This report covers ", Style = TextStyle.Normal },
                        new TextRun { Text = "key metrics", Style = TextStyle.Bold },
                        new TextRun { Text = " for the fiscal year.", Style = TextStyle.Normal }
                    }
                }
            }
        }
    }
};

var renderer = doc.ToComposite().Build();
var html = renderer.Execute(new RenderContext(OutputFormat.Html));
var markdown = renderer.Execute(new RenderContext(OutputFormat.Markdown));
```

### Why This Pattern

- **Nested structure**: Documents naturally form trees
- **Format-agnostic**: Same structure renders to multiple formats
- **Metrics collection**: Word count, char count aggregated automatically
- **Composable**: Build documents programmatically

---

## Example 3: Menu System with Pricing

### The Problem

A restaurant POS system needs to handle menu items that can be standalone or composed (combos, meal deals) while calculating prices with various modifiers.

### The Solution

Use Composite to model menu structure and compute prices with modifiers.

### The Code

```csharp
public abstract class MenuItem
{
    public abstract Composite<OrderContext, PriceResult>.Builder ToPriceComposite();
}

public class SimpleItem : MenuItem
{
    public string Name { get; }
    public decimal BasePrice { get; }
    public string Category { get; }

    public override Composite<OrderContext, PriceResult>.Builder ToPriceComposite() =>
        Composite<OrderContext, PriceResult>.Leaf((in OrderContext ctx) =>
        {
            var price = BasePrice;

            // Apply time-based pricing
            if (ctx.IsHappyHour && Category == "Drinks")
                price *= 0.5m;

            // Apply size modifier
            price *= ctx.SizeMultiplier;

            return new PriceResult
            {
                Subtotal = price,
                Items = new[] { new LineItem(Name, price, 1) }
            };
        });
}

public class ComboMeal : MenuItem
{
    public string Name { get; }
    public List<MenuItem> Items { get; } = new();
    public decimal DiscountPercent { get; } = 0.1m; // 10% combo discount

    public override Composite<OrderContext, PriceResult>.Builder ToPriceComposite()
    {
        var itemsBuilder = Composite<OrderContext, PriceResult>
            .Node(
                static (in OrderContext _) => new PriceResult(),
                static (in OrderContext _, PriceResult acc, PriceResult child) =>
                    new PriceResult
                    {
                        Subtotal = acc.Subtotal + child.Subtotal,
                        Items = acc.Items.Concat(child.Items).ToArray()
                    });

        foreach (var item in Items)
            itemsBuilder.AddChild(item.ToPriceComposite());

        // Wrap with discount application
        return Composite<OrderContext, PriceResult>
            .Node(
                static (in OrderContext _) => new PriceResult(),
                (in OrderContext _, PriceResult acc, PriceResult itemsResult) =>
                {
                    var discount = itemsResult.Subtotal * DiscountPercent;
                    return new PriceResult
                    {
                        Subtotal = itemsResult.Subtotal - discount,
                        Discount = discount,
                        Items = itemsResult.Items
                            .Append(new LineItem($"{Name} Discount", -discount, 1))
                            .ToArray()
                    };
                })
            .AddChild(itemsBuilder);
    }
}

public class CustomizableItem : MenuItem
{
    public SimpleItem BaseItem { get; }
    public List<Modifier> Modifiers { get; } = new();

    public override Composite<OrderContext, PriceResult>.Builder ToPriceComposite()
    {
        var builder = Composite<OrderContext, PriceResult>
            .Node(
                static (in OrderContext _) => new PriceResult(),
                static (in OrderContext _, PriceResult acc, PriceResult child) =>
                    new PriceResult
                    {
                        Subtotal = acc.Subtotal + child.Subtotal,
                        Items = acc.Items.Concat(child.Items).ToArray()
                    });

        // Add base item
        builder.AddChild(BaseItem.ToPriceComposite());

        // Add modifiers
        foreach (var mod in Modifiers)
        {
            builder.AddChild(
                Composite<OrderContext, PriceResult>.Leaf((in OrderContext _) =>
                    new PriceResult
                    {
                        Subtotal = mod.PriceAdjustment,
                        Items = new[] { new LineItem($"  + {mod.Name}", mod.PriceAdjustment, 1) }
                    }));
        }

        return builder;
    }
}

public record OrderContext(bool IsHappyHour, decimal SizeMultiplier);
public record PriceResult
{
    public decimal Subtotal { get; init; }
    public decimal Discount { get; init; }
    public LineItem[] Items { get; init; } = Array.Empty<LineItem>();
}
public record LineItem(string Name, decimal Price, int Quantity);
public record Modifier(string Name, decimal PriceAdjustment);

// Usage
var combo = new ComboMeal
{
    Name = "Lunch Special",
    Items = {
        new CustomizableItem {
            BaseItem = new SimpleItem { Name = "Burger", BasePrice = 8.99m, Category = "Entrees" },
            Modifiers = {
                new Modifier("Extra Cheese", 1.50m),
                new Modifier("Bacon", 2.00m)
            }
        },
        new SimpleItem { Name = "Fries", BasePrice = 3.99m, Category = "Sides" },
        new SimpleItem { Name = "Soda", BasePrice = 2.49m, Category = "Drinks" }
    }
};

var priceCalculator = combo.ToPriceComposite().Build();
var result = priceCalculator.Execute(new OrderContext(IsHappyHour: true, SizeMultiplier: 1.0m));
// Items: Burger 8.99, +Extra Cheese 1.50, +Bacon 2.00, Fries 3.99, Soda 1.245 (50% off), Discount -1.77
// Subtotal: 15.96
```

### Why This Pattern

- **Flexible composition**: Combos contain items, items have modifiers
- **Price aggregation**: Totals computed through tree traversal
- **Context-aware**: Happy hour, sizes affect leaf calculations
- **Discount application**: Combo discount applied at node level

---

## Example 4: Permission Evaluation System

### The Problem

An enterprise application needs to evaluate complex permission rules that can be combined with AND/OR logic, including role-based, attribute-based, and time-based rules.

### The Solution

Use Composite to build permission rule trees that evaluate to allow/deny decisions.

### The Code

```csharp
public abstract class PermissionRule
{
    public abstract Composite<PermissionContext, PermissionResult>.Builder ToComposite();
}

public class RoleRule : PermissionRule
{
    public string RequiredRole { get; }

    public override Composite<PermissionContext, PermissionResult>.Builder ToComposite() =>
        Composite<PermissionContext, PermissionResult>.Leaf((in PermissionContext ctx) =>
        {
            var hasRole = ctx.User.Roles.Contains(RequiredRole);
            return new PermissionResult
            {
                Allowed = hasRole,
                Reason = hasRole ? $"User has role '{RequiredRole}'" : $"Missing role '{RequiredRole}'",
                RulesEvaluated = 1
            };
        });
}

public class AttributeRule : PermissionRule
{
    public string Attribute { get; }
    public string ExpectedValue { get; }

    public override Composite<PermissionContext, PermissionResult>.Builder ToComposite() =>
        Composite<PermissionContext, PermissionResult>.Leaf((in PermissionContext ctx) =>
        {
            var hasAttribute = ctx.Resource.Attributes.TryGetValue(Attribute, out var value)
                && value == ExpectedValue;
            return new PermissionResult
            {
                Allowed = hasAttribute,
                Reason = hasAttribute
                    ? $"Resource {Attribute}='{ExpectedValue}'"
                    : $"Resource {Attribute} mismatch",
                RulesEvaluated = 1
            };
        });
}

public class TimeWindowRule : PermissionRule
{
    public TimeSpan StartTime { get; }
    public TimeSpan EndTime { get; }

    public override Composite<PermissionContext, PermissionResult>.Builder ToComposite() =>
        Composite<PermissionContext, PermissionResult>.Leaf((in PermissionContext ctx) =>
        {
            var now = ctx.RequestTime.TimeOfDay;
            var inWindow = now >= StartTime && now <= EndTime;
            return new PermissionResult
            {
                Allowed = inWindow,
                Reason = inWindow
                    ? $"Within allowed time window"
                    : $"Outside allowed hours ({StartTime}-{EndTime})",
                RulesEvaluated = 1
            };
        });
}

public class AndRule : PermissionRule
{
    public List<PermissionRule> Rules { get; } = new();

    public override Composite<PermissionContext, PermissionResult>.Builder ToComposite()
    {
        var builder = Composite<PermissionContext, PermissionResult>
            .Node(
                static (in PermissionContext _) => new PermissionResult { Allowed = true },
                static (in PermissionContext _, PermissionResult acc, PermissionResult child) =>
                    new PermissionResult
                    {
                        Allowed = acc.Allowed && child.Allowed,
                        Reason = !child.Allowed ? child.Reason : acc.Reason,
                        RulesEvaluated = acc.RulesEvaluated + child.RulesEvaluated
                    });

        foreach (var rule in Rules)
            builder.AddChild(rule.ToComposite());

        return builder;
    }
}

public class OrRule : PermissionRule
{
    public List<PermissionRule> Rules { get; } = new();

    public override Composite<PermissionContext, PermissionResult>.Builder ToComposite()
    {
        var builder = Composite<PermissionContext, PermissionResult>
            .Node(
                static (in PermissionContext _) => new PermissionResult { Allowed = false },
                static (in PermissionContext _, PermissionResult acc, PermissionResult child) =>
                    new PermissionResult
                    {
                        Allowed = acc.Allowed || child.Allowed,
                        Reason = child.Allowed ? child.Reason : acc.Reason,
                        RulesEvaluated = acc.RulesEvaluated + child.RulesEvaluated
                    });

        foreach (var rule in Rules)
            builder.AddChild(rule.ToComposite());

        return builder;
    }
}

public record PermissionContext(User User, Resource Resource, DateTime RequestTime);
public record PermissionResult
{
    public bool Allowed { get; init; }
    public string Reason { get; init; } = "";
    public int RulesEvaluated { get; init; }
}

// Usage: User can access if (Admin OR (Editor AND resource is Draft)) AND within business hours
var policy = new AndRule
{
    Rules = {
        new OrRule {
            Rules = {
                new RoleRule { RequiredRole = "Admin" },
                new AndRule {
                    Rules = {
                        new RoleRule { RequiredRole = "Editor" },
                        new AttributeRule { Attribute = "status", ExpectedValue = "Draft" }
                    }
                }
            }
        },
        new TimeWindowRule { StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17) }
    }
};

var evaluator = policy.ToComposite().Build();
var result = evaluator.Execute(new PermissionContext(user, resource, DateTime.Now));
```

### Why This Pattern

- **Boolean composition**: AND/OR rules combine naturally
- **Extensible rules**: Add new rule types without changing logic
- **Explainable**: Reason strings explain decisions
- **Measurable**: Count rules evaluated for auditing

---

## Key Takeaways

1. **Part-whole hierarchies**: Composite shines for tree structures
2. **Uniform interface**: Leaves and nodes share Execute
3. **Bottom-up aggregation**: Results fold from leaves to root
4. **Type-safe composition**: Compile-time tree building
5. **Immutable evaluation**: Thread-safe after Build

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
