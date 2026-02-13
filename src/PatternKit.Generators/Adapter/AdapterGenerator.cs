using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators.Adapter;

/// <summary>
/// Source generator for the Adapter pattern.
/// Generates object adapters that implement a target contract by delegating to an adaptee through mapping methods.
/// </summary>
[Generator]
public sealed class AdapterGenerator : IIncrementalGenerator
{
    // Symbol display format for generated code (fully qualified with global::, but use keywords for special types)
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                              SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    // Diagnostic IDs
    private const string DiagIdHostNotStaticPartial = "PKADP001";
    private const string DiagIdTargetNotInterfaceOrAbstract = "PKADP002";
    private const string DiagIdMissingMapping = "PKADP003";
    private const string DiagIdDuplicateMapping = "PKADP004";
    private const string DiagIdSignatureMismatch = "PKADP005";
    private const string DiagIdTypeNameConflict = "PKADP006";
    private const string DiagIdInvalidAdapteeType = "PKADP007";
    private const string DiagIdMapMethodNotStatic = "PKADP008";
    private const string DiagIdEventsNotSupported = "PKADP009";
    private const string DiagIdGenericMethodsNotSupported = "PKADP010";
    private const string DiagIdOverloadedMethodsNotSupported = "PKADP011";
    private const string DiagIdAbstractClassNoParameterlessCtor = "PKADP012";
    private const string DiagIdSettablePropertiesNotSupported = "PKADP013";
    private const string DiagIdNestedOrGenericHost = "PKADP014";
    private const string DiagIdMappingMethodNotAccessible = "PKADP015";
    private const string DiagIdStaticMembersNotSupported = "PKADP016";
    private const string DiagIdRefReturnNotSupported = "PKADP017";
    private const string DiagIdIndexersNotSupported = "PKADP018";

