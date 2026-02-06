using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Adapter pattern (object adapter).
/// Generates a class that implements a target interface by delegating to an adaptee instance
/// via mapping methods defined in a static partial host class.
/// </summary>
[Generator]
public sealed class AdapterGenerator : IIncrementalGenerator
{
    private const string DiagIdHostNotStaticPartial = "PKADP001";
    private const string DiagIdTargetNotInterfaceOrAbstract = "PKADP002";
    private const string DiagIdMissingMapping = "PKADP003";
    private const string DiagIdDuplicateMapping = "PKADP004";
    private const string DiagIdSignatureMismatch = "PKADP005";
    private const string DiagIdTypeNameConflict = "PKADP006";

    private static readonly DiagnosticDescriptor HostNotStaticPartialDescriptor = new(
        id: DiagIdHostNotStaticPartial,
        title: "Host class must be static and partial",
        messageFormat: "Type '{0}' is marked with [GenerateAdapter] but is not declared as both static and partial.",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TargetNotInterfaceOrAbstractDescriptor = new(
        id: DiagIdTargetNotInterfaceOrAbstract,
        title: "Target must be an interface or abstract class",
        messageFormat: "Target type '{0}' is not an interface or abstract class.",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingMappingDescriptor = new(
        id: DiagIdMissingMapping,
        title: "Missing mapping for target member",
        messageFormat: "Target member '{0}' on '{1}' has no mapping method in host class '{2}'.",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMappingDescriptor = new(
        id: DiagIdDuplicateMapping,
        title: "Duplicate mapping for target member",
        messageFormat: "Target member '{0}' has multiple mapping methods in host class '{1}'.",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SignatureMismatchDescriptor = new(
        id: DiagIdSignatureMismatch,
        title: "Mapping method signature mismatch",
        messageFormat: "Mapping method '{0}' does not match the expected signature for target member '{1}'. Expected return type '{2}'.",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TypeNameConflictDescriptor = new(
        id: DiagIdTypeNameConflict,
        title: "Adapter type name conflicts with existing type",
        messageFormat: "Adapter type name '{0}' conflicts with an existing type in namespace '{1}'.",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Adapter.GenerateAdapterAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(targets, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol hostSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Adapter.GenerateAdapterAttribute");
            if (attr is null)
                return;

            GenerateAdapter(spc, hostSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateAdapter(
        SourceProductionContext context,
        INamedTypeSymbol hostSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        // PKADP001: Host must be static partial class
        if (!hostSymbol.IsStatic || !GeneratorUtilities.IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                HostNotStaticPartialDescriptor,
                node.GetLocation(),
                hostSymbol.Name));
            return;
        }

        // Parse attribute
        INamedTypeSymbol targetType = null;
        INamedTypeSymbol adapteeType = null;
        string adapterTypeName = null;
        int missingMapPolicy = 0;

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "Target":
                    targetType = namedArg.Value.Value as INamedTypeSymbol;
                    break;
                case "Adaptee":
                    adapteeType = namedArg.Value.Value as INamedTypeSymbol;
                    break;
                case "AdapterTypeName":
                    adapterTypeName = namedArg.Value.Value as string;
                    break;
                case "MissingMap":
                    missingMapPolicy = namedArg.Value.Value is int m ? m : 0;
                    break;
            }
        }

        if (targetType is null || adapteeType is null)
            return;

        // PKADP002: Target must be interface or abstract class
        if (targetType.TypeKind != TypeKind.Interface && !targetType.IsAbstract)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TargetNotInterfaceOrAbstractDescriptor,
                node.GetLocation(),
                targetType.Name));
            return;
        }

        // Determine adapter type name
        if (string.IsNullOrEmpty(adapterTypeName))
        {
            var targetName = targetType.Name;
            if (targetType.TypeKind == TypeKind.Interface && targetName.StartsWith("I") && targetName.Length > 1 && char.IsUpper(targetName[1]))
                targetName = targetName.Substring(1);

            adapterTypeName = $"{adapteeType.Name}To{targetName}Adapter";
        }

        // PKADP006: Type name conflict
        var containingNamespace = hostSymbol.ContainingNamespace;
        var existingType = containingNamespace.GetTypeMembers(adapterTypeName).FirstOrDefault();
        if (existingType is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNameConflictDescriptor,
                node.GetLocation(),
                adapterTypeName,
                containingNamespace.ToDisplayString()));
            return;
        }

