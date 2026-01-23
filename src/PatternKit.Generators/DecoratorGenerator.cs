using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PatternKit.Generators.Decorator;
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
    // Symbol display format that preserves nullable annotations
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    // Diagnostic IDs
    private const string DiagIdUnsupportedTargetType = "PKDEC001";
    private const string DiagIdNestedType = "PKDEC006";
    private const string DiagIdUnsupportedMember = "PKDEC002";
    private const string DiagIdNameConflict = "PKDEC003";
    private const string DiagIdInaccessibleMember = "PKDEC004";
    private const string DiagIdGenericContract = "PKDEC005";

    private static readonly DiagnosticDescriptor UnsupportedTargetTypeDescriptor = new(
        id: DiagIdUnsupportedTargetType,
        title: "Unsupported target type for decorator generation",
        messageFormat: "Type '{0}' cannot be used as a decorator contract. Only interfaces and abstract classes are supported.",
        category: "PatternKit.Generators.Decorator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedMemberDescriptor = new(
        id: DiagIdUnsupportedMember,
        title: "Unsupported member kind for decorator generation",
        messageFormat: "Member '{0}' of kind '{1}' is not supported in decorator generation (v1). Only methods and properties are supported.",
        category: "PatternKit.Generators.Decorator",
        defaultSeverity: DiagnosticSeverity.Error,
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
        messageFormat: "Member '{0}' cannot be forwarded by the generated decorator. Only members accessible from the decorator type (public, internal, or protected internal) can be forwarded; purely protected or private protected members on the inner type are not accessible.",
        category: "PatternKit.Generators.Decorator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor GenericContractDescriptor = new(
        id: DiagIdGenericContract,
        title: "Generic contracts are not supported for decorator generation",
        messageFormat: "Generic type '{0}' cannot be used as a decorator contract. Generic contracts are not supported in v1.",
        category: "PatternKit.Generators.Decorator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NestedTypeDescriptor = new(
        id: DiagIdNestedType,
        title: "Nested types are not supported for decorator generation",
        messageFormat: "Nested type '{0}' cannot be used as a decorator contract. Only top-level types are supported.",
        category: "PatternKit.Generators.Decorator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all types (interfaces, abstract classes, or records) marked with [GenerateDecorator]
        var decoratorContracts = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Decorator.GenerateDecoratorAttribute",
            predicate: static (node, _) => node is InterfaceDeclarationSyntax or ClassDeclarationSyntax or RecordDeclarationSyntax,
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
        // Check if type is interface or abstract class
        if (contractSymbol.TypeKind == TypeKind.Interface)
        {
            // Interfaces are supported
        }
        else if (contractSymbol.TypeKind == TypeKind.Class && contractSymbol.IsAbstract)
        {
            // Abstract classes are supported
        }
        else
        {
            // Not an interface or abstract class - unsupported target type
            context.ReportDiagnostic(Diagnostic.Create(
                UnsupportedTargetTypeDescriptor,
                node.GetLocation(),
                contractSymbol.Name));
            return;
        }

        // Check for nested types (not supported - accessibility issues)
        if (contractSymbol.ContainingType != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NestedTypeDescriptor,
                node.GetLocation(),
                contractSymbol.Name));
            return;
        }

        // Check for generic contracts (not supported in v1)
        if (contractSymbol.TypeParameters.Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                GenericContractDescriptor,
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

        // Check for helpers name conflict only if composition helpers will be generated
        if ((DecoratorCompositionMode)config.Composition == DecoratorCompositionMode.HelpersOnly &&
            HasNameConflict(contractSymbol, config.HelpersTypeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NameConflictDescriptor,
                node.GetLocation(),
                config.HelpersTypeName));
            return;
        }

        // Generate base decorator class
        var decoratorSource = GenerateBaseDecorator(contractInfo, config, context);
        if (!string.IsNullOrEmpty(decoratorSource))
        {
            // Use namespace + simple name to avoid collisions while keeping it readable
            var ns = contractSymbol.ContainingNamespace.IsGlobalNamespace 
                ? "" 
                : contractSymbol.ContainingNamespace.ToDisplayString().Replace(".", "_") + "_";
            var fileName = $"{ns}{contractSymbol.Name}.Decorator.g.cs";
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
                case nameof(GenerateDecoratorAttribute.BaseTypeName):
                    if (named.Value.Value is string baseTypeName && !string.IsNullOrWhiteSpace(baseTypeName))
                        config.BaseTypeName = baseTypeName;
                    break;
                case nameof(GenerateDecoratorAttribute.HelpersTypeName):
                    if (named.Value.Value is string helpersTypeName && !string.IsNullOrWhiteSpace(helpersTypeName))
                        config.HelpersTypeName = helpersTypeName;
                    break;
                case nameof(GenerateDecoratorAttribute.Composition):
                    config.Composition = (int)named.Value.Value!;
                    break;
                // GenerateAsync and ForceAsync are reserved for future use - not parsed
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
            // No members to forward - skip generation to avoid emitting invalid decorators
            // (Could be empty due to hasErrors=true in GetMembersForDecorator, or truly no members)
            return null;
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
        var hasErrors = false;

        // Get all members from the contract and its base types
        var allMembers = GetAllInterfaceMembers(contractSymbol);

        foreach (var member in allMembers)
        {
            // Check for ignore attribute - these will still be forwarded but marked as non-virtual
            var isIgnored = HasAttribute(member, "PatternKit.Generators.Decorator.DecoratorIgnoreAttribute");

            // Only process methods and properties
            if (member is IMethodSymbol method)
            {
                // Skip special methods (constructors, operators, property accessors, etc.)
                if (method.MethodKind != MethodKind.Ordinary)
                    continue;

                // Skip static methods; decorators only forward instance members
                if (method.IsStatic)
                    continue;

                // Generic methods are not supported in v1
                if (method.TypeParameters.Length > 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        method.Name,
                        "Generic method"));
                    hasErrors = true;
                    continue;
                }

                // For abstract classes, only include virtual or abstract methods
                if (contractInfo.IsAbstractClass && !method.IsVirtual && !method.IsAbstract)
                    continue;

                // Skip inaccessible methods
                if (!IsAccessibleForDecorator(method.DeclaredAccessibility))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InaccessibleMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        member.Name));
                    // PKDEC004 is a warning, not an error - don't set hasErrors
                    continue;
                }

                var isAsync = IsAsyncMethod(method);
                var baseReturnType = method.ReturnType.ToDisplayString(TypeFormat);
                var returnType = method.ReturnsByRef
                    ? "ref " + baseReturnType
                    : method.ReturnsByRefReadonly
                        ? "ref readonly " + baseReturnType
                        : baseReturnType;

                members.Add(new MemberInfo
                {
                    Name = method.Name,
                    MemberType = MemberType.Method,
                    ReturnType = returnType,
                    IsAsync = isAsync,
                    IsVoid = method.ReturnsVoid,
                    IsIgnored = isIgnored,
                    Accessibility = method.DeclaredAccessibility,
                    OriginalSymbol = method,
                    ReturnsByRef = method.ReturnsByRef,
                    ReturnsByRefReadonly = method.ReturnsByRefReadonly,
                    Parameters = method.Parameters.Select(p => new ParameterInfo
                    {
                        Name = p.Name,
                        Type = p.Type.ToDisplayString(TypeFormat),
                        HasDefaultValue = p.HasExplicitDefaultValue,
                        DefaultValue = p.HasExplicitDefaultValue ? FormatDefaultValue(p) : null,
                        RefKind = p.RefKind,
                        IsParams = p.IsParams,
                        IsThis = p.IsThis
                    }).ToList()
                });
            }
            else if (member is IPropertySymbol property)
            {
                // Indexer properties are not supported in v1
                if (property.IsIndexer)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        member.Name,
                        "Indexer"));
                    hasErrors = true;
                    continue;
                }

                // Skip static properties; decorators only forward instance members
                if (property.IsStatic)
                    continue;

                // For abstract classes, only include virtual or abstract properties
                if (contractInfo.IsAbstractClass && !property.IsVirtual && !property.IsAbstract)
                    continue;

                // Check accessor-level accessibility
                // Property may be public but have protected/private accessors
                bool getterAccessible = property.GetMethod == null || IsAccessibleForDecorator(property.GetMethod.DeclaredAccessibility);
                bool setterAccessible = property.SetMethod == null || IsAccessibleForDecorator(property.SetMethod.DeclaredAccessibility);
                
                if (!getterAccessible || !setterAccessible)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InaccessibleMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        member.Name));
                    // PKDEC004 is a warning, not an error - don't set hasErrors
                    continue;
                }

                // Skip inaccessible properties
                if (!IsAccessibleForDecorator(property.DeclaredAccessibility))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InaccessibleMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        member.Name));
                    // PKDEC004 is a warning, not an error - don't set hasErrors
                    continue;
                }

                // Properties with init-only setters are not supported
                // The decorator pattern is incompatible with init setters because
                // you cannot assign to init-only properties after object construction
                if (property.SetMethod?.IsInitOnly ?? false)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        property.Name,
                        "Init-only property"));
                    hasErrors = true;
                    continue;
                }

                var propInfo = new MemberInfo
                {
                    Name = property.Name,
                    MemberType = MemberType.Property,
                    ReturnType = property.Type.ToDisplayString(TypeFormat),
                    HasGetter = property.GetMethod is not null,
                    HasSetter = property.SetMethod is not null,
                    IsInitOnly = property.SetMethod?.IsInitOnly ?? false,
                    IsAsync = false,
                    IsIgnored = isIgnored,
                    Accessibility = property.DeclaredAccessibility,
                    GetterAccessibility = property.GetMethod?.DeclaredAccessibility ?? property.DeclaredAccessibility,
                    SetterAccessibility = property.SetMethod?.DeclaredAccessibility ?? property.DeclaredAccessibility,
                    OriginalSymbol = property
                };

                members.Add(propInfo);
            }
            else if (member is IEventSymbol)
            {
                // Events not supported in v1
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedMemberDescriptor,
                    member.Locations.FirstOrDefault() ?? Location.None,
                    member.Name,
                    "Event"));
                hasErrors = true;
            }
            else if (member is IFieldSymbol fieldSymbol && IsAccessibleForDecorator(fieldSymbol.DeclaredAccessibility))
            {
                // Fields are not supported; only report for forwardable API-surface members
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedMemberDescriptor,
                    member.Locations.FirstOrDefault() ?? Location.None,
                    member.Name,
                    member.Kind.ToString()));
                hasErrors = true;
            }
            else if (member is INamedTypeSymbol typeSymbol && IsAccessibleForDecorator(typeSymbol.DeclaredAccessibility))
            {
                // Nested types are not supported; only report for forwardable API-surface members
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedMemberDescriptor,
                    member.Locations.FirstOrDefault() ?? Location.None,
                    member.Name,
                    member.Kind.ToString()));
                hasErrors = true;
            }
        }

        // If any errors were reported, return empty list to skip generation
        // This prevents generating incomplete decorator bases that won't compile
        if (hasErrors)
        {
            return new List<MemberInfo>();
        }

        // Sort members for deterministic ordering by kind, name, and signature
        return members.OrderBy(m => GetMemberSortKey(m), StringComparer.Ordinal).ToList();
    }

    private static string GetMemberSortKey(MemberInfo member)
    {
        // Create a stable sort key: kind + name + parameter signature
        var sb = new StringBuilder();
        sb.Append((int)member.MemberType); // 0 for Method, 1 for Property
        sb.Append('_');
        sb.Append(member.Name);
        
        if (member.MemberType == MemberType.Method && member.Parameters.Count > 0)
        {
            sb.Append('(');
            for (int i = 0; i < member.Parameters.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var param = member.Parameters[i];
                sb.Append(param.RefKind switch
                {
                    RefKind.Ref => "ref ",
                    RefKind.Out => "out ",
                    RefKind.In => "in ",
                    _ => ""
                });
                sb.Append(param.Type);
            }
            sb.Append(')');
        }
        
        return sb.ToString();
    }

    private IEnumerable<ISymbol> GetAllInterfaceMembers(INamedTypeSymbol type)
    {
        var members = new List<ISymbol>();
        var seenSignatures = new HashSet<string>();

        void AddMember(ISymbol symbol)
        {
            // Create a signature key for deduplication
            var signature = GetMemberSignature(symbol);
            if (seenSignatures.Add(signature))
            {
                members.Add(symbol);
            }
        }

        if (type.TypeKind == TypeKind.Interface)
        {
            // For interfaces, collect from this interface and all base interfaces
            foreach (var member in type.GetMembers())
            {
                AddMember(member);
            }
            foreach (var baseInterface in type.AllInterfaces)
            {
                foreach (var member in baseInterface.GetMembers())
                {
                    AddMember(member);
                }
            }
        }
        else if (type.TypeKind == TypeKind.Class && type.IsAbstract)
        {
            // For abstract classes, collect members from this type and all base types
            // (we'll filter virtual/abstract later). We walk the BaseType chain to ensure
            // inherited virtual/abstract members are also considered part of the contract.
            INamedTypeSymbol? current = type;
            while (current != null && current.SpecialType != SpecialType.System_Object)
            {
                foreach (var member in current.GetMembers())
                {
                    AddMember(member);
                }
                current = current.BaseType;
            }
        }

        return members;
    }

    private static string GetMemberSignature(ISymbol symbol)
    {
        var sb = new StringBuilder();
        sb.Append(symbol.Kind);
        sb.Append('_');
        sb.Append(symbol.Name);
        
        if (symbol is IMethodSymbol method)
        {
            sb.Append('(');
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var param = method.Parameters[i];
                sb.Append(param.RefKind switch
                {
                    RefKind.Ref => "ref ",
                    RefKind.Out => "out ",
                    RefKind.In => "in ",
                    _ => ""
                });
                sb.Append(param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
            sb.Append(')');
            sb.Append(':');
            sb.Append(method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
        else if (symbol is IPropertySymbol property)
        {
            sb.Append(':');
            sb.Append(property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
        else if (symbol is IEventSymbol eventSymbol)
        {
            sb.Append(':');
            sb.Append(eventSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
        
        return sb.ToString();
    }

    private static bool IsAsyncMethod(IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        var typeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return typeName.StartsWith("global::System.Threading.Tasks.Task") ||
               typeName.StartsWith("global::System.Threading.Tasks.ValueTask");
    }

    private static bool CanTypeAcceptNull(ITypeSymbol type)
    {
        if (type is null)
            return false;

        // All reference types can accept null
        if (type.IsReferenceType)
            return true;

        // Nullable reference types / annotated types can accept null
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
            return true;

        // Type parameters without a value type constraint can accept null
        if (type is ITypeParameterSymbol typeParam)
            return !typeParam.HasValueTypeConstraint;

        // Nullable<T> value types can accept null
        if (type is INamedTypeSymbol named &&
            named.IsGenericType &&
            named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        return false;
    }

    private static string FormatDefaultValue(IParameterSymbol param)
    {
        if (param.ExplicitDefaultValue is null)
        {
            // Preserve 'null' for types that can accept null (including Nullable<T> and unconstrained type parameters)
            if (CanTypeAcceptNull(param.Type))
                return "null";

            // For non-nullable value types (structs), use 'default'
            if (param.Type.IsValueType)
                return "default";

            // Fallback to 'null' for any remaining cases
            return "null";
        }

        if (param.Type.TypeKind == TypeKind.Enum && param.Type is INamedTypeSymbol enumType)
        {
            // Try to resolve the enum field name corresponding to the default value
            var enumField = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, param.ExplicitDefaultValue));
            
            if (enumField != null)
            {
                return $"{enumType.ToDisplayString(TypeFormat)}.{enumField.Name}";
            }
            
            // Fallback: cast the numeric value
            return $"({enumType.ToDisplayString(TypeFormat)}){param.ExplicitDefaultValue}";
        }

        // Use Roslyn's culture-invariant literal formatting for all other types
        return SymbolDisplay.FormatPrimitive(param.ExplicitDefaultValue, quoteStrings: true, useHexadecimalNumbers: false);
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == attributeName);
    }

    private static bool IsAccessibleForDecorator(Accessibility accessibility)
    {
        // Allow public/internal/protected internal
        // Pure protected can't be forwarded because Inner.Member() isn't accessible through the base type reference
        return accessibility == Accessibility.Public || 
               accessibility == Accessibility.Internal ||
               accessibility == Accessibility.ProtectedOrInternal;
    }

    private static string GetAccessibilityKeyword(Accessibility accessibility)
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

    private static bool HasNameConflict(INamedTypeSymbol contractSymbol, string generatedName)
    {
        // Check if there's already a type with the generated name in the same namespace
        var containingNamespace = contractSymbol.ContainingNamespace;
        var existingTypes = containingNamespace.GetTypeMembers(generatedName);
        return existingTypes.Length > 0;
    }

    private string GenerateBaseDecorator(ContractInfo contractInfo, DecoratorConfig config, SourceProductionContext _)
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
        var contractFullName = contractInfo.ContractSymbol.ToDisplayString(TypeFormat);
        var accessibility = GetAccessibilityKeyword(contractInfo.ContractSymbol.DeclaredAccessibility);
        sb.AppendLine($"/// <summary>Base decorator for {contractInfo.ContractName}. All members forward to Inner.</summary>");
        sb.AppendLine($"{accessibility} abstract partial class {config.BaseTypeName} : {contractFullName}");
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
                GenerateForwardingMethod(sb, member, contractInfo, config);
            }
            else if (member.MemberType == MemberType.Property)
            {
                GenerateForwardingProperty(sb, member, contractInfo, config);
            }
        }

        sb.AppendLine("}");

        // Generate composition helpers if requested
        if ((DecoratorCompositionMode)config.Composition == DecoratorCompositionMode.HelpersOnly)
        {
            sb.AppendLine();
            GenerateCompositionHelpers(sb, contractInfo, config);
        }

        return sb.ToString();
    }

    private void GenerateForwardingMethod(StringBuilder sb, MemberInfo member, ContractInfo contractInfo, DecoratorConfig _)
    {
        // For async methods, use direct forwarding (return Inner.X()) instead of async/await
        // to avoid unnecessary state machine allocation
        
        // Determine the modifier keyword
        string modifierKeyword = contractInfo.IsAbstractClass
            // For abstract class contracts, always override the contract member.
            // If the member is ignored, seal the override to prevent further overriding
            // while still satisfying the abstract/virtual contract.
            ? (member.IsIgnored ? "sealed override " : "override ")
            // For non-abstract contracts (e.g., interfaces), only non-ignored members are virtual.
            : (member.IsIgnored ? "" : "virtual ");
        
        // Preserve the original member's accessibility to avoid widening on overrides
        var accessibilityKeyword = GetAccessibilityKeyword(member.Accessibility);
        
        sb.AppendLine($"    /// <summary>Forwards to Inner.{member.Name}.</summary>");
        sb.Append($"    {accessibilityKeyword} {modifierKeyword}{member.ReturnType} {member.Name}(");

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
            // Preserve additional parameter modifiers (params, this)
            var paramsModifier = p.IsParams ? "params " : "";
            var thisModifier = p.IsThis ? "this " : "";
            var defaultVal = p.HasDefaultValue ? $" = {p.DefaultValue}" : "";
            return $"{thisModifier}{paramsModifier}{refKind}{p.Type} {p.Name}{defaultVal}";
        }));

        sb.Append(paramList);
        sb.AppendLine(")");

        // Generate method body - use direct forwarding for all methods including async
        if (member.IsVoid)
        {
            sb.AppendLine("    {");
            sb.Append($"        Inner.{member.Name}(");
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
            // For ref returns, we need to put "ref" before the invocation
            var refModifier = member.ReturnsByRef || member.ReturnsByRefReadonly ? "ref " : "";
            sb.Append($"        => {refModifier}Inner.{member.Name}(");
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

    private void GenerateForwardingProperty(StringBuilder sb, MemberInfo member, ContractInfo contractInfo, DecoratorConfig _)
    {
        // Determine the modifier keyword
        string modifierKeyword = contractInfo.IsAbstractClass
            ? (member.IsIgnored ? "sealed override " : "override ")
            : (member.IsIgnored ? "" : "virtual ");
        
        // Preserve the original member's accessibility
        var accessibilityKeyword = GetAccessibilityKeyword(member.Accessibility);
        
        sb.AppendLine($"    /// <summary>Forwards to Inner.{member.Name}.</summary>");
        sb.Append($"    {accessibilityKeyword} {modifierKeyword}{member.ReturnType} {member.Name}");

        // Determine accessor-level modifiers
        string getterModifier = "";
        string setterModifier = "";
        
        if (contractInfo.IsAbstractClass)
        {
            // For abstract classes, apply accessor modifiers when accessibility differs from property
            if (member.HasGetter && member.GetterAccessibility != member.Accessibility)
            {
                getterModifier = GetAccessibilityKeyword(member.GetterAccessibility) + " ";
            }
            if (member.HasSetter && member.SetterAccessibility != member.Accessibility)
            {
                setterModifier = GetAccessibilityKeyword(member.SetterAccessibility) + " ";
            }
        }

        if (member.HasGetter && member.HasSetter)
        {
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine($"        {getterModifier}get => Inner.{member.Name};");
            // Note: Init setters cannot be properly forwarded in decorators
            // Always use 'set' for forwarding since we can't assign to init-only properties
            sb.AppendLine($"        {setterModifier}set => Inner.{member.Name} = value;");
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
            // Always use 'set' for forwarding
            sb.AppendLine($"        {setterModifier}set => Inner.{member.Name} = value;");
            sb.AppendLine("    }");
        }

        sb.AppendLine();
    }

    private void GenerateCompositionHelpers(StringBuilder sb, ContractInfo contractInfo, DecoratorConfig config)
    {
        var contractFullName = contractInfo.ContractSymbol.ToDisplayString(TypeFormat);
        var accessibility = GetAccessibilityKeyword(contractInfo.ContractSymbol.DeclaredAccessibility);

        sb.AppendLine($"/// <summary>Composition helpers for {contractInfo.ContractName} decorators.</summary>");
        sb.AppendLine($"{accessibility} static partial class {config.HelpersTypeName}");
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
        // GenerateAsync and ForceAsync are reserved for future use - not stored in config
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
        public bool IsIgnored { get; set; }
        public List<ParameterInfo> Parameters { get; set; } = new();
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
        public bool IsInitOnly { get; set; }
        public Accessibility Accessibility { get; set; } = Accessibility.Public;
        public Accessibility GetterAccessibility { get; set; } = Accessibility.Public;
        public Accessibility SetterAccessibility { get; set; } = Accessibility.Public;
        public ISymbol? OriginalSymbol { get; set; }
        public bool ReturnsByRef { get; set; }
        public bool ReturnsByRefReadonly { get; set; }
    }

    private class ParameterInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public bool HasDefaultValue { get; set; }
        public string? DefaultValue { get; set; }
        public RefKind RefKind { get; set; }
        public bool IsParams { get; set; }
        public bool IsThis { get; set; }
    }

    private enum MemberType
    {
        Method,
        Property
    }
}
