using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Ambassador;

[Generator]
public sealed class AmbassadorGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.Ambassador.GenerateAmbassadorAttribute";
    private const string TransformAttributeName = "PatternKit.Generators.Ambassador.AmbassadorTransformAttribute";
    private const string PolicyAttributeName = "PatternKit.Generators.Ambassador.AmbassadorConnectionPolicyAttribute";
    private const string TelemetryAttributeName = "PatternKit.Generators.Ambassador.AmbassadorTelemetryAttribute";
    private const string CallAttributeName = "PatternKit.Generators.Ambassador.AmbassadorCallAttribute";
    private const string FallbackAttributeName = "PatternKit.Generators.Ambassador.AmbassadorFallbackAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKAMB001", "Ambassador host must be partial",
        "Type '{0}' is marked with [GenerateAmbassador] but is not declared as partial",
        "PatternKit.Generators.Ambassador", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingMembers = new(
        "PKAMB002", "Ambassador members are missing",
        "Ambassador type '{0}' must declare exactly one outbound call handler",
        "PatternKit.Generators.Ambassador", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "PKAMB003", "Ambassador method signature is invalid",
        "Ambassador method '{0}' has an invalid static signature for the configured request or response type",
        "PatternKit.Generators.Ambassador", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateTelemetry = new(
        "PKAMB004", "Ambassador telemetry is duplicated",
        "Ambassador telemetry step '{0}' is duplicated",
        "PatternKit.Generators.Ambassador", DiagnosticSeverity.Error, true);

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

        var transforms = MembersWith(type, TransformAttributeName);
        var policies = MembersWith(type, PolicyAttributeName);
        var telemetry = TelemetryMembers(type);
        var calls = MembersWith(type, CallAttributeName);
        var fallbacks = MembersWith(type, FallbackAttributeName);

        if (calls.Length != 1 || policies.Length > 1 || fallbacks.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMembers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var duplicate = telemetry.GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateTelemetry, node.Identifier.GetLocation(), duplicate.Key));
            return;
        }

        var invalidTransform = transforms.FirstOrDefault(method => !IsTransform(method, requestType));
        if (invalidTransform is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidTransform.Locations.FirstOrDefault(), invalidTransform.Name));
            return;
        }

        var invalidPolicy = policies.FirstOrDefault(method => !IsPolicy(method, requestType));
        if (invalidPolicy is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidPolicy.Locations.FirstOrDefault(), invalidPolicy.Name));
            return;
        }

        var invalidTelemetry = telemetry.FirstOrDefault(item => !IsTelemetry(item.Method, requestType));
        if (invalidTelemetry is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidTelemetry.Method.Locations.FirstOrDefault(), invalidTelemetry.Method.Name));
            return;
        }

        if (!IsHandler(calls[0], requestType, responseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, calls[0].Locations.FirstOrDefault(), calls[0].Name));
            return;
        }

        if (fallbacks.Length == 1 && !IsHandler(fallbacks[0], requestType, responseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, fallbacks[0].Locations.FirstOrDefault(), fallbacks[0].Name));
            return;
        }

        context.AddSource($"{type.Name}.Ambassador.g.cs", SourceText.From(GenerateSource(
            type,
            requestType,
            responseType,
            transforms,
            policies.FirstOrDefault()?.Name,
            telemetry,
            calls[0].Name,
            fallbacks.FirstOrDefault()?.Name,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "AmbassadorName") ?? "ambassador"), Encoding.UTF8));
    }

    private static IMethodSymbol[] MembersWith(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();

    private static TelemetryMember[] TelemetryMembers(INamedTypeSymbol type)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == TelemetryAttributeName)
            })
            .Where(static item => item.Attribute is not null)
            .Select(static item => new TelemetryMember((string)item.Attribute!.ConstructorArguments[0].Value!, item.Method))
            .ToArray();

    private static bool IsTransform(IMethodSymbol method, INamedTypeSymbol requestType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, requestType) &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType);

    private static bool IsPolicy(IMethodSymbol method, INamedTypeSymbol requestType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType);

    private static bool IsTelemetry(IMethodSymbol method, INamedTypeSymbol requestType)
        => method.IsStatic &&
           method.ReturnsVoid &&
           method.Parameters.Length == 1 &&
           IsContext(method.Parameters[0].Type, requestType);

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, responseType) &&
           method.Parameters.Length == 1 &&
           IsContext(method.Parameters[0].Type, requestType);

    private static bool IsContext(ITypeSymbol type, INamedTypeSymbol requestType)
        => type is INamedTypeSymbol contextType &&
           contextType.ConstructedFrom.ToDisplayString() == "PatternKit.Cloud.Ambassador.AmbassadorContext<TRequest>" &&
           contextType.TypeArguments.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(contextType.TypeArguments[0], requestType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol requestType,
        INamedTypeSymbol responseType,
        IReadOnlyList<IMethodSymbol> transforms,
        string? policyName,
        IReadOnlyList<TelemetryMember> telemetry,
        string callName,
        string? fallbackName,
        string factoryMethodName,
        string ambassadorName)
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

        var containingTypes = GetContainingTypes(type);
        var indentLevel = 0;
        foreach (var containingType in containingTypes)
        {
            AppendTypeDeclaration(sb, containingType, indentLevel);
            sb.AppendLine();
            sb.AppendLine(new string(' ', indentLevel * 4) + "{");
            indentLevel++;
        }

        AppendTypeDeclaration(sb, type, indentLevel);
        sb.AppendLine();
        var indent = new string(' ', indentLevel * 4);
        sb.AppendLine(indent + "{");
        var memberIndent = indent + "    ";
        var bodyIndent = memberIndent + "    ";
        var chainIndent = bodyIndent + "    ";
        sb.Append(memberIndent).Append("public static global::PatternKit.Cloud.Ambassador.Ambassador<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.Append(memberIndent).AppendLine("{");
        sb.Append(bodyIndent).Append("return global::PatternKit.Cloud.Ambassador.Ambassador<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append(">.Create(\"").Append(Escape(ambassadorName)).AppendLine("\")");
        foreach (var transform in transforms)
            sb.Append(chainIndent).Append(".Transform(").Append(transform.Name).AppendLine(")");
        if (policyName is not null)
            sb.Append(chainIndent).Append(".ConnectionPolicy(").Append(policyName).AppendLine(")");
        foreach (var item in telemetry)
            sb.Append(chainIndent).Append(".Telemetry(\"").Append(Escape(item.Name)).Append("\", ").Append(item.Method.Name).AppendLine(")");
        sb.Append(chainIndent).Append(".Call(").Append(callName).AppendLine(")");
        if (fallbackName is not null)
            sb.Append(chainIndent).Append(".Fallback(").Append(fallbackName).AppendLine(")");
        sb.Append(chainIndent).AppendLine(".Build();");
        sb.Append(memberIndent).AppendLine("}");
        sb.AppendLine(indent + "}");
        for (var i = containingTypes.Length - 1; i >= 0; i--)
        {
            sb.AppendLine(new string(' ', i * 4) + "}");
        }

        return sb.ToString();
    }

    private static INamedTypeSymbol[] GetContainingTypes(INamedTypeSymbol type)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        return containingTypes.ToArray();
    }

    private static void AppendTypeDeclaration(StringBuilder sb, INamedTypeSymbol type, int indentLevel)
    {
        sb.Append(new string(' ', indentLevel * 4));
        sb.Append(GetAccessibility(type.DeclaredAccessibility)).Append(' ');
        if (type.IsStatic)
            sb.Append("static ");
        else if (type.IsAbstract && type.TypeKind == TypeKind.Class)
            sb.Append("abstract ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class)
            sb.Append("sealed ");
        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ').Append(type.Name);
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

    private sealed record TelemetryMember(string Name, IMethodSymbol Method);
}
