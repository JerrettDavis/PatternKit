using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace PatternKit.Generators;

/// <summary>
/// Shared utility methods used by multiple pattern generators.
/// </summary>
internal static class GeneratorUtilities
{
    /// <summary>
    /// Symbol display format that preserves nullable annotations, uses fully-qualified names with generics.
    /// </summary>
    internal static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
                            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>
    /// Checks whether a syntax node represents a partial type declaration (class, struct, record, or interface).
    /// </summary>
    internal static bool IsPartialType(SyntaxNode node)
    {
        return node switch
        {
            InterfaceDeclarationSyntax iface => iface.Modifiers.Any(SyntaxKind.PartialKeyword),
            ClassDeclarationSyntax cls => cls.Modifiers.Any(SyntaxKind.PartialKeyword),
            StructDeclarationSyntax str => str.Modifiers.Any(SyntaxKind.PartialKeyword),
            RecordDeclarationSyntax rec => rec.Modifiers.Any(SyntaxKind.PartialKeyword),
            _ => false
        };
    }

    /// <summary>
    /// Checks whether a symbol has an attribute with the specified fully-qualified display name.
    /// </summary>
    internal static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == attributeName);
    }

    /// <summary>
    /// Checks whether a method returns Task, Task&lt;T&gt;, ValueTask, or ValueTask&lt;T&gt;.
    /// </summary>
    internal static bool IsAsyncMethod(IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        var typeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return typeName.StartsWith("global::System.Threading.Tasks.Task") ||
               typeName.StartsWith("global::System.Threading.Tasks.ValueTask");
    }

    /// <summary>
    /// Checks whether a type symbol represents <see cref="System.Threading.CancellationToken"/>.
    /// Accepts ITypeSymbol so it can be used from both IParameterSymbol.Type and direct type checks.
    /// </summary>
    internal static bool IsCancellationToken(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return typeName == "global::System.Threading.CancellationToken";
    }

    /// <summary>
    /// Overload that checks whether a parameter is a CancellationToken.
    /// </summary>
    internal static bool IsCancellationToken(IParameterSymbol parameter)
    {
        return IsCancellationToken(parameter.Type);
    }

    /// <summary>
    /// Converts a PascalCase name to camelCase.
    /// </summary>
    internal static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : "");
    }

    /// <summary>
    /// Returns the C# accessibility keyword string for the given accessibility level.
    /// </summary>
    internal static string GetAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.Private => "private",
            _ => "public"
        };
    }

    /// <summary>
    /// Formats a parameter's default value as a C# literal suitable for code generation.
    /// Handles enums, strings, chars, booleans, numeric types, and null.
    /// </summary>
    internal static string FormatDefaultValue(IParameterSymbol param)
    {
        if (param.ExplicitDefaultValue is null)
        {
            if (CanTypeAcceptNull(param.Type))
                return "null";

            if (param.Type.IsValueType)
                return "default";

            return "null";
        }

        if (param.Type.TypeKind == TypeKind.Enum && param.Type is INamedTypeSymbol enumType)
        {
            var enumField = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, param.ExplicitDefaultValue));

            if (enumField != null)
            {
                return $"{enumType.ToDisplayString(TypeFormat)}.{enumField.Name}";
            }

            return $"({enumType.ToDisplayString(TypeFormat)}){param.ExplicitDefaultValue}";
        }

        // Use Roslyn's culture-invariant literal formatting for all other types
        return SymbolDisplay.FormatPrimitive(param.ExplicitDefaultValue, quoteStrings: true, useHexadecimalNumbers: false);
    }

    private static bool CanTypeAcceptNull(ITypeSymbol type)
    {
        if (type is null)
            return false;

        if (type.IsReferenceType)
            return true;

        if (type.NullableAnnotation == NullableAnnotation.Annotated)
            return true;

        if (type is ITypeParameterSymbol typeParam)
            return !typeParam.HasValueTypeConstraint;

        if (type is INamedTypeSymbol named &&
            named.IsGenericType &&
            named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        return false;
    }
}
