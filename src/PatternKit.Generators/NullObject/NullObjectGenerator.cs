using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.NullObject;

[Generator]
public sealed class NullObjectGenerator : IIncrementalGenerator
{
    private const string GenerateNullObjectAttributeName = "PatternKit.Generators.NullObject.GenerateNullObjectAttribute";
    private const string NullObjectDefaultAttributeName = "PatternKit.Generators.NullObject.NullObjectDefaultAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBeInterface = new(
        "PKNO001",
        "Null Object contract must be an interface",
        "Type '{0}' is marked with [GenerateNullObject] but is not an interface",
        "PatternKit.Generators.NullObject",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor GenericContractsNotSupported = new(
        "PKNO002",
        "Generic Null Object contracts are not supported",
        "Type '{0}' is generic; generate a Null Object for a closed non-generic facade contract",
        "PatternKit.Generators.NullObject",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidTypeName = new(
        "PKNO003",
        "Null Object type name is invalid",
        "Generated Null Object type name '{0}' is not a valid C# identifier",
        "PatternKit.Generators.NullObject",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedMember = new(
        "PKNO004",
        "Null Object contract member is not supported",
        "Member '{0}' on Null Object contract '{1}' is not supported: {2}",
        "PatternKit.Generators.NullObject",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor TypeNameConflict = new(
        "PKNO005",
        "Null Object type name conflicts with an existing type",
        "Generated Null Object type name '{0}' conflicts with an existing type in namespace '{1}'",
        "PatternKit.Generators.NullObject",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var contracts = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateNullObjectAttributeName,
            static (node, _) => node is InterfaceDeclarationSyntax,
            static (ctx, _) => ctx);

        context.RegisterSourceOutput(contracts, static (spc, contractContext) =>
        {
            if (contractContext.TargetSymbol is not INamedTypeSymbol contract)
                return;

            var attribute = contractContext.Attributes.FirstOrDefault(static attr =>
                attr.AttributeClass?.ToDisplayString() == GenerateNullObjectAttributeName);
            if (attribute is null)
                return;

            Generate(spc, contract, attribute, contractContext.TargetNode);
        });
    }

    private static void Generate(SourceProductionContext context, INamedTypeSymbol contract, AttributeData attribute, SyntaxNode node)
    {
        if (contract.TypeKind != TypeKind.Interface)
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBeInterface, node.GetLocation(), contract.Name));
            return;
        }

