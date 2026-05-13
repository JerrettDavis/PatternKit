using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators.Bridge;

[Generator]
public sealed class BridgeGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                              SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKBRG001",
        "Bridge abstraction must be partial",
        "Type '{0}' is marked with [BridgeAbstraction] but is not declared as partial",
        "PatternKit.Generators.Bridge",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidImplementor = new(
        "PKBRG002",
        "Bridge implementor must be an interface or abstract class",
        "Implementor type '{0}' must be an interface or abstract class",
        "PatternKit.Generators.Bridge",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedMember = new(
        "PKBRG003",
        "Implementor member is unsupported",
        "Implementor member '{0}' is not supported by the Bridge generator",
        "PatternKit.Generators.Bridge",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DefaultNameConflict = new(
        "PKBRG004",
        "Generated default abstraction name conflicts",
        "Generated default abstraction type name '{0}' conflicts with an existing type in namespace '{1}'",
        "PatternKit.Generators.Bridge",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var abstractions = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Bridge.BridgeAbstractionAttribute",
            static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
            static (ctx, _) => ctx);

        context.RegisterSourceOutput(abstractions, static (spc, ctx) =>
        {
            if (ctx.TargetSymbol is INamedTypeSymbol type)
            {
                var attr = ctx.Attributes.FirstOrDefault(a =>
                    a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Bridge.BridgeAbstractionAttribute");
                if (attr is not null)
                {
                    Generate(spc, type, attr, ctx.TargetNode);
                }
            }
        });
    }

    private static void Generate(SourceProductionContext context, INamedTypeSymbol abstraction, AttributeData attribute, SyntaxNode node)
    {
        if (!IsPartial(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.GetLocation(), abstraction.Name));
            return;
        }

        var implementor = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;

        if (implementor is null ||
            (implementor.TypeKind != TypeKind.Interface && !(implementor.TypeKind == TypeKind.Class && implementor.IsAbstract)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidImplementor,
                node.GetLocation(),
                implementor?.ToDisplayString() ?? "<unknown>"));
            return;
        }

        var propertyName = GetString(attribute, nameof(BridgeAbstractionAttribute.ImplementorPropertyName), "Implementor");
        var generateDefault = GetBool(attribute, nameof(BridgeAbstractionAttribute.GenerateDefault));
        var defaultTypeName = GetString(attribute, nameof(BridgeAbstractionAttribute.DefaultTypeName), abstraction.Name + "Default");

        if (generateDefault && HasTypeInNamespace(abstraction.ContainingNamespace, defaultTypeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DefaultNameConflict,
                node.GetLocation(),
                defaultTypeName,
                abstraction.ContainingNamespace.IsGlobalNamespace ? "" : abstraction.ContainingNamespace.ToDisplayString()));
            return;
        }

        var members = GetForwardableMembers(implementor, propertyName).ToArray();
        foreach (var member in members.Where(m => m.UnsupportedReason is not null))
        {
            context.ReportDiagnostic(Diagnostic.Create(UnsupportedMember, node.GetLocation(), member.Name));
            return;
        }

        var source = RenderAbstraction(abstraction, implementor, propertyName, members);
        context.AddSource($"{abstraction.Name}.Bridge.g.cs", source);

        if (generateDefault)
        {
            context.AddSource($"{defaultTypeName}.Bridge.Default.g.cs", RenderDefault(abstraction, implementor, defaultTypeName));
        }
    }

    private static string RenderAbstraction(INamedTypeSymbol abstraction, INamedTypeSymbol implementor, string propertyName, ForwardMember[] members)
    {
        var ns = abstraction.ContainingNamespace.IsGlobalNamespace ? null : abstraction.ContainingNamespace.ToDisplayString();
        var typeKind = abstraction.IsRecord ? "record class" : "class";
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("public abstract partial ").Append(typeKind).Append(' ').Append(abstraction.Name);
        if (abstraction.TypeParameters.Length > 0)
        {
            sb.Append('<').Append(string.Join(", ", abstraction.TypeParameters.Select(p => p.Name))).Append('>');
        }
        sb.AppendLine();
        sb.AppendLine("{");
        var implType = implementor.ToDisplayString(TypeFormat);
        sb.Append("    protected ").Append(abstraction.Name).Append('(').Append(implType).Append(" implementor)");
        sb.AppendLine();
        sb.AppendLine("    {");
        sb.Append("        ").Append(propertyName).Append(" = implementor ?? throw new global::System.ArgumentNullException(nameof(implementor));").AppendLine();
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    protected ").Append(implType).Append(' ').Append(propertyName).Append(" { get; }").AppendLine();

        foreach (var member in members)
        {
            sb.AppendLine();
            sb.Append(member.Source);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string RenderDefault(INamedTypeSymbol abstraction, INamedTypeSymbol implementor, string defaultTypeName)
    {
        var ns = abstraction.ContainingNamespace.IsGlobalNamespace ? null : abstraction.ContainingNamespace.ToDisplayString();
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("public sealed partial class ").Append(defaultTypeName).Append(" : ").Append(abstraction.ToDisplayString(TypeFormat)).AppendLine();
        sb.AppendLine("{");
        sb.Append("    public ").Append(defaultTypeName).Append('(').Append(implementor.ToDisplayString(TypeFormat)).Append(" implementor) : base(implementor)");
        sb.AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static IEnumerable<ForwardMember> GetForwardableMembers(INamedTypeSymbol implementor, string propertyName)
    {
        foreach (var member in implementor.GetMembers().OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            if (HasIgnore(member) || member.IsStatic)
            {
                continue;
            }

            if (member is IEventSymbol)
            {
                yield return new ForwardMember(member.Name, "", "events");
                continue;
            }

            if (member is IMethodSymbol method &&
                method.MethodKind == MethodKind.Ordinary &&
                (implementor.TypeKind == TypeKind.Interface || method.IsAbstract || method.IsVirtual))
            {
                yield return new ForwardMember(member.Name, RenderMethod(method, propertyName), null);
            }

            if (member is IPropertySymbol property &&
                !property.IsIndexer &&
                (implementor.TypeKind == TypeKind.Interface || property.IsAbstract || property.IsVirtual))
            {
                yield return new ForwardMember(member.Name, RenderProperty(property, propertyName), null);
            }
        }
    }

    private static string RenderMethod(IMethodSymbol method, string propertyName)
    {
        var sb = new StringBuilder();
        sb.Append("    protected ").Append(method.ReturnType.ToDisplayString(TypeFormat)).Append(' ').Append(method.Name);
        if (method.TypeParameters.Length > 0)
        {
            sb.Append('<').Append(string.Join(", ", method.TypeParameters.Select(p => p.Name))).Append('>');
        }

        sb.Append('(').Append(string.Join(", ", method.Parameters.Select(RenderParameter))).Append(')').AppendLine();
        sb.Append("        => ").Append(propertyName).Append('.').Append(method.Name);
        if (method.TypeParameters.Length > 0)
        {
            sb.Append('<').Append(string.Join(", ", method.TypeParameters.Select(p => p.Name))).Append('>');
        }
        sb.Append('(').Append(string.Join(", ", method.Parameters.Select(RenderArgument))).AppendLine(");");
        return sb.ToString();
    }

    private static string RenderProperty(IPropertySymbol property, string propertyName)
    {
        return $"    protected {property.Type.ToDisplayString(TypeFormat)} {property.Name} => {propertyName}.{property.Name};";
    }

    private static string RenderParameter(IParameterSymbol parameter)
    {
        var prefix = parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => ""
        };
        var defaultValue = parameter.HasExplicitDefaultValue
            ? " = " + RenderDefaultValue(parameter)
            : "";
        return $"{prefix}{parameter.Type.ToDisplayString(TypeFormat)} {parameter.Name}{defaultValue}";
    }

    private static string RenderArgument(IParameterSymbol parameter)
    {
        var prefix = parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => ""
        };
        return prefix + parameter.Name;
    }

    private static string RenderDefaultValue(IParameterSymbol parameter)
    {
        if (parameter.ExplicitDefaultValue is null)
        {
            return "default";
        }
        if (parameter.Type.SpecialType == SpecialType.System_String)
        {
            return "@\"" + parameter.ExplicitDefaultValue.ToString()!.Replace("\"", "\"\"") + "\"";
        }
        if (parameter.Type.SpecialType == SpecialType.System_Boolean)
        {
            return (bool)parameter.ExplicitDefaultValue ? "true" : "false";
        }
        return parameter.ExplicitDefaultValue.ToString() ?? "default";
    }

    private static bool IsPartial(SyntaxNode node) =>
        node is TypeDeclarationSyntax type && type.Modifiers.Any(SyntaxKind.PartialKeyword);

    private static bool HasIgnore(ISymbol symbol) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Bridge.BridgeIgnoreAttribute");

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

    private static bool HasTypeInNamespace(INamespaceSymbol ns, string name) =>
        ns.GetTypeMembers(name).Any();

    private readonly record struct ForwardMember(string Name, string Source, string? UnsupportedReason);
}