    private static readonly DiagnosticDescriptor HostNotStaticPartialDescriptor = new(
        id: DiagIdHostNotStaticPartial,
        title: "Adapter host must be static partial",
        messageFormat: "Type '{0}' is marked with [GenerateAdapter] but is not declared as 'static partial'",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TargetNotInterfaceOrAbstractDescriptor = new(
        id: DiagIdTargetNotInterfaceOrAbstract,
        title: "Target must be interface or abstract class",
        messageFormat: "Target type '{0}' must be an interface or abstract class",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingMappingDescriptor = new(
        id: DiagIdMissingMapping,
        title: "Missing mapping for target member",
        messageFormat: "No [AdapterMap] method found for target member '{0}.{1}'",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMappingDescriptor = new(
        id: DiagIdDuplicateMapping,
        title: "Duplicate mapping for target member",
        messageFormat: "Multiple [AdapterMap] methods found for target member '{0}'",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SignatureMismatchDescriptor = new(
        id: DiagIdSignatureMismatch,
        title: "Mapping method signature mismatch",
        messageFormat: "Mapping method '{0}' signature does not match target member '{1}': {2}",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TypeNameConflictDescriptor = new(
        id: DiagIdTypeNameConflict,
        title: "Adapter type name conflicts with existing type",
        messageFormat: "Adapter type name '{0}' conflicts with an existing type in namespace '{1}'",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidAdapteeTypeDescriptor = new(
        id: DiagIdInvalidAdapteeType,
        title: "Invalid adaptee type",
        messageFormat: "Adaptee type '{0}' must be a concrete class or struct",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MapMethodNotStaticDescriptor = new(
        id: DiagIdMapMethodNotStatic,
        title: "Mapping method must be static",
        messageFormat: "Mapping method '{0}' must be declared as static",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EventsNotSupportedDescriptor = new(
        id: DiagIdEventsNotSupported,
        title: "Events are not supported",
        messageFormat: "Target type '{0}' contains event '{1}' which is not supported by the adapter generator",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor GenericMethodsNotSupportedDescriptor = new(
        id: DiagIdGenericMethodsNotSupported,
        title: "Generic methods are not supported",
        messageFormat: "Target type '{0}' contains generic method '{1}' which is not supported by the adapter generator",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor OverloadedMethodsNotSupportedDescriptor = new(
        id: DiagIdOverloadedMethodsNotSupported,
        title: "Overloaded methods are not supported",
        messageFormat: "Target type '{0}' contains overloaded method '{1}' which is not supported by the adapter generator",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AbstractClassNoParameterlessCtorDescriptor = new(
        id: DiagIdAbstractClassNoParameterlessCtor,
        title: "Abstract class target requires accessible parameterless constructor",
        messageFormat: "Abstract class '{0}' does not have an accessible parameterless constructor",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SettablePropertiesNotSupportedDescriptor = new(
        id: DiagIdSettablePropertiesNotSupported,
        title: "Settable properties are not supported",
        messageFormat: "Target type '{0}' contains settable property '{1}' which is not supported by the adapter generator",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NestedOrGenericHostDescriptor = new(
        id: DiagIdNestedOrGenericHost,
        title: "Nested or generic host not supported",
        messageFormat: "Adapter host '{0}' cannot be nested or generic",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MappingMethodNotAccessibleDescriptor = new(
        id: DiagIdMappingMethodNotAccessible,
        title: "Mapping method must be accessible",
        messageFormat: "Mapping method '{0}' must be public or internal to be accessible from generated adapter",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor StaticMembersNotSupportedDescriptor = new(
        id: DiagIdStaticMembersNotSupported,
        title: "Static members are not supported",
        messageFormat: "Target type '{0}' contains static member '{1}' which is not supported by the adapter generator",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RefReturnNotSupportedDescriptor = new(
        id: DiagIdRefReturnNotSupported,
        title: "Ref-return members are not supported",
        messageFormat: "Target type '{0}' contains ref-return member '{1}' which is not supported by the adapter generator",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor IndexersNotSupportedDescriptor = new(
        id: DiagIdIndexersNotSupported,
        title: "Indexers are not supported",
        messageFormat: "Target type '{0}' contains indexer '{1}' which is not supported by the adapter generator",
        category: "PatternKit.Generators.Adapter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all class declarations with [GenerateAdapter] attribute
        var adapterHosts = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Adapter.GenerateAdapterAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Collect all hosts so we can detect conflicts across the entire compilation
        var collectedAdapterHosts = adapterHosts.Collect();

        // Generate for all hosts, tracking generated adapter type names globally
        context.RegisterSourceOutput(collectedAdapterHosts, (spc, collectedTypeContexts) =>
        {
            // Track generated adapter type names to detect conflicts (namespace -> type name -> location)
            var generatedAdapters = new Dictionary<string, Dictionary<string, Location>>();

            foreach (var typeContext in collectedTypeContexts)
            {
                if (typeContext.TargetSymbol is not INamedTypeSymbol hostSymbol)
                    continue;

                var node = typeContext.TargetNode;

                // Process each [GenerateAdapter] attribute on the host
                var generateAdapterAttributes = typeContext.Attributes
                    .Where(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Adapter.GenerateAdapterAttribute");

                foreach (var attr in generateAdapterAttributes)
                {
                    GenerateAdapterForAttribute(spc, hostSymbol, attr, node, typeContext.SemanticModel, generatedAdapters);
                }
            }
        });
    }

    private void GenerateAdapterForAttribute(
        SourceProductionContext context,
        INamedTypeSymbol hostSymbol,
        AttributeData attribute,
        SyntaxNode node,
        SemanticModel semanticModel,
        Dictionary<string, Dictionary<string, Location>> generatedAdapters)
    {
        // Validate host is static partial
        if (!IsStaticPartial(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                HostNotStaticPartialDescriptor,
                node.GetLocation(),
                hostSymbol.Name));
            return;
        }

        // Validate host is not nested or generic
        if (hostSymbol.ContainingType is not null || hostSymbol.TypeParameters.Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NestedOrGenericHostDescriptor,
                node.GetLocation(),
                hostSymbol.Name));
            return;
        }

        // Parse attribute arguments
        var config = ParseAdapterConfig(attribute);
        if (config.TargetType is null || config.AdapteeType is null)
            return; // Attribute error, let compiler handle

        // Reject unbound/open generic target types (e.g., typeof(IFoo<>))
        if (config.TargetType.IsUnboundGenericType)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TargetNotInterfaceOrAbstractDescriptor,
                node.GetLocation(),
                config.TargetType.ToDisplayString()));
            return;
        }

        // Reject unbound/open generic adaptee types (e.g., typeof(IFoo<>))
        if (config.AdapteeType.IsUnboundGenericType)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidAdapteeTypeDescriptor,
                node.GetLocation(),
                config.AdapteeType.ToDisplayString()));
            return;
        }

        // Validate target is interface or abstract class
        if (!IsValidTargetType(config.TargetType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TargetNotInterfaceOrAbstractDescriptor,
                node.GetLocation(),
                config.TargetType.ToDisplayString()));
            return;
        }

        // Validate adaptee is concrete type
        if (!IsValidAdapteeType(config.AdapteeType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidAdapteeTypeDescriptor,
                node.GetLocation(),
                config.AdapteeType.ToDisplayString()));
            return;
        }

        // For abstract class targets, validate accessible parameterless constructor exists
        if (config.TargetType.TypeKind == TypeKind.Class && config.TargetType.IsAbstract)
        {
            var hasAccessibleParameterlessCtor = config.TargetType.InstanceConstructors
                .Any(c =>
                {
                    if (c.Parameters.Length > 0)
                        return false;

                    var accessibility = c.DeclaredAccessibility;
                    if (accessibility == Accessibility.Public ||
                        accessibility == Accessibility.Protected ||
                        accessibility == Accessibility.ProtectedOrInternal)
                        return true;

                    // Internal and private protected constructors are only accessible within the same assembly
                    if (accessibility == Accessibility.Internal ||
                        accessibility == Accessibility.ProtectedAndInternal)
                        return semanticModel.Compilation.IsSymbolAccessibleWithin(c, semanticModel.Compilation.Assembly);

                    return false;
                });

            if (!hasAccessibleParameterlessCtor)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    AbstractClassNoParameterlessCtorDescriptor,
                    node.GetLocation(),
                    config.TargetType.ToDisplayString()));
                return;
            }
        }

        // Check for unsupported members (events, generic methods, overloads)
        var unsupportedMemberErrors = ValidateTargetMembers(config.TargetType, node.GetLocation());
        foreach (var diagnostic in unsupportedMemberErrors)
        {
            context.ReportDiagnostic(diagnostic);
        }
        if (unsupportedMemberErrors.Any())
            return;

        // Get all mapping methods from host
        var mappingMethods = GetMappingMethods(hostSymbol, config.AdapteeType);

        // Validate mapping methods are static and accessible
        foreach (var (method, _) in mappingMethods)
        {
            if (!method.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MapMethodNotStaticDescriptor,
                    method.Locations.FirstOrDefault() ?? node.GetLocation(),
                    method.Name));
                return;
            }

            // Validate method is accessible (public or internal)
            if (method.DeclaredAccessibility != Accessibility.Public &&
                method.DeclaredAccessibility != Accessibility.Internal)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MappingMethodNotAccessibleDescriptor,
                    method.Locations.FirstOrDefault() ?? node.GetLocation(),
                    method.Name));
                return;
            }
        }

