using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.TransactionScript;

[Generator]
public sealed class TransactionScriptGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.TransactionScript.GenerateTransactionScriptAttribute";
    private const string HandlerAttributeName = "PatternKit.Generators.TransactionScript.TransactionScriptHandlerAttribute";
    private const string ValidatorAttributeName = "PatternKit.Generators.TransactionScript.TransactionScriptValidatorAttribute";
    private const string ErrorTypeName = "PatternKit.Application.TransactionScript.TransactionScriptError";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKTS001", "Transaction Script host must be partial",
        "Type '{0}' is marked with [GenerateTransactionScript] but is not declared as partial",
        "PatternKit.Generators.TransactionScript", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingHandler = new(
        "PKTS002", "Transaction Script handler is missing",
        "Transaction Script '{0}' must declare exactly one [TransactionScriptHandler] method",
        "PatternKit.Generators.TransactionScript", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidHandler = new(
        "PKTS003", "Transaction Script handler signature is invalid",
        "Transaction Script handler '{0}' must be static and return ValueTask<TResponse> from TRequest and CancellationToken parameters",
        "PatternKit.Generators.TransactionScript", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidValidator = new(
        "PKTS004", "Transaction Script validator signature is invalid",
        "Transaction Script validator must be a single static method returning IEnumerable<TransactionScriptError> from one TRequest parameter",
        "PatternKit.Generators.TransactionScript", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == GenerateAttributeName);
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

        var requestType = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var responseType = attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (requestType is null || responseType is null)
            return;

        var handlers = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == HandlerAttributeName))
            .ToArray();
        if (handlers.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingHandler, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var handler = handlers[0];
        if (!IsHandler(handler, requestType, responseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, handler.Locations.FirstOrDefault(), handler.Name));
            return;
        }

        var validators = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == ValidatorAttributeName))
            .ToArray();
        if (validators.Length > 1 || (validators.Length == 1 && !IsValidator(validators[0], requestType)))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidValidator, validators.FirstOrDefault()?.Locations.FirstOrDefault() ?? node.Identifier.GetLocation()));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var scriptName = GetNamedString(attribute, "ScriptName");
        if (string.IsNullOrWhiteSpace(scriptName))
            scriptName = type.Name;

        context.AddSource($"{type.Name}.TransactionScript.g.cs", SourceText.From(
            GenerateSource(type, requestType, responseType, handler.Name, validators.FirstOrDefault()?.Name, factoryName, scriptName!),
            Encoding.UTF8));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol requestType,
        INamedTypeSymbol responseType,
        string handlerName,
        string? validatorName,
        string factoryName,
        string scriptName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var requestName = requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var responseName = responseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
        sb.Append("    public static global::PatternKit.Application.TransactionScript.TransactionScript<")
            .Append(requestName).Append(", ").Append(responseName).Append("> ").Append(factoryName).AppendLine("()");
        sb.Append("        => global::PatternKit.Application.TransactionScript.TransactionScript<")
            .Append(requestName).Append(", ").Append(responseName).Append(">.Create(\"").Append(Escape(scriptName)).Append("\")");
        if (validatorName is not null)
            sb.AppendLine().Append("            .Validate(").Append(validatorName).Append(')');
        sb.AppendLine().Append("            .Execute(").Append(handlerName).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
        => method.IsStatic
        && !method.IsGenericMethod
        && method.ReturnType is INamedTypeSymbol returnType
        && returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.ValueTask<" + responseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">"
        && method.Parameters.Length == 2
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType)
        && method.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken";

    private static bool IsValidator(IMethodSymbol method, INamedTypeSymbol requestType)
        => method.IsStatic
        && !method.IsGenericMethod
        && method.Parameters.Length == 1
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType)
        && method.ReturnType is INamedTypeSymbol returnType
        && (returnType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Collections.Generic.IEnumerable<T>"
            || returnType.AllInterfaces.Any(static i => i.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Collections.Generic.IEnumerable<T>"))
        && returnType.TypeArguments.Length == 1
        && returnType.TypeArguments[0].ToDisplayString() == ErrorTypeName;

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
