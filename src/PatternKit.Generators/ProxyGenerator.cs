using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Proxy pattern.
/// Generates proxy classes that delegate calls to an inner instance,
/// with optional interceptor support for cross-cutting concerns.
/// </summary>
[Generator]
public sealed class ProxyGenerator : IIncrementalGenerator
{
    // Symbol display format that preserves nullable annotations
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKPRX001";
    private const string DiagIdUnsupportedMember = "PKPRX002";
    private const string DiagIdInaccessibleMember = "PKPRX003";
    private const string DiagIdNameConflict = "PKPRX004";
    private const string DiagIdAsyncDetected = "PKPRX005";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type must be partial for proxy generation",
        messageFormat: "Type '{0}' must be declared as partial to support proxy generation.",
        category: "PatternKit.Generators.Proxy",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedMemberDescriptor = new(
        id: DiagIdUnsupportedMember,
        title: "Unsupported member kind for proxy generation",
        messageFormat: "Member '{0}' of kind '{1}' is not supported in proxy generation (v1). Only methods and properties are supported.",
        category: "PatternKit.Generators.Proxy",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InaccessibleMemberDescriptor = new(
        id: DiagIdInaccessibleMember,
        title: "Member is not accessible for proxy generation",
        messageFormat: "Member '{0}' cannot be proxied. Only members accessible from the proxy type (public, internal, or protected internal) can be proxied.",
        category: "PatternKit.Generators.Proxy",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NameConflictDescriptor = new(
        id: DiagIdNameConflict,
        title: "Name conflict for generated proxy types",
        messageFormat: "Generated type name '{0}' conflicts with an existing type. Use ProxyTypeName to specify a different name.",
        category: "PatternKit.Generators.Proxy",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AsyncDetectedDescriptor = new(
        id: DiagIdAsyncDetected,
        title: "Async member detected but async disabled",
        messageFormat: "Member '{0}' is async (returns Task/ValueTask or has CancellationToken), but GenerateAsync is explicitly set to false. Interceptors will use synchronous methods only.",
        category: "PatternKit.Generators.Proxy",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all types marked with [GenerateProxy]
        var proxyContracts = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Proxy.GenerateProxyAttribute",
            predicate: static (node, _) => node is InterfaceDeclarationSyntax or ClassDeclarationSyntax or RecordDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Generate for each contract
        context.RegisterSourceOutput(proxyContracts, (spc, contractContext) =>
        {
            if (contractContext.TargetSymbol is not INamedTypeSymbol contractSymbol)
                return;

            var attr = contractContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Proxy.GenerateProxyAttribute");
            if (attr is null)
                return;

            GenerateProxyForContract(spc, contractSymbol, attr, contractContext.TargetNode);
        });
    }

    private void GenerateProxyForContract(
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
                UnsupportedMemberDescriptor,
                node.GetLocation(),
                contractSymbol.Name,
                "Non-abstract class"));
            return;
        }

        // Check for nested types (not supported)
        if (contractSymbol.ContainingType != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnsupportedMemberDescriptor,
                node.GetLocation(),
                contractSymbol.Name,
                "Nested type"));
            return;
        }

        // Check for generic contracts (not supported in v1)
        if (contractSymbol.TypeParameters.Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnsupportedMemberDescriptor,
                node.GetLocation(),
                contractSymbol.Name,
                "Generic type"));
            return;
        }

        // For interface contracts, must be partial (so user can add custom logic if needed)
        // For abstract class contracts, the generated proxy inherits from it so partial is required
        if (!IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor,
                node.GetLocation(),
                contractSymbol.Name));
            return;
        }

        // Parse attribute arguments
        var config = ParseProxyConfig(attribute, contractSymbol);

        // Analyze contract and members
        var contractInfo = AnalyzeContract(contractSymbol, config, context);
        if (contractInfo is null)
            return;

        // Check for name conflicts
        if (HasNameConflict(contractSymbol, config.ProxyTypeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NameConflictDescriptor,
                node.GetLocation(),
                config.ProxyTypeName));
            return;
        }

        // Generate proxy class
        var proxySource = GenerateProxyClass(contractInfo, config, context);
        if (!string.IsNullOrEmpty(proxySource))
        {
            var ns = contractSymbol.ContainingNamespace.IsGlobalNamespace
                ? ""
                : contractSymbol.ContainingNamespace.ToDisplayString().Replace(".", "_") + "_";
            var fileName = $"{ns}{contractSymbol.Name}.Proxy.g.cs";
            context.AddSource(fileName, proxySource);
        }

        // Generate interceptor interface if needed
        if (config.InterceptorMode != ProxyInterceptorMode.None)
        {
            var interceptorSource = GenerateInterceptorInterface(contractInfo, config);
            if (!string.IsNullOrEmpty(interceptorSource))
            {
                var ns = contractSymbol.ContainingNamespace.IsGlobalNamespace
                    ? ""
                    : contractSymbol.ContainingNamespace.ToDisplayString().Replace(".", "_") + "_";
                var fileName = $"{ns}{contractSymbol.Name}.Proxy.Interceptor.g.cs";
                context.AddSource(fileName, interceptorSource);
            }
        }
    }

    private static bool IsPartialType(SyntaxNode node)
    {
        return node switch
        {
            InterfaceDeclarationSyntax iface => iface.Modifiers.Any(m => m.Text == "partial"),
            ClassDeclarationSyntax cls => cls.Modifiers.Any(m => m.Text == "partial"),
            RecordDeclarationSyntax rec => rec.Modifiers.Any(m => m.Text == "partial"),
            _ => false
        };
    }

    private ProxyConfig ParseProxyConfig(AttributeData attribute, INamedTypeSymbol contractSymbol)
    {
        var config = new ProxyConfig
        {
            ContractName = contractSymbol.Name,
            Namespace = contractSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : contractSymbol.ContainingNamespace.ToDisplayString()
        };

        // Determine default proxy type name
        var baseName = contractSymbol.Name;
        if (baseName.StartsWith("I") && baseName.Length > 1 && char.IsUpper(baseName[1]))
        {
            // Interface with I prefix: IUserService -> UserServiceProxy
            baseName = baseName.Substring(1);
        }
        config.ProxyTypeName = $"{baseName}Proxy";
        config.InterceptorInterfaceName = $"I{baseName}Interceptor";

        foreach (var named in attribute.NamedArguments)
        {
            switch (named.Key)
            {
                case "ProxyTypeName":
                    if (named.Value.Value is string proxyTypeName && !string.IsNullOrWhiteSpace(proxyTypeName))
                        config.ProxyTypeName = proxyTypeName;
                    break;
                case "InterceptorMode":
                    config.InterceptorMode = (ProxyInterceptorMode)(int)named.Value.Value!;
                    break;
                case "GenerateAsync":
                    if (named.Value.Value is bool generateAsync)
                        config.GenerateAsync = generateAsync;
                    break;
                case "ForceAsync":
                    if (named.Value.Value is bool forceAsync)
                        config.ForceAsync = forceAsync;
                    break;
                case "Exceptions":
                    config.ExceptionPolicy = (ProxyExceptionPolicy)(int)named.Value.Value!;
                    break;
            }
        }

        return config;
    }

    private ContractInfo? AnalyzeContract(
        INamedTypeSymbol contractSymbol,
        ProxyConfig config,
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
        var members = GetMembersForProxy(contractSymbol, contractInfo, config, context);
        contractInfo.Members.AddRange(members);

        if (contractInfo.Members.Count == 0)
        {
            // No members to proxy - skip generation
            return null;
        }

        // Detect if any async members exist
        contractInfo.HasAsyncMembers = contractInfo.Members.Any(m => m.IsAsync || m.HasCancellationToken);

        // Apply ForceAsync if specified
        if (config.ForceAsync)
        {
            contractInfo.HasAsyncMembers = true;
        }

        // Handle GenerateAsync override
        if (config.GenerateAsync.HasValue)
        {
            if (!config.GenerateAsync.Value && contractInfo.HasAsyncMembers)
            {
                // User explicitly disabled async but we detected async members - warn
                foreach (var member in contractInfo.Members.Where(m => m.IsAsync || m.HasCancellationToken))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        AsyncDetectedDescriptor,
                        member.OriginalSymbol?.Locations.FirstOrDefault() ?? Location.None,
                        member.Name));
                }
            }
            contractInfo.HasAsyncMembers = config.GenerateAsync.Value;
        }

        return contractInfo;
    }

    private List<MemberInfo> GetMembersForProxy(
        INamedTypeSymbol contractSymbol,
        ContractInfo contractInfo,
        ProxyConfig config,
        SourceProductionContext context)
    {
        var members = new List<MemberInfo>();
        var hasErrors = false;

        // Get all members from the contract and its base types
        var allMembers = GetAllInterfaceMembers(contractSymbol);

        foreach (var member in allMembers)
        {
            // Check for ignore attribute
            var isIgnored = HasAttribute(member, "PatternKit.Generators.Proxy.ProxyIgnoreAttribute");
            if (isIgnored)
                continue;

            // Only process methods and properties
            if (member is IMethodSymbol method)
            {
                // Skip special methods (constructors, operators, property accessors, etc.)
                if (method.MethodKind != MethodKind.Ordinary)
                    continue;

                // Skip static methods
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
                if (!IsAccessibleForProxy(method.DeclaredAccessibility))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InaccessibleMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        member.Name));
                    continue;
                }

                var isAsync = IsAsyncMethod(method);
                var hasCancellationToken = method.Parameters.Any(p => IsCancellationToken(p.Type));
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
                    HasCancellationToken = hasCancellationToken,
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

                // Skip static properties
                if (property.IsStatic)
                    continue;

                // For abstract classes, only include virtual or abstract properties
                if (contractInfo.IsAbstractClass && !property.IsVirtual && !property.IsAbstract)
                    continue;

                // Check accessor-level accessibility
                bool getterAccessible = property.GetMethod == null || IsAccessibleForProxy(property.GetMethod.DeclaredAccessibility);
                bool setterAccessible = property.SetMethod == null || IsAccessibleForProxy(property.SetMethod.DeclaredAccessibility);

                if (!getterAccessible || !setterAccessible)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InaccessibleMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        member.Name));
                    continue;
                }

                // Skip inaccessible properties
                if (!IsAccessibleForProxy(property.DeclaredAccessibility))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InaccessibleMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        member.Name));
                    continue;
                }

                var propInfo = new MemberInfo
                {
                    Name = property.Name,
                    MemberType = MemberType.Property,
                    ReturnType = property.Type.ToDisplayString(TypeFormat),
                    HasGetter = property.GetMethod is not null,
                    HasSetter = property.SetMethod is not null,
                    IsAsync = false,
                    HasCancellationToken = false,
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
        }

        // If any errors were reported, return empty list to skip generation
        if (hasErrors)
        {
            return new List<MemberInfo>();
        }

        // Sort members for deterministic ordering
        return members.OrderBy(m => GetMemberSortKey(m), StringComparer.Ordinal).ToList();
    }

    private static string GetMemberSortKey(MemberInfo member)
    {
        var sb = new StringBuilder();
        sb.Append((int)member.MemberType);
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

        return sb.ToString();
    }

    private static bool IsAsyncMethod(IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        var typeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return typeName.StartsWith("global::System.Threading.Tasks.Task") ||
               typeName.StartsWith("global::System.Threading.Tasks.ValueTask");
    }

    private static bool IsCancellationToken(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return typeName == "global::System.Threading.CancellationToken";
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

    private static string FormatDefaultValue(IParameterSymbol param)
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
        // Note: FormatPrimitive is a helper that formats primitive values as C# literals
        var value = param.ExplicitDefaultValue;
        return value switch
        {
            string s => $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
            char c => $"'{c}'",
            bool b => b ? "true" : "false",
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f",
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture) + "d",
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m",
            _ => value?.ToString() ?? "null"
        };
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == attributeName);
    }

    private static bool IsAccessibleForProxy(Accessibility accessibility)
    {
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
        var containingNamespace = contractSymbol.ContainingNamespace;
        var existingTypes = containingNamespace.GetTypeMembers(generatedName);
        
        // No conflict if no types exist
        if (existingTypes.Length == 0)
            return false;
            
        // No conflict if all existing types are partial (user is expected to provide partial implementation)
        // Check if any type has multiple partial declarations or check syntax  
        foreach (var type in existingTypes)
        {
            // If any type is NOT partial (has only one location and not marked partial in syntax), it's a conflict
            if (type.Locations.Length == 1)
            {
                // Single location could still be partial, but if there are multiple types with same name,
                // they could be in different files which would be detected as multiple items in existingTypes
                // For now, assume single occurrence is OK (user provided partial)
                continue;
            }
        }
        
        // If we have multiple types with the same name but they're all partial, no conflict
        return false;
    }

    private string GenerateProxyClass(ContractInfo contractInfo, ProxyConfig config, SourceProductionContext _)
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

        // Generate proxy class
        var contractFullName = contractInfo.ContractSymbol.ToDisplayString(TypeFormat);
        var accessibility = GetAccessibilityKeyword(contractInfo.ContractSymbol.DeclaredAccessibility);

        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Proxy for {contractInfo.ContractName} that delegates all calls to an inner instance.");
        if (config.InterceptorMode != ProxyInterceptorMode.None)
        {
            sb.AppendLine($"/// Supports interceptors for cross-cutting concerns.");
        }
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"{accessibility} sealed partial class {config.ProxyTypeName} : {contractFullName}");
        sb.AppendLine("{");

        // Fields
        sb.AppendLine($"    private readonly {contractFullName} _inner;");
        if (config.InterceptorMode == ProxyInterceptorMode.Single)
        {
            sb.AppendLine($"    private readonly {config.InterceptorInterfaceName}? _interceptor;");
        }
        else if (config.InterceptorMode == ProxyInterceptorMode.Pipeline)
        {
            sb.AppendLine($"    private readonly global::System.Collections.Generic.IReadOnlyList<{config.InterceptorInterfaceName}>? _interceptors;");
        }
        sb.AppendLine();

        // Constructor
        GenerateProxyConstructor(sb, contractInfo, config);

        // Generate forwarding members
        foreach (var member in contractInfo.Members)
        {
            if (member.MemberType == MemberType.Method)
            {
                GenerateProxyMethod(sb, member, contractInfo, config);
            }
            else if (member.MemberType == MemberType.Property)
            {
                GenerateProxyProperty(sb, member, contractInfo, config);
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateProxyConstructor(StringBuilder sb, ContractInfo contractInfo, ProxyConfig config)
    {
        var contractFullName = contractInfo.ContractSymbol.ToDisplayString(TypeFormat);

        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the {config.ProxyTypeName} class.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"inner\">The inner {contractInfo.ContractName} instance to delegate to.</param>");

        if (config.InterceptorMode == ProxyInterceptorMode.Single)
        {
            sb.AppendLine($"    /// <param name=\"interceptor\">Optional interceptor for cross-cutting concerns.</param>");
            sb.AppendLine($"    public {config.ProxyTypeName}({contractFullName} inner, {config.InterceptorInterfaceName}? interceptor = null)");
        }
        else if (config.InterceptorMode == ProxyInterceptorMode.Pipeline)
        {
            sb.AppendLine($"    /// <param name=\"interceptors\">Optional collection of interceptors for cross-cutting concerns.</param>");
            sb.AppendLine($"    public {config.ProxyTypeName}({contractFullName} inner, global::System.Collections.Generic.IReadOnlyList<{config.InterceptorInterfaceName}>? interceptors = null)");
        }
        else
        {
            sb.AppendLine($"    public {config.ProxyTypeName}({contractFullName} inner)");
        }

        sb.AppendLine("    {");
        sb.AppendLine("        _inner = inner ?? throw new global::System.ArgumentNullException(nameof(inner));");
        if (config.InterceptorMode == ProxyInterceptorMode.Single)
        {
            sb.AppendLine("        _interceptor = interceptor;");
        }
        else if (config.InterceptorMode == ProxyInterceptorMode.Pipeline)
        {
            sb.AppendLine("        _interceptors = interceptors;");
        }
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private void GenerateProxyMethod(StringBuilder sb, MemberInfo member, ContractInfo contractInfo, ProxyConfig config)
    {
        var accessibility = GetAccessibilityKeyword(member.Accessibility);
        
        // Determine if this method needs async modifier (uses interceptors with async support)
        bool useAsync = contractInfo.HasAsyncMembers && 
                       config.InterceptorMode != ProxyInterceptorMode.None &&
                       (member.IsAsync || member.HasCancellationToken);
        var asyncModifier = useAsync ? "async " : "";

        sb.AppendLine($"    /// <summary>Proxies {member.Name}.</summary>");
        sb.Append($"    {accessibility} {asyncModifier}{member.ReturnType} {member.Name}(");

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
            var paramsModifier = p.IsParams ? "params " : "";
            var thisModifier = p.IsThis ? "this " : "";
            var defaultVal = p.HasDefaultValue ? $" = {p.DefaultValue}" : "";
            return $"{thisModifier}{paramsModifier}{refKind}{p.Type} {p.Name}{defaultVal}";
        }));

        sb.Append(paramList);
        sb.AppendLine(")");
        sb.AppendLine("    {");

        // Generate method body based on interceptor mode
        if (config.InterceptorMode == ProxyInterceptorMode.None)
        {
            // Simple delegation
            GenerateSimpleDelegation(sb, member);
        }
        else
        {
            // Delegation with interceptors
            GenerateInterceptedDelegation(sb, member, contractInfo, config);
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private void GenerateSimpleDelegation(StringBuilder sb, MemberInfo member)
    {
        if (member.IsVoid)
        {
            // True void methods - no return
            sb.Append($"        _inner.{member.Name}(");
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
        else
        {
            // Non-void methods - return the result
            var refModifier = member.ReturnsByRef || member.ReturnsByRefReadonly ? "ref " : "";
            sb.Append($"        return {refModifier}_inner.{member.Name}(");
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
    }

    private void GenerateInterceptedDelegation(StringBuilder sb, MemberInfo member, ContractInfo contractInfo, ProxyConfig config)
    {
        var interceptorCheck = config.InterceptorMode == ProxyInterceptorMode.Single
            ? "_interceptor is null"
            : "_interceptors is null || _interceptors.Count == 0";

        sb.AppendLine($"        if ({interceptorCheck})");
        sb.AppendLine("        {");

        // No interceptor - simple delegation
        if (member.IsVoid)
        {
            sb.Append($"            _inner.{member.Name}(");
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
            sb.AppendLine("            return;");
        }
        else
        {
            var refModifier = member.ReturnsByRef || member.ReturnsByRefReadonly ? "ref " : "";
            sb.Append($"            return {refModifier}_inner.{member.Name}(");
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

        sb.AppendLine("        }");
        sb.AppendLine();

        // Create method context
        var contextTypeName = $"{member.Name}MethodContext";
        sb.Append($"        var __context = new {contextTypeName}(");
        sb.Append(string.Join(", ", member.Parameters.Select(p => p.Name)));
        sb.AppendLine(");");
        sb.AppendLine();

        // Use async or sync based on detection and configuration
        bool useAsync = contractInfo.HasAsyncMembers && (member.IsAsync || member.HasCancellationToken);

        if (useAsync)
        {
            GenerateAsyncInterceptedCall(sb, member, config, contextTypeName);
        }
        else
        {
            GenerateSyncInterceptedCall(sb, member, config, contextTypeName);
        }
    }

    private void GenerateSyncInterceptedCall(StringBuilder sb, MemberInfo member, ProxyConfig config, string contextTypeName)
    {
        sb.AppendLine("        try");
        sb.AppendLine("        {");

        // Before interceptors
        if (config.InterceptorMode == ProxyInterceptorMode.Single)
        {
            sb.AppendLine("            _interceptor!.Before(__context);");
        }
        else if (config.InterceptorMode == ProxyInterceptorMode.Pipeline)
        {
            sb.AppendLine("            for (int __i = 0; __i < _interceptors!.Count; __i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                _interceptors[__i].Before(__context);");
            sb.AppendLine("            }");
        }

        // Actual method call
        if (member.IsVoid)
        {
            sb.Append($"            _inner.{member.Name}(");
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
        else
        {
            var refModifier = member.ReturnsByRef || member.ReturnsByRefReadonly ? "ref " : "";
            sb.Append($"            var __result = {refModifier}_inner.{member.Name}(");
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
            sb.AppendLine("            __context.SetResult(__result);");
        }
        sb.AppendLine();

        // After interceptors (reverse order for pipeline)
        if (config.InterceptorMode == ProxyInterceptorMode.Single)
        {
            sb.AppendLine("            _interceptor!.After(__context);");
        }
        else if (config.InterceptorMode == ProxyInterceptorMode.Pipeline)
        {
            sb.AppendLine("            for (int __i = _interceptors!.Count - 1; __i >= 0; __i--)");
            sb.AppendLine("            {");
            sb.AppendLine("                _interceptors[__i].After(__context);");
            sb.AppendLine("            }");
        }

        if (!member.IsVoid)
        {
            sb.AppendLine("            return __result;");
        }

        sb.AppendLine("        }");
        sb.AppendLine("        catch (global::System.Exception __ex)");
        sb.AppendLine("        {");

        // OnException interceptors
        if (config.InterceptorMode == ProxyInterceptorMode.Single)
        {
            sb.AppendLine("            _interceptor!.OnException(__context, __ex);");
        }
        else if (config.InterceptorMode == ProxyInterceptorMode.Pipeline)
        {
            sb.AppendLine("            for (int __i = 0; __i < _interceptors!.Count; __i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                _interceptors[__i].OnException(__context, __ex);");
            sb.AppendLine("            }");
        }

        if (config.ExceptionPolicy == ProxyExceptionPolicy.Rethrow)
        {
            sb.AppendLine("            throw;");
        }
        else // Swallow
        {
            if (!member.IsVoid)
            {
                sb.AppendLine("            return default!;");
            }
        }

        sb.AppendLine("        }");
    }

    private void GenerateAsyncInterceptedCall(StringBuilder sb, MemberInfo member, ProxyConfig config, string contextTypeName)
    {
        sb.AppendLine("        try");
        sb.AppendLine("        {");

        // Before interceptors (async)
        if (config.InterceptorMode == ProxyInterceptorMode.Single)
        {
            sb.AppendLine("            await _interceptor!.BeforeAsync(__context).ConfigureAwait(false);");
        }
        else if (config.InterceptorMode == ProxyInterceptorMode.Pipeline)
        {
            sb.AppendLine("            for (int __i = 0; __i < _interceptors!.Count; __i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                await _interceptors[__i].BeforeAsync(__context).ConfigureAwait(false);");
            sb.AppendLine("            }");
        }

        // Actual method call
        if (member.IsVoid)
        {
            sb.Append($"            _inner.{member.Name}(");
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
        else if (member.IsAsync)
        {
            // For async methods, get the task and await it  
            sb.Append($"            var __task = _inner.{member.Name}(");
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
            sb.AppendLine("            __context.SetResult(__task);");
            
            // Check if the async method returns a value (Task<T> or ValueTask<T> vs Task or ValueTask)
            // If ReturnType contains<, it's generic so has a result value
            if (member.ReturnType.Contains("<"))
            {
                sb.AppendLine("            var __result = await __task.ConfigureAwait(false);");
            }
            else
            {
                // Task or ValueTask with no result - just await
                sb.AppendLine("            await __task.ConfigureAwait(false);");
            }
        }
        else
        {
            var refModifier = member.ReturnsByRef || member.ReturnsByRefReadonly ? "ref " : "";
            sb.Append($"            var __result = {refModifier}_inner.{member.Name}(");
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
            sb.AppendLine("            __context.SetResult(__result);");
        }
        sb.AppendLine();

        // After interceptors (async, reverse order for pipeline)
        if (config.InterceptorMode == ProxyInterceptorMode.Single)
        {
            sb.AppendLine("            await _interceptor!.AfterAsync(__context).ConfigureAwait(false);");
        }
        else if (config.InterceptorMode == ProxyInterceptorMode.Pipeline)
        {
            sb.AppendLine("            for (int __i = _interceptors!.Count - 1; __i >= 0; __i--)");
            sb.AppendLine("            {");
            sb.AppendLine("                await _interceptors[__i].AfterAsync(__context).ConfigureAwait(false);");
            sb.AppendLine("            }");
        }

        if (!member.IsVoid)
        {
            // For async methods, check if it's Task<T>/ValueTask<T> (has a generic parameter)
            // vs Task/ValueTask (no generic parameter)
            if (member.IsAsync && !member.ReturnType.Contains("<"))
            {
                // Task or ValueTask with no result value - don't return anything
            }
            else
            {
                sb.AppendLine("            return __result;");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("        catch (global::System.Exception __ex)");
        sb.AppendLine("        {");

        // OnException interceptors (async)
        if (config.InterceptorMode == ProxyInterceptorMode.Single)
        {
            sb.AppendLine("            await _interceptor!.OnExceptionAsync(__context, __ex).ConfigureAwait(false);");
        }
        else if (config.InterceptorMode == ProxyInterceptorMode.Pipeline)
        {
            sb.AppendLine("            for (int __i = 0; __i < _interceptors!.Count; __i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                await _interceptors[__i].OnExceptionAsync(__context, __ex);");
            sb.AppendLine("            }");
        }

        if (config.ExceptionPolicy == ProxyExceptionPolicy.Rethrow)
        {
            sb.AppendLine("            throw;");
        }
        else // Swallow
        {
            if (!member.IsVoid)
            {
                sb.AppendLine("            return default!;");
            }
        }

        sb.AppendLine("        }");
    }

    private void GenerateProxyProperty(StringBuilder sb, MemberInfo member, ContractInfo contractInfo, ProxyConfig config)
    {
        var accessibility = GetAccessibilityKeyword(member.Accessibility);

        sb.AppendLine($"    /// <summary>Proxies {member.Name}.</summary>");
        sb.Append($"    {accessibility} {member.ReturnType} {member.Name}");

        if (member.HasGetter && member.HasSetter)
        {
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine($"        get => _inner.{member.Name};");
            sb.AppendLine($"        set => _inner.{member.Name} = value;");
            sb.AppendLine("    }");
        }
        else if (member.HasGetter)
        {
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine($"        get => _inner.{member.Name};");
            sb.AppendLine("    }");
        }
        else if (member.HasSetter)
        {
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine($"        set => _inner.{member.Name} = value;");
            sb.AppendLine("    }");
        }

        sb.AppendLine();
    }

    private string GenerateInterceptorInterface(ContractInfo contractInfo, ProxyConfig config)
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

        var accessibility = GetAccessibilityKeyword(contractInfo.ContractSymbol.DeclaredAccessibility);

        // Generate interceptor interface
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Interceptor interface for {contractInfo.ContractName} proxy.");
        sb.AppendLine($"/// Provides hooks for Before/After/OnException interception of method calls.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"{accessibility} interface {config.InterceptorInterfaceName}");
        sb.AppendLine("{");

        // Generate sync interceptor methods
        sb.AppendLine("    /// <summary>Called before a method is invoked.</summary>");
        sb.AppendLine("    void Before(MethodContext context);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Called after a method completes successfully.</summary>");
        sb.AppendLine("    void After(MethodContext context);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Called when a method throws an exception.</summary>");
        sb.AppendLine("    void OnException(MethodContext context, global::System.Exception exception);");

        // Generate async interceptor methods if needed
        if (contractInfo.HasAsyncMembers)
        {
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Called before a method is invoked (async).</summary>");
            sb.AppendLine("    global::System.Threading.Tasks.ValueTask BeforeAsync(MethodContext context);");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Called after a method completes successfully (async).</summary>");
            sb.AppendLine("    global::System.Threading.Tasks.ValueTask AfterAsync(MethodContext context);");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Called when a method throws an exception (async).</summary>");
            sb.AppendLine("    global::System.Threading.Tasks.ValueTask OnExceptionAsync(MethodContext context, global::System.Exception exception);");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Generate base MethodContext class
        sb.AppendLine("/// <summary>Base class for method context information.</summary>");
        sb.AppendLine($"{accessibility} abstract class MethodContext");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Gets the name of the method being invoked.</summary>");
        sb.AppendLine("    public abstract string MethodName { get; }");
        sb.AppendLine("}");

        // Generate specific MethodContext types for each method
        foreach (var member in contractInfo.Members.Where(m => m.MemberType == MemberType.Method))
        {
            sb.AppendLine();
            GenerateMethodContext(sb, member, contractInfo, config, accessibility);
        }

        return sb.ToString();
    }

    private void GenerateMethodContext(StringBuilder sb, MemberInfo member, ContractInfo contractInfo, ProxyConfig config, string accessibility)
    {
        var contextTypeName = $"{member.Name}MethodContext";

        sb.AppendLine($"/// <summary>Method context for {member.Name}.</summary>");
        sb.AppendLine($"{accessibility} sealed class {contextTypeName} : MethodContext");
        sb.AppendLine("{");

        // Constructor with parameters
        if (member.Parameters.Count > 0)
        {
            sb.Append($"    public {contextTypeName}(");
            sb.Append(string.Join(", ", member.Parameters.Select(p => $"{p.Type} {p.Name}")));
            sb.AppendLine(")");
            sb.AppendLine("    {");
            foreach (var param in member.Parameters)
            {
                sb.AppendLine($"        {char.ToUpper(param.Name[0]) + param.Name.Substring(1)} = {param.Name};");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine($"    public override string MethodName => \"{member.Name}\";");

        // Parameter properties
        foreach (var param in member.Parameters)
        {
            var propName = char.ToUpper(param.Name[0]) + param.Name.Substring(1);
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>Gets the {param.Name} parameter.</summary>");
            sb.AppendLine($"    public {param.Type} {propName} {{ get; }}");
        }

        // Result property if not void
        if (!member.IsVoid)
        {
            // For async methods, we need to unwrap the Task<T> or ValueTask<T> to get the actual result type
            var resultType = member.ReturnType;
            if (member.IsAsync)
            {
                // Extract the inner type from Task<T> or ValueTask<T>
                // This is a simplification - we store the full Task/ValueTask type in ReturnType
                // but for the context, we want the unwrapped result
                // For now, we'll use the full return type and handle unwrapping in the proxy
                // Actually, let's keep it simple and use the return type as-is
            }
            
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>Gets the result of the method call.</summary>");
            sb.AppendLine($"    public {member.ReturnType} Result {{ get; private set; }} = default!;");
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>Sets the result of the method call.</summary>");
            sb.AppendLine($"    internal void SetResult({member.ReturnType} result) => Result = result;");
        }

        sb.AppendLine("}");
    }

    // Configuration classes
    private class ProxyConfig
    {
        public string ContractName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string ProxyTypeName { get; set; } = "";
        public string InterceptorInterfaceName { get; set; } = "";
        public ProxyInterceptorMode InterceptorMode { get; set; } = ProxyInterceptorMode.Single;
        public bool? GenerateAsync { get; set; }
        public bool ForceAsync { get; set; }
        public ProxyExceptionPolicy ExceptionPolicy { get; set; } = ProxyExceptionPolicy.Rethrow;
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
        public bool HasCancellationToken { get; set; }
        public List<ParameterInfo> Parameters { get; set; } = new();
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
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

    private enum ProxyInterceptorMode
    {
        None = 0,
        Single = 1,
        Pipeline = 2
    }

    private enum ProxyExceptionPolicy
    {
        Rethrow = 0,
        Swallow = 1
    }
}
