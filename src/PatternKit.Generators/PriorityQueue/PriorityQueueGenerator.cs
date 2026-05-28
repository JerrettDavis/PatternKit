using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.PriorityQueue;

[Generator]
public sealed class PriorityQueueGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.PriorityQueue.GeneratePriorityQueueAttribute";
    private const string SelectorAttributeName = "PatternKit.Generators.PriorityQueue.PriorityQueuePrioritySelectorAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKPQ001", "Priority Queue host must be partial",
        "Type '{0}' is marked with [GeneratePriorityQueue] but is not declared as partial",
        "PatternKit.Generators.PriorityQueue", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingSelector = new(
        "PKPQ002", "Priority Queue priority selector is missing",
        "Priority Queue type '{0}' must declare exactly one [PriorityQueuePrioritySelector] method",
        "PatternKit.Generators.PriorityQueue", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidSelector = new(
        "PKPQ003", "Priority Queue priority selector signature is invalid",
        "Priority Queue selector '{0}' must be static and return TPriority with one TItem parameter",
        "PatternKit.Generators.PriorityQueue", DiagnosticSeverity.Error, true);

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == AttributeName);
            if (attr is not null)
                Generate(spc, candidate.Type, candidate.Node, attr);
        });
    }

    private static void Generate(SourceProductionContext context, INamedTypeSymbol type, TypeDeclarationSyntax node, AttributeData attribute)
    {
        if (!node.Modifiers.Any(static modifier => modifier.Text == "partial"))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var itemType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var priorityType = attribute.ConstructorArguments.Length >= 2 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (itemType is null || priorityType is null)
            return;

        var selectors = type.GetMembers().OfType<IMethodSymbol>().Where(static method =>
            method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == SelectorAttributeName)).ToArray();
        if (selectors.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingSelector, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (!IsSelector(selectors[0], itemType, priorityType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidSelector, selectors[0].Locations.FirstOrDefault(), selectors[0].Name));
            return;
        }

        context.AddSource($"{type.Name}.PriorityQueue.g.cs", SourceText.From(GenerateSource(
            type,
            itemType,
            priorityType,
            selectors[0].Name,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "QueueName") ?? "priority-queue",
            GetNamedBool(attribute, "DequeueHighestPriorityFirst") ?? true), Encoding.UTF8));
    }

    private static bool IsSelector(IMethodSymbol method, INamedTypeSymbol itemType, INamedTypeSymbol priorityType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, priorityType) &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, itemType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol itemType,
        INamedTypeSymbol priorityType,
        string selectorName,
        string factoryMethodName,
        string queueName,
        bool dequeueHighestPriorityFirst)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var itemTypeName = itemType.ToDisplayString(TypeFormat);
        var priorityTypeName = priorityType.ToDisplayString(TypeFormat);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append(GetAccessibility(type.DeclaredAccessibility)).Append(' ');
        if (type.IsStatic)
            sb.Append("static ");
        else if (type.IsAbstract && type.TypeKind == TypeKind.Class)
            sb.Append("abstract ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class)
            sb.Append("sealed ");
        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ').Append(type.Name).AppendLine();
        sb.AppendLine("{");
        sb.Append("    public static global::PatternKit.Cloud.PriorityQueue.PriorityQueuePolicy<").Append(itemTypeName).Append(", ").Append(priorityTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        return global::PatternKit.Cloud.PriorityQueue.PriorityQueuePolicy<").Append(itemTypeName).Append(", ").Append(priorityTypeName).Append(">.Create(\"").Append(Escape(queueName)).AppendLine("\")");
        sb.Append("            .WithPrioritySelector(").Append(selectorName).AppendLine(")");
        sb.AppendLine(dequeueHighestPriorityFirst
            ? "            .DequeueHighestPriorityFirst()"
            : "            .DequeueLowestPriorityFirst()");
        sb.AppendLine("            .Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static bool? GetNamedBool(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as bool?;

    private static string GetAccessibility(Accessibility accessibility)
        => accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal"
        };
}
