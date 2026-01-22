using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Decorator pattern.
/// Generates base decorator classes that forward all members to an inner instance,
/// with optional composition helpers for building decorator chains.
/// </summary>
[Generator]
public sealed class DecoratorGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKDEC001";
    private const string DiagIdUnsupportedMember = "PKDEC002";
    private const string DiagIdNameConflict = "PKDEC003";
    private const string DiagIdInaccessibleMember = "PKDEC004";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [GenerateDecorator] must be partial",
        messageFormat: "Type '{0}' is marked with [GenerateDecorator] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Decorator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedMemberDescriptor = new(
        id: DiagIdUnsupportedMember,
        title: "Unsupported member kind for decorator generation",
        messageFormat: "Member '{0}' of kind '{1}' is not supported in decorator generation (v1). Only methods and properties are supported.",
        category: "PatternKit.Generators.Decorator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NameConflictDescriptor = new(
        id: DiagIdNameConflict,
        title: "Name conflict for generated decorator types",
        messageFormat: "Generated type name '{0}' conflicts with an existing type. Use BaseTypeName or HelpersTypeName to specify a different name.",
        category: "PatternKit.Generators.Decorator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InaccessibleMemberDescriptor = new(
        id: DiagIdInaccessibleMember,
        title: "Member is not accessible for decorator generation",
        messageFormat: "Member '{0}' cannot be accessed for decorator forwarding. Ensure the member is public or internal with InternalsVisibleTo.",
        category: "PatternKit.Generators.Decorator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all types (interfaces or abstract classes) marked with [GenerateDecorator]
        var decoratorContracts = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Decorator.GenerateDecoratorAttribute",
            predicate: static (node, _) => node is InterfaceDeclarationSyntax or ClassDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Generate for each contract
        context.RegisterSourceOutput(decoratorContracts, (spc, contractContext) =>
        {
            if (contractContext.TargetSymbol is not INamedTypeSymbol contractSymbol)
                return;

            var attr = contractContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Decorator.GenerateDecoratorAttribute");
            if (attr is null)
                return;

            GenerateDecoratorForContract(spc, contractSymbol, attr, contractContext.TargetNode);
        });
    }

    private void GenerateDecoratorForContract(
        SourceProductionContext context,
        INamedTypeSymbol contractSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        // Check if type is partial (for interfaces and abstract classes)
        if (contractSymbol.TypeKind == TypeKind.Interface)
        {
            // Interfaces don't need to be partial
        }
        else if (contractSymbol.TypeKind == TypeKind.Class && contractSymbol.IsAbstract)
        {
            // Abstract classes should be partial if we want to add members
            // But we're generating a separate class, so not required
        }
        else
        {
            // Not an interface or abstract class - error
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor,
                node.GetLocation(),
                contractSymbol.Name));
            return;
        }

        // Parse attribute arguments
        var config = ParseDecoratorConfig(attribute, contractSymbol);

        // Analyze contract and members
        var contractInfo = AnalyzeContract(contractSymbol, config, context);
        if (contractInfo is null)
            return;

        // Check for name conflicts
        if (HasNameConflict(contractSymbol, config.BaseTypeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NameConflictDescriptor,
                node.GetLocation(),
                config.BaseTypeName));
            return;
        }

        // Generate base decorator class
        var decoratorSource = GenerateBaseDecorator(contractInfo, config, context);
        if (!string.IsNullOrEmpty(decoratorSource))
        {
            var fileName = $"{contractSymbol.Name}.Decorator.g.cs";
            context.AddSource(fileName, decoratorSource);
        }
    }

    private DecoratorConfig ParseDecoratorConfig(AttributeData attribute, INamedTypeSymbol contractSymbol)
    {
        var config = new DecoratorConfig
        {
            ContractName = contractSymbol.Name,
            Namespace = contractSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : contractSymbol.ContainingNamespace.ToDisplayString()
        };

        // Determine default base type name
        var baseName = contractSymbol.Name;
        if (baseName.StartsWith("I") && baseName.Length > 1 && char.IsUpper(baseName[1]))
        {
            // Interface with I prefix: IStorage -> StorageDecoratorBase
            baseName = baseName.Substring(1);
        }
        config.BaseTypeName = $"{baseName}DecoratorBase";
        config.HelpersTypeName = $"{baseName}Decorators";

        foreach (var named in attribute.NamedArguments)
        {
            switch (named.Key)
            {
                case nameof(Decorator.GenerateDecoratorAttribute.BaseTypeName):
                    if (named.Value.Value is string baseTypeName && !string.IsNullOrWhiteSpace(baseTypeName))
                        config.BaseTypeName = baseTypeName;
                    break;
                case nameof(Decorator.GenerateDecoratorAttribute.HelpersTypeName):
                    if (named.Value.Value is string helpersTypeName && !string.IsNullOrWhiteSpace(helpersTypeName))
                        config.HelpersTypeName = helpersTypeName;
                    break;
                case nameof(Decorator.GenerateDecoratorAttribute.Composition):
                    config.Composition = (int)named.Value.Value!;
                    break;
                case nameof(Decorator.GenerateDecoratorAttribute.GenerateAsync):
                    config.GenerateAsync = (bool)named.Value.Value!;
                    break;
                case nameof(Decorator.GenerateDecoratorAttribute.ForceAsync):
                    config.ForceAsync = (bool)named.Value.Value!;
                    break;
            }
        }

        return config;
    }

    private ContractInfo? AnalyzeContract(
        INamedTypeSymbol contractSymbol,
        DecoratorConfig config,
        SourceProductionContext context)
    {
        var contractInfo = new ContractInfo
        {
            ContractSymbol = contractSymbol,
            ContractName = contractSymbol.Name,
            Namespace = config.Namespace,
            IsInterface = contractSymbol.TypeKind == TypeKind.Interface,
            IsAbstractClass = contractSymbol.TypeKind == TypeKind.Class && contractSymbol.IsAbstract,
            Members = new List<MemberInfo>()
        };

        // Collect members based on contract type
        var members = GetMembersForDecorator(contractSymbol, contractInfo, context);
        contractInfo.Members.AddRange(members);

        if (contractInfo.Members.Count == 0)
        {
            // No members to forward - this might be intentional, but warn
            return contractInfo;
        }

        // Detect if any async members exist
        contractInfo.HasAsyncMembers = contractInfo.Members.Any(m => m.IsAsync);

        return contractInfo;
    }

    private List<MemberInfo> GetMembersForDecorator(
        INamedTypeSymbol contractSymbol,
        ContractInfo contractInfo,
        SourceProductionContext context)
    {
        var members = new List<MemberInfo>();

        // Get all members from the contract and its base types
        var allMembers = GetAllInterfaceMembers(contractSymbol);

        foreach (var member in allMembers)
        {
            // Check for ignore attribute
            if (HasAttribute(member, "PatternKit.Generators.Decorator.DecoratorIgnoreAttribute"))
                continue;

            // Only process methods and properties
            if (member is IMethodSymbol method)
            {
                // Skip special methods (constructors, operators, property accessors, etc.)
                if (method.MethodKind != MethodKind.Ordinary)
                    continue;

                // For abstract classes, only include virtual or abstract methods
                if (contractInfo.IsAbstractClass && !method.IsVirtual && !method.IsAbstract)
                    continue;

                // Skip inaccessible methods
                if (method.DeclaredAccessibility != Accessibility.Public)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InaccessibleMemberDescriptor,
                        member.Locations.FirstOrDefault(),
                        member.Name));
                    continue;
                }

                var isAsync = IsAsyncMethod(method);
                var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                members.Add(new MemberInfo
                {
                    Name = method.Name,
                    MemberType = MemberType.Method,
                    ReturnType = returnType,
                    IsAsync = isAsync,
                    IsVoid = method.ReturnsVoid,
                    Parameters = method.Parameters.Select(p => new ParameterInfo
                    {
                        Name = p.Name,
                        Type = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        HasDefaultValue = p.HasExplicitDefaultValue,
                        DefaultValue = p.HasExplicitDefaultValue ? FormatDefaultValue(p) : null,
                        RefKind = p.RefKind
                    }).ToList()
                });
            }
            else if (member is IPropertySymbol property)
            {
                // For abstract classes, only include virtual or abstract properties
                if (contractInfo.IsAbstractClass && !property.IsVirtual && !property.IsAbstract)
                    continue;

                // Skip inaccessible properties
                if (property.DeclaredAccessibility != Accessibility.Public)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InaccessibleMemberDescriptor,
                        member.Locations.FirstOrDefault(),
                        member.Name));
                    continue;
                }

                var propInfo = new MemberInfo
                {
                    Name = property.Name,
                    MemberType = MemberType.Property,
                    ReturnType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    HasGetter = property.GetMethod is not null,
                    HasSetter = property.SetMethod is not null,
                    IsAsync = false
                };

                members.Add(propInfo);
            }
            else if (member is IEventSymbol)
            {
                // Events not supported in v1
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedMemberDescriptor,
                    member.Locations.FirstOrDefault(),
                    member.Name,
                    "Event"));
            }
        }

        // Sort members by name for deterministic ordering
        return members.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
    }

    private IEnumerable<ISymbol> GetAllInterfaceMembers(INamedTypeSymbol type)
    {
        var members = new List<ISymbol>();

        if (type.TypeKind == TypeKind.Interface)
        {
            // For interfaces, collect from this interface and all base interfaces
            members.AddRange(type.GetMembers());
            foreach (var baseInterface in type.AllInterfaces)
            {
                members.AddRange(baseInterface.GetMembers());
            }
        }
        else if (type.TypeKind == TypeKind.Class && type.IsAbstract)
        {
            // For abstract classes, collect all members (we'll filter virtual/abstract later)
            members.AddRange(type.GetMembers());
        }

        return members;
    }

    private static bool IsAsyncMethod(IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        var typeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return typeName.StartsWith("System.Threading.Tasks.Task") ||
               typeName.StartsWith("System.Threading.Tasks.ValueTask");
    }

    private static string FormatDefaultValue(IParameterSymbol param)
    {
        if (param.ExplicitDefaultValue is null)
            return "null";

        if (param.Type.TypeKind == TypeKind.Enum)
            return $"{param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{param.ExplicitDefaultValue}";

        if (param.ExplicitDefaultValue is string str)
            return $"\"{str}\"";

        if (param.ExplicitDefaultValue is bool b)
            return b ? "true" : "false";

        return param.ExplicitDefaultValue.ToString()!;
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == attributeName);
    }

    private static bool HasNameConflict(INamedTypeSymbol contractSymbol, string generatedName)
    {
        // Check if there's already a type with the generated name in the same namespace
        var containingNamespace = contractSymbol.ContainingNamespace;
        var existingTypes = containingNamespace.GetTypeMembers(generatedName);
        return existingTypes.Length > 0;
    }

    private string GenerateBaseDecorator(ContractInfo contractInfo, DecoratorConfig config, SourceProductionContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();

        // Only add namespace declaration if not in global namespace
        if (!string.IsNullOrEmpty(contractInfo.Namespace))
        {
            sb.AppendLine($"namespace {contractInfo.Namespace};");
            sb.AppendLine();
        }

        // Generate base decorator class
        var contractFullName = contractInfo.ContractSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        sb.AppendLine($"/// <summary>Base decorator for {contractInfo.ContractName}. All members forward to Inner.</summary>");
        sb.AppendLine($"public abstract partial class {config.BaseTypeName} : {contractFullName}");
        sb.AppendLine("{");

        // Constructor
        sb.AppendLine($"    /// <summary>Initializes the decorator with an inner instance.</summary>");
        sb.AppendLine($"    protected {config.BaseTypeName}({contractFullName} inner)");
        sb.AppendLine("    {");
        sb.AppendLine("        Inner = inner ?? throw new System.ArgumentNullException(nameof(inner));");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Inner property
        sb.AppendLine($"    /// <summary>Gets the inner {contractInfo.ContractName} instance.</summary>");
        sb.AppendLine($"    protected {contractFullName} Inner {{ get; }}");
        sb.AppendLine();

        // Generate forwarding members
        foreach (var member in contractInfo.Members)
        {
            if (member.MemberType == MemberType.Method)
            {
                GenerateForwardingMethod(sb, member, config);
            }
            else if (member.MemberType == MemberType.Property)
            {
                GenerateForwardingProperty(sb, member, config);
            }
        }

        sb.AppendLine("}");

        // Generate composition helpers if requested
        if (config.Composition == 1) // HelpersOnly
        {
            sb.AppendLine();
            GenerateCompositionHelpers(sb, contractInfo, config);
        }

        return sb.ToString();
    }

    private void GenerateForwardingMethod(StringBuilder sb, MemberInfo member, DecoratorConfig config)
    {
        var asyncKeyword = member.IsAsync ? "async " : "";
        var awaitKeyword = member.IsAsync ? "await " : "";
        
        sb.AppendLine($"    /// <summary>Forwards to Inner.{member.Name}.</summary>");
        sb.Append($"    public virtual {asyncKeyword}{member.ReturnType} {member.Name}(");

        // Generate parameters
        var paramList = string.Join(", ", member.Parameters.Select(p =>
        {
            var refKind = p.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => ""
            };
            var defaultVal = p.HasDefaultValue ? $" = {p.DefaultValue}" : "";
            return $"{refKind}{p.Type} {p.Name}{defaultVal}";
        }));

        sb.Append(paramList);
        sb.AppendLine(")");

        // Generate method body
        if (member.IsVoid)
        {
            sb.AppendLine("    {");
            sb.Append($"        {awaitKeyword}Inner.{member.Name}(");
            sb.Append(string.Join(", ", member.Parameters.Select(p =>
            {
                var refKind = p.RefKind switch
                {
                    RefKind.Ref => "ref ",
                    RefKind.Out => "out ",
                    RefKind.In => "in ",
                    _ => ""
                };
                return $"{refKind}{p.Name}";
            })));
            sb.AppendLine(");");
            sb.AppendLine("    }");
        }
        else
        {
            sb.Append($"        => {awaitKeyword}Inner.{member.Name}(");
            sb.Append(string.Join(", ", member.Parameters.Select(p =>
            {
                var refKind = p.RefKind switch
                {
                    RefKind.Ref => "ref ",
                    RefKind.Out => "out ",
                    RefKind.In => "in ",
                    _ => ""
                };
                return $"{refKind}{p.Name}";
            })));
            sb.AppendLine(");");
        }

        sb.AppendLine();
    }

    private void GenerateForwardingProperty(StringBuilder sb, MemberInfo member, DecoratorConfig config)
    {
        sb.AppendLine($"    /// <summary>Forwards to Inner.{member.Name}.</summary>");
        sb.Append($"    public virtual {member.ReturnType} {member.Name}");

        if (member.HasGetter && member.HasSetter)
        {
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine($"        get => Inner.{member.Name};");
            sb.AppendLine($"        set => Inner.{member.Name} = value;");
            sb.AppendLine("    }");
        }
        else if (member.HasGetter)
        {
            sb.AppendLine($" => Inner.{member.Name};");
        }
        else if (member.HasSetter)
        {
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine($"        set => Inner.{member.Name} = value;");
            sb.AppendLine("    }");
        }

        sb.AppendLine();
    }

    private void GenerateCompositionHelpers(StringBuilder sb, ContractInfo contractInfo, DecoratorConfig config)
    {
        var contractFullName = contractInfo.ContractSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        sb.AppendLine($"/// <summary>Composition helpers for {contractInfo.ContractName} decorators.</summary>");
        sb.AppendLine($"public static partial class {config.HelpersTypeName}");
        sb.AppendLine("{");
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Composes multiple decorators in order.");
        sb.AppendLine($"    /// Decorators are applied in array order: decorators[0] is the outermost decorator,");
        sb.AppendLine($"    /// decorators[^1] is the innermost decorator (closest to the inner instance).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"inner\">The inner instance to wrap.</param>");
        sb.AppendLine($"    /// <param name=\"decorators\">Factory functions that create decorators.</param>");
        sb.AppendLine($"    /// <returns>The fully decorated instance.</returns>");
        sb.AppendLine($"    public static {contractFullName} Compose(");
        sb.AppendLine($"        {contractFullName} inner,");
        sb.AppendLine($"        params System.Func<{contractFullName}, {contractFullName}>[] decorators)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (inner == null) throw new System.ArgumentNullException(nameof(inner));");
        sb.AppendLine("        if (decorators == null) throw new System.ArgumentNullException(nameof(decorators));");
        sb.AppendLine();
        sb.AppendLine("        var current = inner;");
        sb.AppendLine("        // Apply decorators in reverse order so first decorator is outermost");
        sb.AppendLine("        for (int i = decorators.Length - 1; i >= 0; i--)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (decorators[i] != null)");
        sb.AppendLine("                current = decorators[i](current);");
        sb.AppendLine("        }");
        sb.AppendLine("        return current;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    // Helper classes
    private class DecoratorConfig
    {
        public string ContractName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string BaseTypeName { get; set; } = "";
        public string HelpersTypeName { get; set; } = "";
        public int Composition { get; set; } = 1; // HelpersOnly
        public bool GenerateAsync { get; set; }
        public bool ForceAsync { get; set; }
    }

    private class ContractInfo
    {
        public INamedTypeSymbol ContractSymbol { get; set; } = null!;
        public string ContractName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public bool IsInterface { get; set; }
        public bool IsAbstractClass { get; set; }
        public List<MemberInfo> Members { get; set; } = new();
        public bool HasAsyncMembers { get; set; }
    }

    private class MemberInfo
    {
        public string Name { get; set; } = "";
        public MemberType MemberType { get; set; }
        public string ReturnType { get; set; } = "";
        public bool IsAsync { get; set; }
        public bool IsVoid { get; set; }
        public List<ParameterInfo> Parameters { get; set; } = new();
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
    }

    private class ParameterInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public bool HasDefaultValue { get; set; }
        public string? DefaultValue { get; set; }
        public RefKind RefKind { get; set; }
    }

    private enum MemberType
    {
        Method,
        Property
    }
}
