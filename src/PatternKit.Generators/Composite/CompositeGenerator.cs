using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PatternKit.Generators.Composite;

[Generator]
public sealed class CompositeGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                              SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKCMP001",
        "Composite component must be partial",
        "Type '{0}' is marked with [CompositeComponent] but is not declared as partial",
        "PatternKit.Generators.Composite",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidTarget = new(
        "PKCMP002",
        "Composite component target is invalid",
        "Composite component type '{0}' must be an interface or abstract class",
        "PatternKit.Generators.Composite",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor NameConflict = new(
        "PKCMP003",
        "Generated Composite type name conflicts",
        "Generated Composite type name '{0}' conflicts with an existing type in namespace '{1}'",
        "PatternKit.Generators.Composite",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedMember = new(
        "PKCMP004",
        "Composite contract member is unsupported",
        "Composite contract member '{0}' is not supported in v1",
        "PatternKit.Generators.Composite",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var components = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Composite.CompositeComponentAttribute",
            static (node, _) => node is InterfaceDeclarationSyntax or ClassDeclarationSyntax,
            static (ctx, _) => ctx);

        context.RegisterSourceOutput(components, static (spc, ctx) =>
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol component)
            {
                return;
            }

            var attr = ctx.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Composite.CompositeComponentAttribute");
            if (attr is not null)
            {
                Generate(spc, component, attr, ctx.TargetNode);
            }
        });
    }

    private static void Generate(SourceProductionContext context, INamedTypeSymbol component, AttributeData attribute, SyntaxNode node)
    {
        if (!IsPartial(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.GetLocation(), component.Name));
            return;
        }

        if (component.TypeKind != TypeKind.Interface && !(component.TypeKind == TypeKind.Class && component.IsAbstract))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidTarget, node.GetLocation(), component.Name));
            return;
        }

        foreach (var member in component.GetMembers())
        {
            if (member is IEventSymbol)
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedMember, node.GetLocation(), member.Name));
                return;
            }
        }

        var contractName = component.Name.StartsWith("I", StringComparison.Ordinal) && component.Name.Length > 1
            ? component.Name.Substring(1)
            : component.Name;
        var componentBaseName = GetString(attribute, nameof(CompositeComponentAttribute.ComponentBaseName), contractName + "ComponentBase");
        var compositeBaseName = GetString(attribute, nameof(CompositeComponentAttribute.CompositeBaseName), contractName + "CompositeBase");
        var childrenName = GetString(attribute, nameof(CompositeComponentAttribute.ChildrenPropertyName), "Children");
        var generateTraversal = GetBool(attribute, nameof(CompositeComponentAttribute.GenerateTraversalHelpers));

        var ns = component.ContainingNamespace;
        if (HasType(ns, componentBaseName) || HasType(ns, compositeBaseName))
        {
            context.ReportDiagnostic(Diagnostic.Create(NameConflict, node.GetLocation(), HasType(ns, componentBaseName) ? componentBaseName : compositeBaseName, ns.ToDisplayString()));
            return;
        }

        context.AddSource($"{component.Name}.Composite.g.cs", RenderComposite(component, componentBaseName, compositeBaseName, childrenName));
        if (generateTraversal)
        {
            context.AddSource($"{component.Name}.Composite.Traversal.g.cs", RenderTraversal(component, contractName, componentBaseName, childrenName));
        }
    }

    private static string RenderComposite(INamedTypeSymbol component, string componentBaseName, string compositeBaseName, string childrenName)
    {
        var ns = component.ContainingNamespace.IsGlobalNamespace ? null : component.ContainingNamespace.ToDisplayString();
        var contract = component.ToDisplayString(TypeFormat);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("public abstract partial class ").Append(componentBaseName).Append(" : ").Append(contract).AppendLine();
        sb.AppendLine("{");
        foreach (var property in GetContractProperties(component))
        {
            sb.Append(component.TypeKind == TypeKind.Class ? "    public abstract override " : "    public abstract ")
                .Append(property.Type.ToDisplayString(TypeFormat)).Append(' ').Append(property.Name).Append(" { get; }").AppendLine();
            sb.AppendLine();
        }
        sb.AppendLine("    public virtual bool IsLeaf => true;");
        sb.AppendLine();
        sb.Append("    public virtual global::System.Collections.Generic.IReadOnlyList<").Append(contract).Append("> ").Append(childrenName)
            .Append(" => global::System.Array.Empty<").Append(contract).Append(">();").AppendLine();
        sb.AppendLine();
        sb.Append("    public virtual void Add(").Append(contract).Append(" child) => throw new global::System.NotSupportedException();").AppendLine();
        sb.AppendLine();
        sb.Append("    public virtual bool Remove(").Append(contract).Append(" child) => throw new global::System.NotSupportedException();").AppendLine();
        sb.AppendLine();
        sb.AppendLine("    public virtual void Clear() => throw new global::System.NotSupportedException();");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.Append("public abstract partial class ").Append(compositeBaseName).Append(" : ").Append(componentBaseName).AppendLine();
        sb.AppendLine("{");
        sb.Append("    private readonly global::System.Collections.Generic.List<").Append(contract).Append("> _children = new();").AppendLine();
        sb.AppendLine();
        sb.AppendLine("    public override bool IsLeaf => false;");
        sb.AppendLine();
        sb.Append("    public override global::System.Collections.Generic.IReadOnlyList<").Append(contract).Append("> ").Append(childrenName).Append(" => _children;").AppendLine();
        sb.AppendLine();
        sb.Append("    public override void Add(").Append(contract).Append(" child)").AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        if (child is null) throw new global::System.ArgumentNullException(nameof(child));");
        sb.AppendLine("        _children.Add(child);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public override bool Remove(").Append(contract).Append(" child)").AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        if (child is null) throw new global::System.ArgumentNullException(nameof(child));");
        sb.AppendLine("        return _children.Remove(child);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void Clear() => _children.Clear();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string RenderTraversal(INamedTypeSymbol component, string contractName, string componentBaseName, string childrenName)
    {
        var ns = component.ContainingNamespace.IsGlobalNamespace ? null : component.ContainingNamespace.ToDisplayString();
        var contract = componentBaseName;
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }
        sb.Append("public static partial class ").Append(contractName).AppendLine("Traversal");
        sb.AppendLine("{");
        sb.Append("    public static global::System.Collections.Generic.IEnumerable<").Append(contract).Append("> DepthFirst(").Append(contract).Append(" root)").AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        if (root is null) throw new global::System.ArgumentNullException(nameof(root));");
        sb.AppendLine("        var stack = new global::System.Collections.Generic.Stack<" + contract + ">();");
        sb.AppendLine("        stack.Push(root);");
        sb.AppendLine("        while (stack.Count > 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            var current = stack.Pop();");
        sb.AppendLine("            yield return current;");
        sb.AppendLine("            var children = current." + childrenName + ";");
        sb.AppendLine("            for (var i = children.Count - 1; i >= 0; i--)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (children[i] is " + contract + " child) stack.Push(child);");
        sb.AppendLine("                else throw new global::System.InvalidOperationException(\"Traversal requires generated composite component base instances.\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public static global::System.Collections.Generic.IEnumerable<").Append(contract).Append("> BreadthFirst(").Append(contract).Append(" root)").AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        if (root is null) throw new global::System.ArgumentNullException(nameof(root));");
        sb.AppendLine("        var queue = new global::System.Collections.Generic.Queue<" + contract + ">();");
        sb.AppendLine("        queue.Enqueue(root);");
        sb.AppendLine("        while (queue.Count > 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            var current = queue.Dequeue();");
        sb.AppendLine("            yield return current;");
        sb.AppendLine("            foreach (var item in current." + childrenName + ")");
        sb.AppendLine("            {");
        sb.AppendLine("                if (item is " + contract + " child) queue.Enqueue(child);");
        sb.AppendLine("                else throw new global::System.InvalidOperationException(\"Traversal requires generated composite component base instances.\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static IEnumerable<IPropertySymbol> GetContractProperties(INamedTypeSymbol component) =>
        component.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && !p.IsIndexer && p.GetMethod is not null && !HasIgnore(p))
            .Where(p => component.TypeKind != TypeKind.Class || p.IsAbstract || p.IsVirtual || (p.IsOverride && !p.IsSealed))
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    private static bool IsPartial(SyntaxNode node) =>
        node is TypeDeclarationSyntax type && type.Modifiers.Any(SyntaxKind.PartialKeyword);

    private static bool HasIgnore(ISymbol symbol) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Composite.CompositeIgnoreAttribute");

    private static bool HasType(INamespaceSymbol ns, string name) => ns.GetTypeMembers(name).Any();

    private static string GetString(AttributeData attribute, string name, string fallback)
    {
        foreach (var item in attribute.NamedArguments)
        {
            if (item.Key == name && item.Value.Value is string value && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return fallback;
    }

    private static bool GetBool(AttributeData attribute, string name)
    {
        foreach (var item in attribute.NamedArguments)
        {
            if (item.Key == name && item.Value.Value is bool value)
            {
                return value;
            }
        }
        return false;
    }
}