        // Collect target members that need mapping
        var targetMembers = CollectTargetMembers(targetType);

        // Collect mapping methods from host
        var mappingsResult = CollectMappings(hostSymbol, targetMembers, context, node);
        if (mappingsResult is null)
            return; // Diagnostics already reported

        var mappings = mappingsResult.Value;

        // Validate all target members are mapped
        var hasErrors = false;
        foreach (var member in targetMembers)
        {
            var mapping = mappings.FirstOrDefault(m => m.TargetMemberName == member.Name);
            if (mapping.TargetMemberName is null)
            {
                if (missingMapPolicy == 0) // Error
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MissingMappingDescriptor,
                        node.GetLocation(),
                        member.Name,
                        targetType.Name,
                        hostSymbol.Name));
                    hasErrors = true;
                }
            }
            else
            {
                // PKADP005: Validate return type matches
                if (!ValidateReturnType(member, mapping.HostMethod))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SignatureMismatchDescriptor,
                        node.GetLocation(),
                        mapping.HostMethod.Name,
                        member.Name,
                        member.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    hasErrors = true;
                }
            }
        }

        if (hasErrors)
            return;

        // Generate adapter
        var ns = hostSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : hostSymbol.ContainingNamespace.ToDisplayString();

        var source = GenerateAdapterSource(
            ns,
            adapterTypeName,
            targetType,
            adapteeType,
            targetMembers,
            mappings,
            hostSymbol,
            missingMapPolicy);

        context.AddSource($"{adapterTypeName}.Adapter.g.cs", source);
    }

    private static ImmutableArray<IMethodSymbol> CollectTargetMembers(INamedTypeSymbol targetType)
    {
        var builder = ImmutableArray.CreateBuilder<IMethodSymbol>();

        // Collect from interface itself
        foreach (var member in targetType.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.MethodKind == MethodKind.Ordinary)
                builder.Add(member);
        }

        // Collect from inherited interfaces
        foreach (var iface in targetType.AllInterfaces)
        {
            foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.MethodKind == MethodKind.Ordinary)
                    builder.Add(member);
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<MappingInfo>? CollectMappings(
        INamedTypeSymbol hostSymbol,
        ImmutableArray<IMethodSymbol> targetMembers,
        SourceProductionContext context,
        SyntaxNode node)
    {
        var builder = ImmutableArray.CreateBuilder<MappingInfo>();
        var mappedTargetMembers = new Dictionary<string, List<IMethodSymbol>>();

        foreach (var method in hostSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var mapAttr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Adapter.AdapterMapAttribute");

            if (mapAttr is null)
                continue;

            // Determine which target member this maps to
            string targetMemberName = null;
            foreach (var namedArg in mapAttr.NamedArguments)
            {
                if (namedArg.Key == "TargetMember")
                    targetMemberName = namedArg.Value.Value as string;
            }

            // If no explicit target member, match by method name
            if (string.IsNullOrEmpty(targetMemberName))
                targetMemberName = method.Name;

            // Track duplicates
            if (!mappedTargetMembers.ContainsKey(targetMemberName))
                mappedTargetMembers[targetMemberName] = new List<IMethodSymbol>();
            mappedTargetMembers[targetMemberName].Add(method);

            builder.Add(new MappingInfo
            {
                TargetMemberName = targetMemberName,
                HostMethod = method
            });
        }

        // PKADP004: Check for duplicate mappings
        foreach (var kvp in mappedTargetMembers)
        {
            if (kvp.Value.Count > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateMappingDescriptor,
                    node.GetLocation(),
                    kvp.Key,
                    hostSymbol.Name));
                return null;
            }
        }

        return builder.ToImmutable();
    }

    private static bool ValidateReturnType(IMethodSymbol targetMember, IMethodSymbol hostMethod)
    {
        return SymbolEqualityComparer.Default.Equals(
            targetMember.ReturnType,
            hostMethod.ReturnType);
    }

    private static string GenerateAdapterSource(
        string ns,
        string adapterTypeName,
        INamedTypeSymbol targetType,
        INamedTypeSymbol adapteeType,
        ImmutableArray<IMethodSymbol> targetMembers,
        ImmutableArray<MappingInfo> mappings,
        INamedTypeSymbol hostSymbol,
        int missingMapPolicy)
    {
        var targetFullName = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var adapteeFullName = adapteeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var hostFullName = hostSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Object adapter that implements <see cref=\"{targetType.Name}\"/>");
        sb.AppendLine($"/// by delegating to a <see cref=\"{adapteeType.Name}\"/> instance.");
        sb.AppendLine("/// </summary>");

        var implementsClause = targetType.TypeKind == TypeKind.Interface
            ? $" : {targetFullName}"
            : $" : {targetFullName}";

        sb.AppendLine($"public sealed class {adapterTypeName}{implementsClause}");
        sb.AppendLine("{");

        // Adaptee field
        sb.AppendLine($"    private readonly {adapteeFullName} _adaptee;");
        sb.AppendLine();

        // Constructor
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of <see cref=\"{adapterTypeName}\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public {adapterTypeName}({adapteeFullName} adaptee)");
        sb.AppendLine("    {");
        sb.AppendLine("        _adaptee = adaptee ?? throw new System.ArgumentNullException(nameof(adaptee));");
        sb.AppendLine("    }");

        // Generate methods in stable order
        foreach (var member in targetMembers.OrderBy(m => m.Name))
        {
            sb.AppendLine();

            var mapping = mappings.FirstOrDefault(m => m.TargetMemberName == member.Name);

            if (mapping.TargetMemberName is not null)
            {
                GenerateMappedMethod(sb, member, mapping.HostMethod, hostFullName);
            }
            else if (missingMapPolicy == 1) // ThrowingStub
            {
                GenerateThrowingStub(sb, member);
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateMappedMethod(
        StringBuilder sb,
        IMethodSymbol targetMember,
        IMethodSymbol hostMethod,
        string hostFullName)
    {
        var returnType = targetMember.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var parameters = string.Join(", ", targetMember.Parameters.Select(p =>
            $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));

        sb.AppendLine($"    /// <inheritdoc />");
        sb.AppendLine($"    public {returnType} {targetMember.Name}({parameters})");
        sb.AppendLine("    {");

        // Build call arguments: first arg is _adaptee, then the target method parameters
        var args = new StringBuilder();
        args.Append("_adaptee");
        foreach (var param in targetMember.Parameters)
        {
            args.Append(", ");
            args.Append(param.Name);
        }

        var returnKeyword = targetMember.ReturnsVoid ? "" : "return ";
        sb.AppendLine($"        {returnKeyword}{hostFullName}.{hostMethod.Name}({args});");

        sb.AppendLine("    }");
    }

    private static void GenerateThrowingStub(
        StringBuilder sb,
        IMethodSymbol targetMember)
    {
        var returnType = targetMember.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var parameters = string.Join(", ", targetMember.Parameters.Select(p =>
            $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));

        sb.AppendLine($"    /// <inheritdoc />");
        sb.AppendLine($"    public {returnType} {targetMember.Name}({parameters})");
        sb.AppendLine("    {");
        sb.AppendLine($"        throw new System.NotImplementedException(\"Mapping for '{targetMember.Name}' is not yet defined.\");");
        sb.AppendLine("    }");
    }

    private struct MappingInfo
    {
        public string TargetMemberName;
        public IMethodSymbol HostMethod;
    }
}
