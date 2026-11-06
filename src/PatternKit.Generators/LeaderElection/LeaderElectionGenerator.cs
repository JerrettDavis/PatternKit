using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.LeaderElection;

[Generator]
public sealed class LeaderElectionGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.LeaderElection.GenerateLeaderElectionAttribute";
    private const string CandidateIdAttributeName = "PatternKit.Generators.LeaderElection.LeaderCandidateIdAttribute";
    private const string AcquiredAttributeName = "PatternKit.Generators.LeaderElection.LeaderAcquiredAttribute";
    private const string RenewedAttributeName = "PatternKit.Generators.LeaderElection.LeaderRenewedAttribute";
    private const string ReleasedAttributeName = "PatternKit.Generators.LeaderElection.LeaderReleasedAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKLE001", "Leader Election host must be partial",
        "Type '{0}' is marked with [GenerateLeaderElection] but is not declared as partial",
        "PatternKit.Generators.LeaderElection", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingMembers = new(
        "PKLE002", "Leader Election members are missing",
        "Leader Election type '{0}' must declare exactly one candidate id selector",
        "PatternKit.Generators.LeaderElection", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "PKLE003", "Leader Election method signature is invalid",
        "Leader Election method '{0}' has an invalid static signature for the configured context type",
        "PatternKit.Generators.LeaderElection", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidLease = new(
        "PKLE004", "Leader Election lease duration is invalid",
        "Leader Election '{0}' must have LeaseDurationMilliseconds > 0",
        "PatternKit.Generators.LeaderElection", DiagnosticSeverity.Error, true);

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

        var contextType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        if (contextType is null)
            return;

        var leaseMs = GetNamedInt(attribute, "LeaseDurationMilliseconds") ?? 30000;
        if (leaseMs <= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidLease, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var ids = MembersWith(type, CandidateIdAttributeName);
        var acquired = MembersWith(type, AcquiredAttributeName);
        var renewed = MembersWith(type, RenewedAttributeName);
        var released = MembersWith(type, ReleasedAttributeName);
        if (ids.Length != 1 || acquired.Length > 1 || renewed.Length > 1 || released.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMembers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (!IsCandidateId(ids[0], contextType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, ids[0].Locations.FirstOrDefault(), ids[0].Name));
            return;
        }

        var invalidLeaseCallback = acquired.Concat(renewed).FirstOrDefault(method => !IsLeaseCallback(method, contextType));
        if (invalidLeaseCallback is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidLeaseCallback.Locations.FirstOrDefault(), invalidLeaseCallback.Name));
            return;
        }

        var invalidRelease = released.FirstOrDefault(method => !IsReleaseCallback(method, contextType));
        if (invalidRelease is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidRelease.Locations.FirstOrDefault(), invalidRelease.Name));
            return;
        }

        context.AddSource($"{type.Name}.LeaderElection.g.cs", SourceText.From(GenerateSource(
            type,
            contextType,
            ids[0].Name,
            acquired.FirstOrDefault()?.Name,
            renewed.FirstOrDefault()?.Name,
            released.FirstOrDefault()?.Name,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "ElectionName") ?? "leader-election",
            leaseMs), Encoding.UTF8));
    }

    private static IMethodSymbol[] MembersWith(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();

    private static bool IsCandidateId(IMethodSymbol method, INamedTypeSymbol contextType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_String &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextType);

    private static bool IsLeaseCallback(IMethodSymbol method, INamedTypeSymbol contextType)
        => method.IsStatic &&
           method.ReturnsVoid &&
           method.Parameters.Length == 2 &&
           method.Parameters[0].Type.ToDisplayString() == "PatternKit.Cloud.LeaderElection.LeaderLease" &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, contextType);

    private static bool IsReleaseCallback(IMethodSymbol method, INamedTypeSymbol contextType)
        => method.IsStatic &&
           method.ReturnsVoid &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol contextType,
        string candidateIdName,
        string? acquiredName,
        string? renewedName,
        string? releasedName,
        string factoryMethodName,
        string electionName,
        int leaseDurationMilliseconds)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var contextTypeName = contextType.ToDisplayString(TypeFormat);
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
        sb.Append(memberIndent).Append("public static global::PatternKit.Cloud.LeaderElection.LeaderElection<").Append(contextTypeName).Append("> ").Append(factoryMethodName).AppendLine("Election()");
        sb.Append(memberIndent).AppendLine("{");
        sb.Append(bodyIndent).Append("return global::PatternKit.Cloud.LeaderElection.LeaderElection<").Append(contextTypeName).Append(">.Create(\"").Append(Escape(electionName)).AppendLine("\")");
        sb.Append(bodyIndent).Append("    .LeaseDuration(global::System.TimeSpan.FromMilliseconds(").Append(leaseDurationMilliseconds).AppendLine("))");
        sb.Append(bodyIndent).AppendLine("    .Build();");
        sb.AppendLine(memberIndent + "}");
        sb.AppendLine();
        sb.Append(memberIndent).Append("public static global::PatternKit.Cloud.LeaderElection.LeaderElectionCandidate<").Append(contextTypeName).Append("> ").Append(factoryMethodName).Append('(').Append(contextTypeName).AppendLine(" context)");
        sb.Append(memberIndent).AppendLine("{");
        sb.Append(bodyIndent).Append("return global::PatternKit.Cloud.LeaderElection.LeaderElectionCandidate.Create(").Append(candidateIdName).AppendLine("(context), context)");
        if (acquiredName is not null)
            sb.Append(bodyIndent).Append("    .OnAcquired(").Append(acquiredName).AppendLine(")");
        if (renewedName is not null)
            sb.Append(bodyIndent).Append("    .OnRenewed(").Append(renewedName).AppendLine(")");
        if (releasedName is not null)
            sb.Append(bodyIndent).Append("    .OnReleased(").Append(releasedName).AppendLine(")");
        sb.Append(bodyIndent).AppendLine("    .Build();");
        sb.AppendLine(memberIndent + "}");
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

    private static int? GetNamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as int?;

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
}
