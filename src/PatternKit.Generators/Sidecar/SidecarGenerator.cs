using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.Sidecar;

[Generator]
public sealed class SidecarGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.Sidecar.GenerateSidecarAttribute";
    private const string BeforeAttributeName = "PatternKit.Generators.Sidecar.SidecarBeforeAttribute";
    private const string AfterAttributeName = "PatternKit.Generators.Sidecar.SidecarAfterAttribute";
    private const string HandlerAttributeName = "PatternKit.Generators.Sidecar.SidecarHandlerAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKSC001", "Sidecar host must be partial",
        "Type '{0}' is marked with [GenerateSidecar] but is not declared as partial",
        "PatternKit.Generators.Sidecar", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingMembers = new(
        "PKSC002", "Sidecar members are missing",
        "Sidecar type '{0}' must declare at least one companion step and exactly one handler",
        "PatternKit.Generators.Sidecar", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "PKSC003", "Sidecar method signature is invalid",
        "Sidecar method '{0}' has an invalid static signature for the configured request or response type",
        "PatternKit.Generators.Sidecar", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateStep = new(
        "PKSC004", "Sidecar step is duplicated",
        "Sidecar step name '{0}' is duplicated",
        "PatternKit.Generators.Sidecar", DiagnosticSeverity.Error, true);

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

        var requestType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var responseType = attribute.ConstructorArguments.Length >= 2 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (requestType is null || responseType is null)
            return;

        var before = NamedMembers(type, BeforeAttributeName);
        var after = NamedMembers(type, AfterAttributeName);
        var handlers = MembersWith(type, HandlerAttributeName);
        if (before.Length + after.Length == 0 || handlers.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMembers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var duplicate = before.Concat(after).GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateStep, node.Identifier.GetLocation(), duplicate.Key));
            return;
        }

        var invalidBefore = before.FirstOrDefault(item => !IsBefore(item.Method, requestType));
        if (invalidBefore is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidBefore.Method.Locations.FirstOrDefault(), invalidBefore.Method.Name));
            return;
        }

        var invalidAfter = after.FirstOrDefault(item => !IsAfter(item.Method, requestType, responseType));
        if (invalidAfter is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidAfter.Method.Locations.FirstOrDefault(), invalidAfter.Method.Name));
            return;
        }

        if (!IsHandler(handlers[0], requestType, responseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, handlers[0].Locations.FirstOrDefault(), handlers[0].Name));
            return;
        }

        context.AddSource($"{type.Name}.Sidecar.g.cs", SourceText.From(GenerateSource(
            type,
            requestType,
            responseType,
            before,
            after,
            handlers[0].Name,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "SidecarName") ?? "sidecar"), Encoding.UTF8));
    }

    private static NamedMember[] NamedMembers(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == attributeName)
            })
            .Where(static item => item.Attribute is not null)
            .Select(static item => new NamedMember((string)item.Attribute!.ConstructorArguments[0].Value!, item.Method))
            .ToArray();

    private static IMethodSymbol[] MembersWith(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();

    private static bool IsBefore(IMethodSymbol method, INamedTypeSymbol requestType)
        => method.IsStatic &&
           method.ReturnsVoid &&
           method.Parameters.Length == 1 &&
           IsSidecarContext(method.Parameters[0].Type, requestType);

    private static bool IsAfter(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
        => method.IsStatic &&
           method.ReturnsVoid &&
           method.Parameters.Length == 2 &&
           IsSidecarContext(method.Parameters[0].Type, requestType) &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, responseType);

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, responseType) &&
           method.Parameters.Length == 1 &&
           IsSidecarContext(method.Parameters[0].Type, requestType);

    private static bool IsSidecarContext(ITypeSymbol type, INamedTypeSymbol requestType)
        => type is INamedTypeSymbol contextType &&
           contextType.ConstructedFrom.ToDisplayString() == "PatternKit.Cloud.Sidecar.SidecarContext<TRequest>" &&
           contextType.TypeArguments.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(contextType.TypeArguments[0], requestType);

    private static string GenerateSource(INamedTypeSymbol type, INamedTypeSymbol requestType, INamedTypeSymbol responseType, IReadOnlyList<NamedMember> before, IReadOnlyList<NamedMember> after, string handlerName, string factoryMethodName, string sidecarName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var requestTypeName = requestType.ToDisplayString(TypeFormat);
        var responseTypeName = responseType.ToDisplayString(TypeFormat);
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
        sb.Append("    public static global::PatternKit.Cloud.Sidecar.Sidecar<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        return global::PatternKit.Cloud.Sidecar.Sidecar<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append(">.Create(\"").Append(Escape(sidecarName)).AppendLine("\")");
        foreach (var step in before)
            sb.Append("            .Before(\"").Append(Escape(step.Name)).Append("\", ").Append(step.Method.Name).AppendLine(")");
        foreach (var step in after)
            sb.Append("            .After(\"").Append(Escape(step.Name)).Append("\", ").Append(step.Method.Name).AppendLine(")");
        sb.Append("            .Handle(").Append(handlerName).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

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

    private sealed record NamedMember(string Name, IMethodSymbol Method);
}
