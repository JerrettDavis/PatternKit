using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.ChangeDataCapture;

[Generator]
public sealed class ChangeDataCaptureGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.ChangeDataCapture.GenerateChangeDataCaptureAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKCDC001",
        "Change Data Capture host must be partial",
        "Type '{0}' is marked with [GenerateChangeDataCapture] but is not declared as partial",
        "PatternKit.Generators.ChangeDataCapture",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidIdentifier = new(
        "PKCDC002",
        "Change Data Capture member name is invalid",
        "Change Data Capture '{0}' has an invalid member name '{1}'",
        "PatternKit.Generators.ChangeDataCapture",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingTypes = new(
        "PKCDC003",
        "Change Data Capture types are required",
        "Change Data Capture '{0}' requires mutation and event types",
        "PatternKit.Generators.ChangeDataCapture",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ContainingTypeMustBePartial = new(
        "PKCDC004",
        "Change Data Capture containing type must be partial",
        "Type '{0}' contains a [GenerateChangeDataCapture] host but is not declared as partial",
        "PatternKit.Generators.ChangeDataCapture",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == AttributeName);
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

        foreach (var containingType in GetContainingTypes(type))
        {
            if (!IsPartial(containingType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ContainingTypeMustBePartial,
                    containingType.Locations.FirstOrDefault() ?? node.Identifier.GetLocation(),
                    containingType.Name));
                return;
            }
        }

        var mutationType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        var eventType = attribute.ConstructorArguments.Length >= 2
            ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol
            : null;
        if (mutationType is null || eventType is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingTypes, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        if (!IsIdentifier(factoryName))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidIdentifier, node.Identifier.GetLocation(), type.Name, factoryName));
            return;
        }

        var mapperName = GetNamedString(attribute, "MapperMethodName") ?? "Map";
        if (!IsIdentifier(mapperName))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidIdentifier, node.Identifier.GetLocation(), type.Name, mapperName));
            return;
        }

        var pipelineName = GetNamedString(attribute, "PipelineName") ?? "change-data-capture";
        context.AddSource(
            $"{type.Name}.ChangeDataCapture.g.cs",
            SourceText.From(GenerateSource(type, mutationType, eventType, factoryName, mapperName, pipelineName), Encoding.UTF8));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol mutationType,
        INamedTypeSymbol eventType,
        string factoryName,
        string mapperName,
        string pipelineName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var mutationTypeName = mutationType.ToDisplayString(TypeFormat);
        var eventTypeName = eventType.ToDisplayString(TypeFormat);
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
        var memberIndent = indent + "    ";
        var bodyIndent = memberIndent + "    ";
        sb.AppendLine(indent + "{");
        sb.Append(memberIndent).Append("public static global::PatternKit.Messaging.ChangeDataCapture.ChangeDataCapturePipeline<")
            .Append(mutationTypeName).Append(", ").Append(eventTypeName).Append("> ").Append(factoryName).AppendLine("(");
        sb.Append(bodyIndent).Append("global::System.Func<").Append(eventTypeName)
            .Append(", global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask> publisher,");
        sb.AppendLine();
        sb.Append(bodyIndent).Append("global::PatternKit.Messaging.ChangeDataCapture.IChangeDataCaptureStore<")
            .Append(mutationTypeName).Append(", ").Append(eventTypeName).Append(">? store = null)");
        sb.AppendLine();
        sb.AppendLine(memberIndent + "{");
        sb.Append(bodyIndent).Append("var builder = global::PatternKit.Messaging.ChangeDataCapture.ChangeDataCapturePipeline<")
            .Append(mutationTypeName).Append(", ").Append(eventTypeName).Append(">.Create(\"")
            .Append(Escape(pipelineName)).AppendLine("\")");
        sb.Append(bodyIndent).Append("    .MapWith(").Append(mapperName).AppendLine(")");
        sb.AppendLine(bodyIndent + "    .PublishWith(publisher);");
        sb.AppendLine(bodyIndent + "if (store is not null)");
        sb.AppendLine(bodyIndent + "    builder.UseStore(store);");
        sb.AppendLine(bodyIndent + "return builder.Build();");
        sb.AppendLine(memberIndent + "}");
        sb.AppendLine(indent + "}");
        for (var i = containingTypes.Length - 1; i >= 0; i--)
            sb.AppendLine(new string(' ', i * 4) + "}");

        return sb.ToString();
    }

    private static INamedTypeSymbol[] GetContainingTypes(INamedTypeSymbol type)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
            containingTypes.Push(current);

        return containingTypes.ToArray();
    }

    private static bool IsPartial(INamedTypeSymbol type)
        => type.DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(static declaration => declaration.Modifiers.Any(static modifier => modifier.Text == "partial"));

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

    private static bool IsIdentifier(string value)
        => SyntaxFacts.IsValidIdentifier(value) && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;

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

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;
}