        // Get target members that need mapping
        var targetMembers = GetTargetMembers(config.TargetType);

        // Build mapping dictionary and validate
        var memberMappings = new Dictionary<ISymbol, IMethodSymbol>(SymbolEqualityComparer.Default);
        var hasErrors = false;

        foreach (var targetMember in targetMembers)
        {
            var memberName = targetMember.Name;
            var matchingMaps = mappingMethods.Where(m => m.TargetMember == memberName).ToList();

            if (matchingMaps.Count == 0)
            {
                if (config.MissingMapPolicy == AdapterMissingMapPolicyValue.Error)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MissingMappingDescriptor,
                        node.GetLocation(),
                        config.TargetType.Name,
                        memberName));
                    hasErrors = true;
                }
                // For ThrowingStub, we'll generate the stub later
            }
            else if (matchingMaps.Count > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateMappingDescriptor,
                    matchingMaps[1].Method.Locations.FirstOrDefault() ?? node.GetLocation(),
                    memberName));
                hasErrors = true;
            }
            else
            {
                var mapping = matchingMaps[0];
                var signatureError = ValidateSignature(targetMember, mapping.Method, config.AdapteeType);
                if (signatureError is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SignatureMismatchDescriptor,
                        mapping.Method.Locations.FirstOrDefault() ?? node.GetLocation(),
                        mapping.Method.Name,
                        memberName,
                        signatureError));
                    hasErrors = true;
                }
                else
                {
                    memberMappings[targetMember] = mapping.Method;
                }
            }
        }

        if (hasErrors)
            return;

        // Determine adapter type name
        var adapterTypeName = config.AdapterTypeName
            ?? $"{config.AdapteeType.Name}To{config.TargetType.Name}Adapter";

        // Determine namespace
        var ns = config.Namespace
            ?? (hostSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : hostSymbol.ContainingNamespace.ToDisplayString());

        // Check for type name conflict (PKADP006) - both in existing compilation and in current generator run
        if (HasTypeNameConflict(semanticModel.Compilation, ns, adapterTypeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNameConflictDescriptor,
                node.GetLocation(),
                adapterTypeName,
                string.IsNullOrEmpty(ns) ? "<global>" : ns));
            return;
        }

        // Check for conflict with adapters being generated in this run
        var normalizedNs = string.IsNullOrEmpty(ns) ? "" : ns;
        if (!generatedAdapters.ContainsKey(normalizedNs))
            generatedAdapters[normalizedNs] = new Dictionary<string, Location>();

        if (generatedAdapters[normalizedNs].ContainsKey(adapterTypeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNameConflictDescriptor,
                node.GetLocation(),
                adapterTypeName,
                string.IsNullOrEmpty(ns) ? "<global>" : ns));
            return;
        }

        // Track this adapter type name
        generatedAdapters[normalizedNs][adapterTypeName] = node.GetLocation();

        // Generate adapter
        var source = GenerateAdapterCode(
            adapterTypeName,
            ns,
            config.TargetType,
            config.AdapteeType,
            hostSymbol,
            targetMembers,
            memberMappings,
            config.MissingMapPolicy,
            config.Sealed);

        var hintName = string.IsNullOrEmpty(ns)
            ? $"{adapterTypeName}.Adapter.g.cs"
            : $"{ns}.{adapterTypeName}.Adapter.g.cs";

        context.AddSource(hintName, source);
    }

    private static bool IsStaticPartial(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl)
            return false;

        var hasStatic = classDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
        var hasPartial = classDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
        return hasStatic && hasPartial;
    }

    private static bool IsValidTargetType(INamedTypeSymbol type)
    {
        return type.TypeKind == TypeKind.Interface ||
               (type.TypeKind == TypeKind.Class && type.IsAbstract);
    }

    private static bool IsValidAdapteeType(INamedTypeSymbol type)
    {
        return (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) &&
               !type.IsAbstract;
    }

    private static bool HasTypeNameConflict(Compilation compilation, string ns, string typeName)
    {
        var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
        var existingType = compilation.GetTypeByMetadataName(fullName);
        if (existingType is null)
            return false;

        // If the type comes from metadata only, we can't add a partial declaration safely.
        if (existingType.DeclaringSyntaxReferences.Length == 0)
            return true;

        // Only treat this as a conflict if any declaration is non-partial.
        foreach (var syntaxRef in existingType.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();
            if (syntax is not TypeDeclarationSyntax typeDecl)
                return true;

            if (!typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                return true;
        }

        // All declarations are partial; allow the generator to emit its own partial type.
        return false;
    }

    private List<Diagnostic> ValidateTargetMembers(INamedTypeSymbol targetType, Location fallbackLocation)
    {
        var diagnostics = new List<Diagnostic>();
        var isAbstractClass = targetType.TypeKind == TypeKind.Class && targetType.IsAbstract;

        // Track method signatures to detect true overloads vs diamond inheritance
        // Key: method name, Value: set of full signatures for that name
        var methodSignatures = new Dictionary<string, HashSet<string>>();

        // Collect all members from the type hierarchy
        var typesToProcess = new Queue<INamedTypeSymbol>();
        typesToProcess.Enqueue(targetType);
        var processed = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        while (typesToProcess.Count > 0)
        {
            var type = typesToProcess.Dequeue();
            if (!processed.Add(type))
                continue;

            var membersToCheck = type.GetMembers()
                .Where(m => !isAbstractClass || m.IsAbstract);

            foreach (var member in membersToCheck)
            {
                // Use member location if available, otherwise fall back to host location
                var location = member.Locations.FirstOrDefault() ?? fallbackLocation;

                // Check for static members (not supported)
                if (member.IsStatic)
                {
                    diagnostics.Add(Diagnostic.Create(
                        StaticMembersNotSupportedDescriptor,
                        location,
                        targetType.Name,
                        member.Name));
                }

                // Check for events (not supported)
                if (member is IEventSymbol evt)
                {
                    diagnostics.Add(Diagnostic.Create(
                        EventsNotSupportedDescriptor,
                        location,
                        targetType.Name,
                        evt.Name));
                }

                // Check for indexers (not supported) - must be checked before other property checks
                if (member is IPropertySymbol propertySymbol && propertySymbol.IsIndexer)
                {
                    diagnostics.Add(Diagnostic.Create(
                        IndexersNotSupportedDescriptor,
                        location,
                        targetType.Name,
                        propertySymbol.ToDisplayString()));
                }

                // Check for settable properties (not supported)
                if (member is IPropertySymbol prop && !prop.IsIndexer && prop.SetMethod is not null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        SettablePropertiesNotSupportedDescriptor,
                        location,
                        targetType.Name,
                        prop.Name));
                }

                // Check for ref-return properties (not supported)
                if (member is IPropertySymbol refProp && !refProp.IsIndexer && refProp.ReturnsByRef)
                {
                    diagnostics.Add(Diagnostic.Create(
                        RefReturnNotSupportedDescriptor,
                        location,
                        targetType.Name,
                        refProp.Name));
                }

                // Check for generic methods (not supported)
                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                {
                    if (method.IsGenericMethod)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            GenericMethodsNotSupportedDescriptor,
                            location,
                            targetType.Name,
                            method.Name));
                    }

                    // Check for ref-return methods (not supported)
                    if (method.ReturnsByRef || method.ReturnsByRefReadonly)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            RefReturnNotSupportedDescriptor,
                            location,
                            targetType.Name,
                            method.Name));
                    }

                    // Track full method signature for overload detection
                    var sig = GetMemberSignature(method);
                    if (!methodSignatures.TryGetValue(method.Name, out var sigs))
                    {
                        sigs = new HashSet<string>();
                        methodSignatures[method.Name] = sigs;
                    }
                    sigs.Add(sig);
                }
            }

            // Add base interfaces
            foreach (var iface in type.Interfaces)
                typesToProcess.Enqueue(iface);

            // Add base class (for abstract classes)
            if (type.BaseType is not null && type.BaseType.IsAbstract)
                typesToProcess.Enqueue(type.BaseType);
        }

        // Check for true overloaded methods (same name, different signatures)
        // Diamond inheritance (same signature from multiple paths) is OK
        foreach (var kvp in methodSignatures.Where(kvp => kvp.Value.Count > 1))
        {
            diagnostics.Add(Diagnostic.Create(
                OverloadedMethodsNotSupportedDescriptor,
                fallbackLocation,
                targetType.Name,
                kvp.Key));
        }

        return diagnostics;
    }

    private static AdapterConfig ParseAdapterConfig(AttributeData attribute)
    {
        var config = new AdapterConfig();

        foreach (var named in attribute.NamedArguments)
        {
            switch (named.Key)
            {
                case "Target":
                    config.TargetType = named.Value.Value as INamedTypeSymbol;
                    break;
                case "Adaptee":
                    config.AdapteeType = named.Value.Value as INamedTypeSymbol;
                    break;
                case "AdapterTypeName":
                    config.AdapterTypeName = named.Value.Value as string;
                    break;
                case "MissingMap":
                    if (named.Value.Value is int missingMapValue &&
                        global::System.Enum.IsDefined(typeof(AdapterMissingMapPolicyValue), missingMapValue))
                    {
                        config.MissingMapPolicy = (AdapterMissingMapPolicyValue)missingMapValue;
                    }
                    break;
                case "Sealed":
                    if (named.Value.Value is bool sealedValue)
                        config.Sealed = sealedValue;
                    break;
                case "Namespace":
                    config.Namespace = named.Value.Value as string;
                    break;
            }
        }

        return config;
    }

    private static List<(IMethodSymbol Method, string TargetMember)> GetMappingMethods(
        INamedTypeSymbol hostSymbol,
        INamedTypeSymbol adapteeType)
    {
        var mappings = new List<(IMethodSymbol, string)>();

        foreach (var member in hostSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var mapAttr = member.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Adapter.AdapterMapAttribute");

            if (mapAttr is null)
                continue;

            // Filter by first parameter type matching the adaptee type
            if (member.Parameters.Length == 0)
                continue;

            var firstParamType = member.Parameters[0].Type;
            if (!SymbolEqualityComparer.Default.Equals(firstParamType, adapteeType))
                continue;

            var targetMember = mapAttr.NamedArguments
                .FirstOrDefault(na => na.Key == "TargetMember")
                .Value.Value as string;

            if (targetMember is not null)
            {
                mappings.Add((member, targetMember));
            }
        }

        return mappings;
    }

    private static List<ISymbol> GetTargetMembers(INamedTypeSymbol targetType)
    {
        var members = new List<ISymbol>();
        var seenSignatures = new HashSet<string>();
        var isAbstractClass = targetType.TypeKind == TypeKind.Class && targetType.IsAbstract;

        // Get members from this type and all base interfaces/classes
        var typesToProcess = new Queue<INamedTypeSymbol>();
        typesToProcess.Enqueue(targetType);

        var processed = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        while (typesToProcess.Count > 0)
        {
            var type = typesToProcess.Dequeue();
            if (!processed.Add(type))
                continue;

            var membersToProcess = type.GetMembers()
                .Where(m => !m.IsStatic) // Exclude static members
                .Where(m =>
                {
                    // For abstract classes, only include abstract members
                    if (isAbstractClass)
                        return m.IsAbstract;
                    
                    // For interfaces, only include abstract members (exclude default implementations)
                    // Properties/methods with implementations (C# 8.0+) are not abstract
                    if (type.TypeKind == TypeKind.Interface)
                        return m.IsAbstract;
                    
                    return true;
                });

            foreach (var member in membersToProcess)
            {
                // Include methods (not constructors), properties (not events - not supported)
                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                {
                    // De-duplicate by signature for interface diamonds
                    var sig = GetMemberSignature(method);
                    if (seenSignatures.Add(sig))
                        members.Add(member);
                }
                else if (member is IPropertySymbol prop && !prop.IsIndexer)
                {
                    // De-duplicate by name+type for properties
                    var sig = $"P:{prop.Name}:{prop.Type.ToDisplayString(FullyQualifiedFormat)}";
                    if (seenSignatures.Add(sig))
                        members.Add(member);
                }
                // Events are intentionally excluded - not supported by this generator
            }

            // Add base interfaces
            foreach (var iface in type.Interfaces)
            {
                typesToProcess.Enqueue(iface);
            }

            // Add base class (for abstract classes)
            if (type.BaseType is not null && type.BaseType.IsAbstract)
            {
                typesToProcess.Enqueue(type.BaseType);
            }
        }

        // Order by declaration order when available, falling back to stable sort for metadata-only symbols
        // This provides both readable (contract-ordered) output and deterministic ordering
        return members.OrderBy(m =>
        {
            // Try to get syntax declaration order by line number
            var syntaxRef = m.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef != null)
            {
                var location = syntaxRef.GetSyntax().GetLocation();
                var lineSpan = location.GetLineSpan();
                // Use only line number for ordering, not file path (which varies across machines)
                // Note: For types split across multiple partial files, this may not preserve
                // perfect declaration order, but ThenBy clauses provide stable fallback ordering
                return lineSpan.StartLinePosition.Line;
            }
            // For metadata-only symbols without source, use a fallback ordering
            return int.MaxValue;
        })
        .ThenBy(m => m.Kind)
        .ThenBy(m => m.Name)
        .ThenBy(m => m.ToDisplayString(FullyQualifiedFormat))
        .ToList();
    }

    private static string GetMemberSignature(IMethodSymbol method)
    {
        var paramSig = string.Join(",", method.Parameters.Select(p =>
            $"{p.RefKind}:{p.Type.ToDisplayString(FullyQualifiedFormat)}"));
        return $"M:{method.Name}({paramSig}):{method.ReturnType.ToDisplayString(FullyQualifiedFormat)}";
    }

    private static string? ValidateSignature(ISymbol targetMember, IMethodSymbol mapMethod, INamedTypeSymbol adapteeType)
    {
        // First parameter must be the adaptee type
        if (mapMethod.Parameters.Length == 0)
            return $"First parameter must be of type '{adapteeType.ToDisplayString()}'.";

        var firstParam = mapMethod.Parameters[0];
        if (!SymbolEqualityComparer.Default.Equals(firstParam.Type, adapteeType))
            return $"First parameter must be of type '{adapteeType.ToDisplayString()}', but was '{firstParam.Type.ToDisplayString()}'.";

        // Adaptee parameter must be passed by value (no ref/in/out) and cannot be a 'this' or 'scoped' parameter,
        // because the generated call site always passes `_adaptee` without any modifier.
        if (firstParam.RefKind != RefKind.None)
            return "Adaptee parameter must not have a ref, in, or out modifier.";

        if (firstParam.IsThis)
            return "Adaptee parameter cannot be declared with the 'this' modifier.";

        if (firstParam.ScopedKind != ScopedKind.None)
            return "Adaptee parameter cannot be declared with the 'scoped' modifier.";

        if (targetMember is IMethodSymbol targetMethod)
        {
            // Check return type with nullability
            if (!SymbolEqualityComparer.IncludeNullability.Equals(mapMethod.ReturnType, targetMethod.ReturnType))
                return $"Return type must be '{targetMethod.ReturnType.ToDisplayString()}', but was '{mapMethod.ReturnType.ToDisplayString()}'.";

            // Check remaining parameters (after adaptee)
            var mapParams = mapMethod.Parameters.Skip(1).ToList();
            var targetParams = targetMethod.Parameters.ToList();

            if (mapParams.Count != targetParams.Count)
                return $"Expected {targetParams.Count} parameters (after adaptee), but found {mapParams.Count}.";

            for (int i = 0; i < targetParams.Count; i++)
            {
                var mapParam = mapParams[i];
                var targetParam = targetParams[i];

                if (!SymbolEqualityComparer.IncludeNullability.Equals(mapParam.Type, targetParam.Type))
                    return $"Parameter '{targetParam.Name}' type mismatch: expected '{targetParam.Type.ToDisplayString()}', but was '{mapParam.Type.ToDisplayString()}'.";

                if (mapParam.RefKind != targetParam.RefKind)
                    return $"Parameter '{targetParam.Name}' ref kind mismatch: expected '{targetParam.RefKind}', but was '{mapParam.RefKind}'.";
            }
        }
        else if (targetMember is IPropertySymbol targetProp)
        {
            // For property getters, no additional parameters
            if (mapMethod.Parameters.Length != 1)
                return $"Property getter mapping must have exactly one parameter (the adaptee).";

            // Check return type with nullability
            if (!SymbolEqualityComparer.IncludeNullability.Equals(mapMethod.ReturnType, targetProp.Type))
                return $"Return type must be '{targetProp.Type.ToDisplayString()}', but was '{mapMethod.ReturnType.ToDisplayString()}'.";
        }

        return null; // Valid
    }

    private static string GenerateAdapterCode(
        string adapterTypeName,
        string ns,
        INamedTypeSymbol targetType,
        INamedTypeSymbol adapteeType,
        INamedTypeSymbol hostSymbol,
        List<ISymbol> targetMembers,
        Dictionary<ISymbol, IMethodSymbol> memberMappings,
        AdapterMissingMapPolicyValue missingMapPolicy,
        bool isSealed)
    {
        var sb = new StringBuilder();

        // File header
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Namespace
        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        // Class declaration
        var sealedModifier = isSealed ? "sealed " : "";
        var targetTypeName = targetType.ToDisplayString(FullyQualifiedFormat);
        var adapteeTypeName = adapteeType.ToDisplayString(FullyQualifiedFormat);
        var hostTypeName = hostSymbol.ToDisplayString(FullyQualifiedFormat);

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Adapter that implements <see cref=\"{targetTypeName}\"/> by delegating to <see cref=\"{adapteeTypeName}\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public {sealedModifier}partial class {adapterTypeName} : {targetTypeName}");
        sb.AppendLine("{");

        // Field
        sb.AppendLine($"    private readonly {adapteeTypeName} _adaptee;");
        sb.AppendLine();

        // Constructor
        var isValueTypeAdaptee = adapteeType.IsValueType;
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the <see cref=\"{adapterTypeName}\"/> class.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"adaptee\">The adaptee instance to delegate to.</param>");
        if (!isValueTypeAdaptee)
        {
            sb.AppendLine($"    /// <exception cref=\"global::System.ArgumentNullException\">Thrown when <paramref name=\"adaptee\"/> is null.</exception>");
        }
        sb.AppendLine($"    public {adapterTypeName}({adapteeTypeName} adaptee)");
        sb.AppendLine("    {");
        if (isValueTypeAdaptee)
        {
            sb.AppendLine("        _adaptee = adaptee;");
        }
        else
        {
            sb.AppendLine("        _adaptee = adaptee ?? throw new global::System.ArgumentNullException(nameof(adaptee));");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate members
        var isAbstractClassTarget = targetType.TypeKind == TypeKind.Class && targetType.IsAbstract;
        foreach (var member in targetMembers)
        {
            if (memberMappings.TryGetValue(member, out var mapMethod))
            {
                GenerateMappedMember(sb, member, mapMethod, hostTypeName, isAbstractClassTarget);
            }
            else if (missingMapPolicy == AdapterMissingMapPolicyValue.ThrowingStub)
            {
                GenerateThrowingStub(sb, member, isAbstractClassTarget);
            }
            // Ignore policy: don't generate anything (will cause compile error if interface)
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateMappedMember(StringBuilder sb, ISymbol member, IMethodSymbol mapMethod, string hostTypeName, bool isAbstractClassTarget)
    {
        // Determine if we need 'override' keyword (for abstract class members)
        var overrideKeyword = isAbstractClassTarget && member.IsAbstract ? "override " : "";

        if (member is IMethodSymbol targetMethod)
        {
            // Generate method
            var returnType = targetMethod.ReturnType.ToDisplayString(FullyQualifiedFormat);
            var methodName = targetMethod.Name;
            var parameters = string.Join(", ", targetMethod.Parameters.Select(p =>
                $"{GetParameterModifiers(p)}{p.Type.ToDisplayString(FullyQualifiedFormat)} {p.Name}{GetDefaultValue(p)}"));
            var parameterNames = string.Join(", ", targetMethod.Parameters.Select(p => 
                $"{GetArgumentModifier(p)}{p.Name}"));

            var isVoid = targetMethod.ReturnsVoid;
            var callExpression = $"{hostTypeName}.{mapMethod.Name}(_adaptee{(string.IsNullOrEmpty(parameterNames) ? "" : ", " + parameterNames)})";

            sb.AppendLine($"    /// <inheritdoc/>");
            sb.AppendLine($"    public {overrideKeyword}{returnType} {methodName}({parameters})");
            sb.AppendLine("    {");
            if (isVoid)
            {
                sb.AppendLine($"        {callExpression};");
            }
            else
            {
                sb.AppendLine($"        return {callExpression};");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        else if (member is IPropertySymbol targetProp)
        {
            // Generate property
            var propType = targetProp.Type.ToDisplayString(FullyQualifiedFormat);
            var propName = targetProp.Name;

            sb.AppendLine($"    /// <inheritdoc/>");
            sb.AppendLine($"    public {overrideKeyword}{propType} {propName}");
            sb.AppendLine("    {");
            // Only read-only properties are supported (setters are caught by PKADP013)
            if (targetProp.GetMethod is not null)
            {
                sb.AppendLine($"        get => {hostTypeName}.{mapMethod.Name}(_adaptee);");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    private static void GenerateThrowingStub(StringBuilder sb, ISymbol member, bool isAbstractClassTarget)
    {
        var overrideKeyword = isAbstractClassTarget && member.IsAbstract ? "override " : "";

        if (member is IMethodSymbol targetMethod)
        {
            var returnType = targetMethod.ReturnType.ToDisplayString(FullyQualifiedFormat);
            var methodName = targetMethod.Name;
            var parameters = string.Join(", ", targetMethod.Parameters.Select(p =>
                $"{GetParameterModifiers(p)}{p.Type.ToDisplayString(FullyQualifiedFormat)} {p.Name}{GetDefaultValue(p)}"));

            sb.AppendLine($"    /// <inheritdoc/>");
            sb.AppendLine($"    /// <remarks>This member is not mapped and will throw <see cref=\"global::System.NotImplementedException\"/>.</remarks>");
            sb.AppendLine($"    public {overrideKeyword}{returnType} {methodName}({parameters})");
            sb.AppendLine("    {");
            sb.AppendLine($"        throw new global::System.NotImplementedException(\"No [AdapterMap] provided for '{methodName}'.\");");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        else if (member is IPropertySymbol targetProp)
        {
            var propType = targetProp.Type.ToDisplayString(FullyQualifiedFormat);
            var propName = targetProp.Name;

            sb.AppendLine($"    /// <inheritdoc/>");
            sb.AppendLine($"    /// <remarks>This property is not mapped and will throw <see cref=\"global::System.NotImplementedException\"/>.</remarks>");
            sb.AppendLine($"    public {overrideKeyword}{propType} {propName}");
            sb.AppendLine("    {");
            // Only read-only properties are supported (setters are caught by PKADP013)
            if (targetProp.GetMethod is not null)
            {
                sb.AppendLine($"        get => throw new global::System.NotImplementedException(\"No [AdapterMap] provided for '{propName}'.\");");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    private static string GetParameterModifiers(IParameterSymbol param)
    {
        return param.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnlyParameter => "ref readonly ",
            _ => ""
        };
    }

    private static string GetArgumentModifier(IParameterSymbol param)
    {
        return param.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnlyParameter => "in ",
            _ => ""
        };
    }

    private static string GetDefaultValue(IParameterSymbol param)
    {
        if (!param.HasExplicitDefaultValue)
            return "";

        var value = param.ExplicitDefaultValue;

        if (value is null)
        {
            // For reference types or nullable value types, emit 'null'; otherwise, use 'default'
            if (param.Type.IsReferenceType || param.NullableAnnotation == NullableAnnotation.Annotated)
                return " = null";

            return " = default";
        }

        // Handle enum parameters specially to emit proper enum syntax
        if (param.Type.TypeKind == TypeKind.Enum && param.Type is INamedTypeSymbol enumType)
        {
            // Try to find the enum field matching this value
            var enumField = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, value));

            if (enumField != null)
            {
                return $" = {enumType.ToDisplayString(FullyQualifiedFormat)}.{enumField.Name}";
            }

            // Fallback: cast the numeric value
            return $" = ({enumType.ToDisplayString(FullyQualifiedFormat)}){value}";
        }

        var literal = SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false);
        return " = " + literal;
    }

    // Helper types

    private enum AdapterMissingMapPolicyValue
    {
        Error = 0,
        ThrowingStub = 1,
        Ignore = 2
    }

    private class AdapterConfig
    {
        public INamedTypeSymbol? TargetType { get; set; }
        public INamedTypeSymbol? AdapteeType { get; set; }
        public string? AdapterTypeName { get; set; }
        public AdapterMissingMapPolicyValue MissingMapPolicy { get; set; } = AdapterMissingMapPolicyValue.Error;
        public bool Sealed { get; set; } = true;
        public string? Namespace { get; set; }
    }
}