        if (contract.TypeParameters.Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(GenericContractsNotSupported, node.GetLocation(), contract.Name));
            return;
        }

        var typeName = GetNamedString(attribute, "TypeName") ?? $"Null{TrimInterfacePrefix(contract.Name)}";
        if (!IsIdentifier(typeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidTypeName, node.GetLocation(), typeName));
            return;
        }

        if (contract.ContainingNamespace.GetTypeMembers(typeName).Any(static type => type.TypeKind != TypeKind.Error))
        {
            var namespaceName = contract.ContainingNamespace.IsGlobalNamespace
                ? "<global namespace>"
                : contract.ContainingNamespace.ToDisplayString();
            context.ReportDiagnostic(Diagnostic.Create(TypeNameConflict, node.GetLocation(), typeName, namespaceName));
            return;
        }

        var unsupportedMember = GetUnsupportedMember(contract);
        if (unsupportedMember is not null)
        {
            var location = unsupportedMember.Symbol.Locations.FirstOrDefault() ?? node.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(
                UnsupportedMember,
                location,
                unsupportedMember.Symbol.Name,
                contract.Name,
                unsupportedMember.Reason));
            return;
        }

        var source = GenerateSource(contract, typeName);
        var hintPrefix = contract.ContainingNamespace.IsGlobalNamespace
            ? contract.Name
            : contract.ContainingNamespace.ToDisplayString().Replace(".", "_") + "_" + contract.Name;
        context.AddSource($"{hintPrefix}.NullObject.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateSource(INamedTypeSymbol contract, string typeName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        var hasNamespace = !contract.ContainingNamespace.IsGlobalNamespace;
        if (hasNamespace)
        {
            sb.Append("namespace ").Append(contract.ContainingNamespace.ToDisplayString()).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append(GetAccessibility(contract.DeclaredAccessibility)).Append(" sealed class ").Append(typeName)
            .Append(" : ").Append(contract.ToDisplayString(TypeFormat)).AppendLine();
        sb.AppendLine("{");
        sb.Append("    public static ").Append(typeName).Append(" Instance { get; } = new();").AppendLine();
        sb.AppendLine();
        sb.Append("    private ").Append(typeName).AppendLine("()");
        sb.AppendLine("    {");
        sb.AppendLine("    }");

        var members = GetContractMembers(contract);

        foreach (var @event in members.OfType<IEventSymbol>().Where(static e => !e.IsStatic))
        {
            sb.AppendLine();
            AppendEvent(sb, @event);
        }

        foreach (var property in members.OfType<IPropertySymbol>().Where(static p => !p.IsStatic))
        {
            sb.AppendLine();
            AppendProperty(sb, property);
        }

        foreach (var method in members.OfType<IMethodSymbol>().Where(static m => m.MethodKind == MethodKind.Ordinary && !m.IsStatic))
        {
            sb.AppendLine();
            AppendMethod(sb, method);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static IEnumerable<ISymbol> GetContractMembers(INamedTypeSymbol contract)
    {
        var emittedMembers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in contract.GetMembers())
        {
            if (emittedMembers.Add(GetMemberImplementationKey(member)))
                yield return member;
        }

        foreach (var baseInterface in contract.AllInterfaces)
        {
            foreach (var member in baseInterface.GetMembers())
            {
                if (emittedMembers.Add(GetMemberImplementationKey(member)))
                    yield return member;
            }
        }
    }

    private static UnsupportedContractMember? GetUnsupportedMember(INamedTypeSymbol contract)
    {
        if (contract.ContainingType is not null)
            return new UnsupportedContractMember(contract, "nested Null Object contracts are not supported");

        var conflictingMember = GetConflictingMember(contract);
        if (conflictingMember is not null)
            return conflictingMember;

        foreach (var member in GetContractMembers(contract))
        {
            if (member.IsStatic && member.IsAbstract)
                return new UnsupportedContractMember(member, "static abstract interface members must be implemented explicitly");

            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                && (method.ReturnsByRef || method.ReturnsByRefReadonly))
            {
                return new UnsupportedContractMember(method, "by-ref return members are not supported");
            }

            if (member is IPropertySymbol property
                && (property.ReturnsByRef || property.ReturnsByRefReadonly))
            {
                return new UnsupportedContractMember(property, "by-ref return properties and indexers are not supported");
            }
        }

        return null;
    }

    private static UnsupportedContractMember? GetConflictingMember(INamedTypeSymbol contract)
    {
        var groups = GetAllContractMembers(contract)
            .GroupBy(GetMemberConflictKey, StringComparer.Ordinal)
            .Where(static group => group.Select(GetMemberImplementationKey).Distinct(StringComparer.Ordinal).Skip(1).Any());

        foreach (var group in groups)
        {
            return new UnsupportedContractMember(
                group.First(),
                "hidden interface members with conflicting signatures are not supported");
        }

        return null;
    }

    private static IEnumerable<ISymbol> GetAllContractMembers(INamedTypeSymbol contract)
    {
        foreach (var member in contract.GetMembers())
            yield return member;

        foreach (var baseInterface in contract.AllInterfaces)
        {
            foreach (var member in baseInterface.GetMembers())
                yield return member;
        }
    }

    private static void AppendEvent(StringBuilder sb, IEventSymbol @event)
    {
        sb.Append("    public event ").Append(@event.Type.ToDisplayString(TypeFormat)).Append(' ').Append(EscapeIdentifier(@event.Name)).AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        add { }");
        sb.AppendLine("        remove { }");
        sb.AppendLine("    }");
    }

    private static void AppendProperty(StringBuilder sb, IPropertySymbol property)
    {
        var type = property.Type.ToDisplayString(TypeFormat);
        var defaultExpression = GetDefaultExpression(property.Type, GetConfiguredDefault(property));
        sb.Append("    public ").Append(type).Append(' ');

        if (property.IsIndexer)
            sb.Append("this[").Append(string.Join(", ", property.Parameters.Select(FormatParameter))).Append(']');
        else
            sb.Append(EscapeIdentifier(property.Name));

        if (property.GetMethod is not null && property.SetMethod is not null)
        {
            var setterKeyword = property.SetMethod.IsInitOnly ? "init" : "set";
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.Append("        get => ").Append(defaultExpression).AppendLine(";");
            sb.Append("        ").Append(setterKeyword).AppendLine(" { }");
            sb.AppendLine("    }");
            return;
        }

        if (property.GetMethod is not null)
        {
            sb.Append(" => ").Append(defaultExpression).AppendLine(";");
            return;
        }

        sb.AppendLine();
        sb.AppendLine("    {");
        sb.Append("        ").Append(property.SetMethod?.IsInitOnly == true ? "init" : "set").AppendLine(" { }");
        sb.AppendLine("    }");
    }

    private static void AppendMethod(StringBuilder sb, IMethodSymbol method)
    {
        var returnType = method.ReturnType.ToDisplayString(TypeFormat);
        sb.Append("    public ").Append(returnType).Append(' ').Append(EscapeIdentifier(method.Name))
            .Append(GetTypeParameterList(method)).Append('(');
        sb.Append(string.Join(", ", method.Parameters.Select(FormatParameter)));
        sb.Append(')').Append(GetConstraintClauses(method));

        var outParameters = method.Parameters.Where(static parameter => parameter.RefKind == RefKind.Out).ToArray();
        if (outParameters.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    {");
            foreach (var parameter in outParameters)
                sb.Append("        ").Append(EscapeIdentifier(parameter.Name)).Append(" = ").Append(GetDefaultExpression(parameter.Type, null)).AppendLine(";");
            if (!method.ReturnsVoid)
                sb.Append("        return ").Append(GetDefaultExpression(method.ReturnType, GetConfiguredDefault(method))).AppendLine(";");
            sb.AppendLine("    }");
            return;
        }

        if (method.ReturnsVoid)
        {
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            return;
        }

        sb.Append(" => ").Append(GetDefaultExpression(method.ReturnType, GetConfiguredDefault(method))).AppendLine(";");
    }

    private static string FormatParameter(IParameterSymbol parameter)
    {
        var prefix = parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnlyParameter => "ref readonly ",
            _ => string.Empty
        };

        return prefix + parameter.Type.ToDisplayString(TypeFormat) + " " + EscapeIdentifier(parameter.Name);
    }

    private static string GetTypeParameterList(IMethodSymbol method)
        => method.TypeParameters.Length == 0
            ? string.Empty
            : "<" + string.Join(", ", method.TypeParameters.Select(static parameter => parameter.Name)) + ">";

    private static string GetConstraintClauses(IMethodSymbol method)
    {
        if (method.TypeParameters.Length == 0)
            return string.Empty;

        var clauses = new List<string>();
        foreach (var parameter in method.TypeParameters)
        {
            var constraints = new List<string>();
            if (parameter.HasReferenceTypeConstraint)
                constraints.Add(parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
            if (parameter.HasNotNullConstraint)
                constraints.Add("notnull");
            if (parameter.HasUnmanagedTypeConstraint)
                constraints.Add("unmanaged");
            else if (parameter.HasValueTypeConstraint)
                constraints.Add("struct");
            constraints.AddRange(parameter.ConstraintTypes.Select(static constraint => constraint.ToDisplayString(TypeFormat)));
            if (parameter.HasConstructorConstraint)
                constraints.Add("new()");
            if (constraints.Count > 0)
                clauses.Add($" where {parameter.Name} : {string.Join(", ", constraints)}");
        }

        return string.Concat(clauses);
    }

    private static string GetDefaultExpression(ITypeSymbol type, object? configured)
    {
        if (type.SpecialType == SpecialType.System_String)
            return configured is not null ? GetConfiguredDefaultExpression(type, configured) : "string.Empty";
        if (type.SpecialType == SpecialType.System_Boolean)
            return configured is not null ? GetConfiguredDefaultExpression(type, configured) : "false";
        if (IsNumeric(type))
            return configured is not null ? GetConfiguredDefaultExpression(type, configured) : "0";
        if (type is IArrayTypeSymbol arrayType)
            return $"global::System.Array.Empty<{arrayType.ElementType.ToDisplayString(TypeFormat)}>()";
        if (type.ToDisplayString(TypeFormat) == "global::System.Threading.Tasks.Task")
            return "global::System.Threading.Tasks.Task.CompletedTask";
        if (type is INamedTypeSymbol named && IsNamedType(named.ConstructedFrom, "System.Threading.Tasks.Task`1"))
            return $"global::System.Threading.Tasks.Task.FromResult<{named.TypeArguments[0].ToDisplayString(TypeFormat)}>({GetDefaultExpression(named.TypeArguments[0], configured)})";
        if (type is INamedTypeSymbol valueTaskOfT && IsNamedType(valueTaskOfT.ConstructedFrom, "System.Threading.Tasks.ValueTask`1"))
            return $"new global::System.Threading.Tasks.ValueTask<{valueTaskOfT.TypeArguments[0].ToDisplayString(TypeFormat)}>({GetDefaultExpression(valueTaskOfT.TypeArguments[0], configured)})";
        if (type is INamedTypeSymbol valueTask && IsNamedType(valueTask, "System.Threading.Tasks.ValueTask"))
            return "default";

        if (configured is not null)
            return GetConfiguredDefaultExpression(type, configured);

        return "default!";
    }

    private static string GetConfiguredDefaultExpression(ITypeSymbol type, object value)
        => value switch
        {
            string text when type.SpecialType == SpecialType.System_String => "@\"" + text.Replace("\"", "\"\"") + "\"",
            bool flag when type.SpecialType == SpecialType.System_Boolean => flag ? "true" : "false",
            int number when IsNumeric(type) => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            long number when IsNumeric(type) => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double number when double.IsNaN(number) && type.SpecialType == SpecialType.System_Single => "float.NaN",
            double number when double.IsPositiveInfinity(number) && type.SpecialType == SpecialType.System_Single => "float.PositiveInfinity",
            double number when double.IsNegativeInfinity(number) && type.SpecialType == SpecialType.System_Single => "float.NegativeInfinity",
            double number when double.IsNaN(number) && type.SpecialType == SpecialType.System_Double => "double.NaN",
            double number when double.IsPositiveInfinity(number) && type.SpecialType == SpecialType.System_Double => "double.PositiveInfinity",
            double number when double.IsNegativeInfinity(number) && type.SpecialType == SpecialType.System_Double => "double.NegativeInfinity",
            double number when type.SpecialType == SpecialType.System_Single => number.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "F",
            double number when type.SpecialType == SpecialType.System_Decimal => number.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "M",
            double number when IsNumeric(type) => number.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            _ => GetDefaultExpression(type, null)
        };

    private static bool IsNumeric(ITypeSymbol type)
        => type.SpecialType is SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal;

    private static bool IsNamedType(INamedTypeSymbol type, string metadataName)
        => type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName == metadataName;

    private static object? GetConfiguredDefault(ISymbol symbol)
        => symbol.GetAttributes()
            .FirstOrDefault(static attr => attr.AttributeClass?.ToDisplayString() == NullObjectDefaultAttributeName)
            ?.ConstructorArguments.FirstOrDefault().Value;

    private static string TrimInterfacePrefix(string name)
        => name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1])
            ? name.Substring(1)
            : name;

    private static bool IsIdentifier(string value)
        => !string.IsNullOrWhiteSpace(value)
            && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None
            && (char.IsLetter(value[0]) || value[0] == '_')
            && value.Skip(1).All(static c => char.IsLetterOrDigit(c) || c == '_');

    private static string GetAccessibility(Accessibility accessibility)
        => accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => "internal"
        };

    private static string EscapeIdentifier(string value)
        => SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;

    private static string GetMemberImplementationKey(ISymbol member)
        => member switch
        {
            IMethodSymbol method when method.MethodKind == MethodKind.Ordinary =>
                "M:" + method.ReturnType.ToDisplayString(TypeFormat) + ":" + method.Name + "`" + method.TypeParameters.Length + "(" + string.Join(",", method.Parameters.Select(static parameter => parameter.RefKind + ":" + parameter.Type.ToDisplayString(TypeFormat))) + ")",
            IPropertySymbol property when property.IsIndexer =>
                "P:" + property.Type.ToDisplayString(TypeFormat) + ":this(" + string.Join(",", property.Parameters.Select(static parameter => parameter.RefKind + ":" + parameter.Type.ToDisplayString(TypeFormat))) + ")",
            IPropertySymbol property => "P:" + property.Type.ToDisplayString(TypeFormat) + ":" + property.Name,
            IEventSymbol @event => "E:" + @event.Type.ToDisplayString(TypeFormat) + ":" + @event.Name,
            _ => member.Kind + ":" + member.Name
        };

    private static string GetMemberConflictKey(ISymbol member)
        => member switch
        {
            IMethodSymbol method when method.MethodKind == MethodKind.Ordinary =>
                "M:" + method.Name + "`" + method.TypeParameters.Length + "(" + string.Join(",", method.Parameters.Select(static parameter => parameter.RefKind + ":" + parameter.Type.ToDisplayString(TypeFormat))) + ")",
            IPropertySymbol property when property.IsIndexer =>
                "P:this(" + string.Join(",", property.Parameters.Select(static parameter => parameter.RefKind + ":" + parameter.Type.ToDisplayString(TypeFormat))) + ")",
            IPropertySymbol property => "P:" + property.Name,
            IEventSymbol @event => "E:" + @event.Name,
            _ => member.Kind + ":" + member.Name
        };

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private sealed record UnsupportedContractMember(ISymbol Symbol, string Reason);
}
