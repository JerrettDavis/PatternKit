using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Factories;

[Generator]
public sealed class AbstractFactoryGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKAF001",
        "Abstract factory host must be partial",
        "Type '{0}' is marked with [GenerateAbstractFactory] but is not declared as partial",
        "PatternKit.Generators.Factories",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingProducts = new(
        "PKAF002",
        "Abstract factory has no products",
        "Type '{0}' is marked with [GenerateAbstractFactory] but does not declare any abstract factory products",
        "PatternKit.Generators.Factories",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidProduct = new(
        "PKAF003",
        "Abstract factory product is invalid",
        "Product declaration '{0}' must reference a valid key, contract type, concrete implementation type, and public parameterless constructor",
        "PatternKit.Generators.Factories",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateProduct = new(
        "PKAF004",
        "Abstract factory product is duplicated",
        "Family '{0}' declares product contract '{1}' more than once",
        "PatternKit.Generators.Factories",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Factories.GenerateAbstractFactoryAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Factories.GenerateAbstractFactoryAttribute");
            if (attr is not null)
                Generate(spc, candidate.Type, candidate.Node, attr);
        });
    }

    private static void Generate(
        SourceProductionContext context,
        INamedTypeSymbol type,
        TypeDeclarationSyntax node,
        AttributeData attribute)
    {
        if (!node.Modifiers.Any(static modifier => modifier.Text == "partial"))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var keyType = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (keyType is null)
            return;

        var hasProductAttributes = type.GetAttributes().Any(static attr =>
            attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Factories.AbstractFactoryProductAttribute");
        var products = GetProducts(type, keyType, context);
        if (products.Length == 0)
        {
            if (!hasProductAttributes)
                context.ReportDiagnostic(Diagnostic.Create(MissingProducts, node.Identifier.GetLocation(), type.Name));

            return;
        }

        if (TryFindDuplicate(products, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateProduct, duplicate.Location, duplicate.FamilyKeyText, duplicate.ContractTypeName));
            return;
        }

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        var serviceProviderFactoryMethodName = GetNamedString(attribute, "ServiceProviderFactoryMethodName");
        context.AddSource($"{type.Name}.AbstractFactory.g.cs", SourceText.From(
            GenerateSource(type, keyType, products, factoryMethodName, serviceProviderFactoryMethodName),
            Encoding.UTF8));
    }

    private static ImmutableArray<Product> GetProducts(
        INamedTypeSymbol type,
        INamedTypeSymbol keyType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Product>();
        foreach (var attr in type.GetAttributes().Where(static attr =>
                     attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Factories.AbstractFactoryProductAttribute"))
        {
            if (!TryGetProduct(keyType, attr, out var product))
            {
                var contractName = attr.ConstructorArguments.Length > 1
                    ? (attr.ConstructorArguments[1].Value as INamedTypeSymbol)?.Name
                    : null;
                context.ReportDiagnostic(Diagnostic.Create(InvalidProduct, attr.ApplicationSyntaxReference?.GetSyntax().GetLocation(), contractName ?? type.Name));
                continue;
            }

            builder.Add(product);
        }

        return builder.ToImmutable();
    }

    private static bool TryGetProduct(INamedTypeSymbol keyType, AttributeData attribute, out Product product)
    {
        product = default;
        if (attribute.ConstructorArguments.Length != 3)
            return false;

        var key = attribute.ConstructorArguments[0];
        var contractType = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;
        var implementationType = attribute.ConstructorArguments[2].Value as INamedTypeSymbol;
        if (contractType is null || implementationType is null)
            return false;

        if (!IsKeyCompatible(keyType, key) || !TryFormatKey(key, out var keyExpression, out var keyText))
            return false;

        if (implementationType.IsAbstract || implementationType.TypeKind != TypeKind.Class)
            return false;

        if (!Implements(implementationType, contractType))
            return false;

        if (!implementationType.Constructors.Any(static ctor =>
                ctor.DeclaredAccessibility == Accessibility.Public &&
                ctor.Parameters.Length == 0))
        {
            return false;
        }

        product = new Product(
            keyExpression,
            keyText,
            contractType.ToDisplayString(TypeFormat),
            implementationType.ToDisplayString(TypeFormat),
            GetNamedBool(attribute, "IsDefaultFamily"),
            attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());
        return true;
    }

    private static bool IsKeyCompatible(INamedTypeSymbol keyType, TypedConstant key)
    {
        if (key.IsNull || key.Value is null)
            return !keyType.IsValueType;

        if (key.Type is null)
            return false;

        if (SymbolEqualityComparer.Default.Equals(key.Type, keyType))
            return true;

        return keyType.SpecialType switch
        {
            SpecialType.System_String => key.Value is string,
            SpecialType.System_Boolean => key.Value is bool,
            SpecialType.System_Char => key.Value is char,
            SpecialType.System_Byte => key.Value is byte,
            SpecialType.System_SByte => key.Value is sbyte,
            SpecialType.System_Int16 => key.Value is short,
            SpecialType.System_UInt16 => key.Value is ushort,
            SpecialType.System_Int32 => key.Value is int,
            SpecialType.System_UInt32 => key.Value is uint,
            SpecialType.System_Int64 => key.Value is long,
            SpecialType.System_UInt64 => key.Value is ulong,
            _ => keyType.TypeKind == TypeKind.Enum && SymbolEqualityComparer.Default.Equals(key.Type, keyType)
        };
    }

    private static bool TryFormatKey(TypedConstant key, out string expression, out string text)
    {
        if (key.IsNull)
        {
            expression = "null!";
            text = "<null>";
            return true;
        }

        var constantValue = key.Value;
        if (constantValue is null)
        {
            expression = string.Empty;
            text = string.Empty;
            return false;
        }

        if (key.Type is INamedTypeSymbol enumType && enumType.TypeKind == TypeKind.Enum)
        {
            var enumMember = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(field => field.HasConstantValue && Equals(field.ConstantValue, constantValue));
            if (enumMember is not null)
            {
                expression = enumType.ToDisplayString(TypeFormat) + "." + enumMember.Name;
                text = enumMember.Name;
                return true;
            }

            expression = string.Empty;
            text = string.Empty;
            return false;
        }

        expression = key.Value switch
        {
            string value => "\"" + Escape(value) + "\"",
            char value => "'" + EscapeChar(value) + "'",
            bool value => value ? "true" : "false",
            byte value => value.ToString(CultureInfo.InvariantCulture),
            sbyte value => value.ToString(CultureInfo.InvariantCulture),
            short value => value.ToString(CultureInfo.InvariantCulture),
            ushort value => value.ToString(CultureInfo.InvariantCulture),
            int value => value.ToString(CultureInfo.InvariantCulture),
            uint value => value.ToString(CultureInfo.InvariantCulture) + "u",
            long value => value.ToString(CultureInfo.InvariantCulture) + "L",
            ulong value => value.ToString(CultureInfo.InvariantCulture) + "UL",
            _ => string.Empty
        };

        text = constantValue.ToString() ?? string.Empty;
        return expression.Length > 0;
    }

    private static bool Implements(INamedTypeSymbol implementation, INamedTypeSymbol contract)
    {
        if (SymbolEqualityComparer.Default.Equals(implementation, contract))
            return true;

        if (implementation.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract)))
            return true;

        for (var current = implementation.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, contract))
                return true;
        }

        return false;
    }

    private static bool TryFindDuplicate(IReadOnlyList<Product> products, out Product duplicate)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var product in products)
        {
            var key = product.IsDefaultFamily
                ? "<default>:" + product.ContractTypeName
                : product.FamilyKeyText + ":" + product.ContractTypeName;
            if (!seen.Add(key))
            {
                duplicate = product;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol keyType,
        IReadOnlyList<Product> products,
        string factoryMethodName,
        string? serviceProviderFactoryMethodName)
    {
        var keyTypeName = keyType.ToDisplayString(TypeFormat);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ').Append(type.Name).AppendLine();
        sb.AppendLine("{");
        EmitFactoryMethod(sb, keyTypeName, products, factoryMethodName, useServiceProvider: false);

        if (!string.IsNullOrWhiteSpace(serviceProviderFactoryMethodName))
        {
            sb.AppendLine();
            EmitFactoryMethod(sb, keyTypeName, products, serviceProviderFactoryMethodName!, useServiceProvider: true);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitFactoryMethod(
        StringBuilder sb,
        string keyTypeName,
        IReadOnlyList<Product> products,
        string methodName,
        bool useServiceProvider)
    {
        sb.Append("    public static global::PatternKit.Creational.AbstractFactory.AbstractFactory<")
            .Append(keyTypeName)
            .Append("> ")
            .Append(methodName)
            .Append(useServiceProvider ? "(global::System.IServiceProvider services)" : "()")
            .AppendLine();
        sb.AppendLine("    {");
        if (useServiceProvider)
        {
            sb.AppendLine("        if (services is null)");
            sb.AppendLine("            throw new global::System.ArgumentNullException(nameof(services));");
            sb.AppendLine();
        }

        sb.Append("        var builder = global::PatternKit.Creational.AbstractFactory.AbstractFactory<")
            .Append(keyTypeName)
            .AppendLine(">.Create();");

        foreach (var group in products.Where(static p => !p.IsDefaultFamily).GroupBy(static p => p.FamilyKeyExpression).OrderBy(static g => g.Key, System.StringComparer.Ordinal))
        {
            sb.Append("        builder.Family(").Append(group.Key).AppendLine(");");
            foreach (var product in group.OrderBy(static p => p.ContractTypeName, System.StringComparer.Ordinal))
                EmitProduct(sb, product, useServiceProvider, defaultFamily: false);
        }

        foreach (var product in products.Where(static p => p.IsDefaultFamily).OrderBy(static p => p.ContractTypeName, System.StringComparer.Ordinal))
            EmitProduct(sb, product, useServiceProvider, defaultFamily: true);

        sb.AppendLine("        return builder.Build();");
        sb.AppendLine("    }");
    }

    private static void EmitProduct(StringBuilder sb, Product product, bool useServiceProvider, bool defaultFamily)
    {
        sb.Append(defaultFamily ? "        builder.DefaultProduct<" : "        builder.Product<")
            .Append(product.ContractTypeName)
            .Append(">(() => ");
        if (useServiceProvider)
        {
            sb.Append("global::Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<")
                .Append(product.ImplementationTypeName)
                .Append(">(services)");
        }
        else
        {
            sb.Append("new ").Append(product.ImplementationTypeName).Append("()");
        }

        sb.AppendLine(");");
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeChar(char value)
        => value switch
        {
            '\\' => "\\\\",
            '\'' => "\\'",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ => value.ToString()
        };

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static bool GetNamedBool(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as bool? ?? false;

    private readonly record struct Product(
        string FamilyKeyExpression,
        string FamilyKeyText,
        string ContractTypeName,
        string ImplementationTypeName,
        bool IsDefaultFamily,
        Location? Location);
}
