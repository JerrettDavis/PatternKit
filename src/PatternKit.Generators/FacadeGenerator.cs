using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator that implements the Facade pattern.
/// Generates implementation classes that provide simplified interfaces to complex subsystems.
/// Supports contract-first (interface/partial class) and host-first (static class with exposed methods) approaches.
/// The generated code is self-contained with no dependencies on PatternKit.Core.
/// </summary>
[Generator]
public sealed class FacadeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all types marked with [GenerateFacade]
        var facadeTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Facade.GenerateFacadeAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax 
                                        or InterfaceDeclarationSyntax 
                                        or StructDeclarationSyntax,
            transform: static (gasc, ct) => GetFacadeInfo(gasc, ct)
        ).Where(static x => x is not null);

        // Generate facade implementation for each type
        // Deduplicate by target type to handle multiple [GenerateFacade] attributes
        context.RegisterSourceOutput(facadeTypes.Collect(), (spc, infos) =>
        {
            var uniqueInfos = infos
                .Where(static info => info is not null)
                .GroupBy(static info => info!.TargetType, SymbolEqualityComparer.Default)
                .Select(static g => g.First())
                .ToList();
            
            foreach (var info in uniqueInfos)
            {
                GenerateFacade(spc, info!);
            }
        });
    }

    private static FacadeInfo? GetFacadeInfo(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol targetType)
            return null;

        // Check ALL attributes for auto-facade mode
        var autoFacadeAttrs = context.TargetSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Facade.GenerateFacadeAttribute")
            .Where(a => GetAttributeProperty<string>(a, "TargetTypeName") is not null)
            .ToList();
        
        if (autoFacadeAttrs.Any())
        {
            return GetAutoFacadeInfo(context, targetType, autoFacadeAttrs, ct);
        }

        var attr = context.Attributes[0];
        
        // Read attribute properties
        var facadeTypeName = GetAttributeProperty<string>(attr, "FacadeTypeName");
        var generateAsync = GetAttributeProperty<bool?>(attr, "GenerateAsync") ?? true;
        var forceAsync = GetAttributeProperty<bool?>(attr, "ForceAsync") ?? false;
        var missingMapPolicy = GetAttributeProperty<int?>(attr, "MissingMap") ?? 0; // FacadeMissingMapPolicy.Error

        var ns = targetType.ContainingNamespace.IsGlobalNamespace 
            ? null 
            : targetType.ContainingNamespace.ToDisplayString();

        // Determine facade mode (contract-first vs host-first)
        bool isHostFirst = targetType.IsStatic;
        
        // Collect methods based on mode
        var methods = isHostFirst 
            ? CollectHostFirstMethods(targetType, context.SemanticModel.Compilation)
            : CollectContractFirstMethods(targetType, context.SemanticModel.Compilation);

        // Generate default facade type name
        var defaultFacadeTypeName = isHostFirst 
            ? $"{targetType.Name}Facade"
            : targetType.TypeKind == TypeKind.Interface 
                ? $"{targetType.Name.TrimStart('I')}Impl"
                : $"{targetType.Name}Impl";

        return new FacadeInfo(
            TargetType: targetType,
            Namespace: ns,
            FacadeTypeName: facadeTypeName ?? defaultFacadeTypeName,
            GenerateAsync: generateAsync,
            ForceAsync: forceAsync,
            MissingMapPolicy: missingMapPolicy,
            IsHostFirst: isHostFirst,
            Methods: methods
        );
    }

    private static ImmutableArray<MethodInfo> CollectHostFirstMethods(
        INamedTypeSymbol hostType,
        Compilation compilation)
    {
        var methods = new List<MethodInfo>();
        
        foreach (var member in hostType.GetMembers().OfType<IMethodSymbol>())
        {
            // Look for methods marked with [FacadeExpose]
            var exposeAttr = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == 
                    "PatternKit.Generators.Facade.FacadeExposeAttribute");
            
            if (exposeAttr is null)
                continue;

            var methodName = GetAttributeProperty<string>(exposeAttr, "MethodName") ?? member.Name;
            
            methods.Add(new MethodInfo(
                Symbol: member,
                ContractName: methodName,
                IsAsync: IsAsyncMethod(member),
                HasCancellationToken: HasCancellationTokenParameter(member),
                MapAttribute: exposeAttr,
                IsExposed: true
            ));
        }
        
        return methods.ToImmutableArray();
    }

    private static ImmutableArray<MethodInfo> CollectContractFirstMethods(
        INamedTypeSymbol contractType,
        Compilation compilation)
    {
        var methods = new List<MethodInfo>();
        
        // Get all abstract/interface methods that need implementation
        var contractMethods = contractType.TypeKind == TypeKind.Interface
            ? contractType.GetMembers().OfType<IMethodSymbol>()
            : contractType.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.IsAbstract || m.MethodKind == MethodKind.Ordinary);
        
        foreach (var method in contractMethods)
        {
            // Skip methods marked with [FacadeIgnore]
            if (method.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == 
                "PatternKit.Generators.Facade.FacadeIgnoreAttribute"))
                continue;

            // Look for mapping methods marked with [FacadeMap]
            var candidateMappings = new List<(IMethodSymbol method, AttributeData attr)>();
            
            // Search for methods with [FacadeMap] in the same compilation
            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();
                
                foreach (var methodDecl in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl);
                    if (methodSymbol is null) continue;
                    
                    var attr = methodSymbol.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == 
                            "PatternKit.Generators.Facade.FacadeMapAttribute");
                    
                    if (attr is null) continue;
                    
                    // Check if this maps to our contract method
                    var memberName = GetAttributeProperty<string>(attr, "MemberName");
                    if (!string.IsNullOrEmpty(memberName))
                    {
                        if (memberName == method.Name)
                        {
                            candidateMappings.Add((methodSymbol, attr));
                        }
                    }
                    else if (SignaturesMatch(method, methodSymbol))
                    {
                        candidateMappings.Add((methodSymbol, attr));
                    }
                }
            }
            
            // Check for duplicate mappings (PKFCD003)
            IMethodSymbol? mappingMethod = null;
            AttributeData? mapAttr = null;
            bool hasDuplicateMapping = false;
            
            if (candidateMappings.Count > 1)
            {
                hasDuplicateMapping = true;
                // Use the first mapping found
                mappingMethod = candidateMappings[0].method;
                mapAttr = candidateMappings[0].attr;
            }
            else if (candidateMappings.Count == 1)
            {
                mappingMethod = candidateMappings[0].method;
                mapAttr = candidateMappings[0].attr;
            }
            
            methods.Add(new MethodInfo(
                Symbol: method,
                ContractName: method.Name,
                IsAsync: IsAsyncMethod(method),
                HasCancellationToken: HasCancellationTokenParameter(method),
                MapAttribute: mapAttr,
                MappingMethod: mappingMethod,
                IsExposed: false,
                HasDuplicateMapping: hasDuplicateMapping
            ));
        }
        
        return methods.ToImmutableArray();
    }

    private static FacadeInfo? GetAutoFacadeInfo(
        GeneratorAttributeSyntaxContext context,
        INamedTypeSymbol contractType,
        List<AttributeData> autoFacadeAttrs,
        CancellationToken ct)
    {
        var allMethods = ImmutableArray.CreateBuilder<MethodInfo>();
        var externalTypes = new List<INamedTypeSymbol>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        
        // Validate that auto-facade mode is only used with interfaces
        if (contractType.TypeKind != TypeKind.Interface)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.AutoFacadeOnlyForInterfaces,
                contractType.Locations.FirstOrDefault(),
                contractType.Name));
            
            return new FacadeInfo(
                TargetType: contractType,
                Namespace: contractType.ContainingNamespace.IsGlobalNamespace 
                    ? null 
                    : contractType.ContainingNamespace.ToDisplayString(),
                FacadeTypeName: $"{contractType.Name}Impl",
                GenerateAsync: true,
                ForceAsync: false,
                MissingMapPolicy: 0,
                IsHostFirst: false,
                Methods: ImmutableArray<MethodInfo>.Empty,
                IsAutoFacade: true,
                ExternalTypes: ImmutableArray<INamedTypeSymbol>.Empty,
                Diagnostics: diagnostics.ToImmutable()
            );
        }
        
        int fieldIndex = 0;
        foreach (var attr in autoFacadeAttrs)
        {
            var targetTypeName = GetAttributeProperty<string>(attr, "TargetTypeName")!;
            var include = GetStringArrayProperty(attr, "Include");
            var exclude = GetStringArrayProperty(attr, "Exclude");
            var memberPrefix = GetAttributeProperty<string>(attr, "MemberPrefix") ?? "";
            var fieldName = GetAttributeProperty<string>(attr, "FieldName") 
                           ?? (autoFacadeAttrs.Count > 1 ? $"_target{fieldIndex}" : "_target");
            
            // Validate mutually exclusive filters
            if (include?.Length > 0 && exclude?.Length > 0)
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.MutuallyExclusiveFilters,
                    contractType.Locations.FirstOrDefault()));
                continue;
            }
            
            // Resolve external type
            var externalType = context.SemanticModel.Compilation.GetTypeByMetadataName(targetTypeName);
            
            if (externalType is null)
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.TargetTypeNotFound,
                    contractType.Locations.FirstOrDefault(),
                    targetTypeName));
                continue;
            }
            
            externalTypes.Add(externalType);
            
            // Collect filtered methods
            var (methods, methodDiagnostics) = CollectAutoFacadeMethods(
                externalType, 
                include, 
                exclude,
                memberPrefix,
                fieldName,
                contractType.Locations.FirstOrDefault()
            );
            
            allMethods.AddRange(methods);
            diagnostics.AddRange(methodDiagnostics);
            fieldIndex++;
        }
        
        // If we have diagnostics but no methods, still return FacadeInfo to report the diagnostics
        if (allMethods.Count == 0 && diagnostics.Count == 0)
        {
            return null;
        }
        
        var ns = contractType.ContainingNamespace.IsGlobalNamespace 
            ? null 
            : contractType.ContainingNamespace.ToDisplayString();
        
        var defaultFacadeTypeName = contractType.TypeKind == TypeKind.Interface 
            ? (contractType.Name.StartsWith("I") ? contractType.Name.Substring(1) + "Impl" : contractType.Name + "Impl")
            : contractType.Name + "Impl";
        
        return new FacadeInfo(
            TargetType: contractType,
            Namespace: ns,
            FacadeTypeName: defaultFacadeTypeName,
            GenerateAsync: true,
            ForceAsync: false,
            MissingMapPolicy: 0,
            IsHostFirst: false,
            Methods: allMethods.ToImmutable(),
            IsAutoFacade: true,
            ExternalTypes: externalTypes.ToImmutableArray(),
            Diagnostics: diagnostics.ToImmutable()
        );
    }

    private static (ImmutableArray<MethodInfo> methods, ImmutableArray<Diagnostic> diagnostics) CollectAutoFacadeMethods(
        INamedTypeSymbol externalType,
        string[]? include,
        string[]? exclude,
        string memberPrefix,
        string fieldName,
        Location? location)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        
        var allMethods = externalType
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && 
                        m.DeclaredAccessibility == Accessibility.Public &&
                        !m.IsStatic)
            .ToList();
        
        // Apply include filter
        if (include?.Length > 0)
        {
            var includeSet = new HashSet<string>(include, StringComparer.Ordinal);
            
            // Report if specified member not found in the original list
            var allMethodNames = new HashSet<string>(allMethods.Select(m => m.Name));
            foreach (var name in include.Where(name => !allMethodNames.Contains(name)))
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.MemberNotFound,
                    location,
                    name,
                    externalType.Name));
            }
            
            // Filter to only included methods
            allMethods = allMethods.Where(m => includeSet.Contains(m.Name)).ToList();
        }
        
        // Apply exclude filter
        if (exclude?.Length > 0)
        {
            var excludeSet = new HashSet<string>(exclude, StringComparer.Ordinal);
            allMethods = allMethods.Where(m => !excludeSet.Contains(m.Name)).ToList();
        }
        
        // Warn if no members found
        if (allMethods.Count == 0)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.NoPublicMembers,
                location,
                externalType.Name));
        }
        
        var builder = ImmutableArray.CreateBuilder<MethodInfo>();
        
        foreach (var method in allMethods)
        {
            builder.Add(new MethodInfo(
                Symbol: method,
                ContractName: memberPrefix + method.Name,
                IsAsync: IsAsyncMethod(method),
                HasCancellationToken: HasCancellationTokenParameter(method),
                MapAttribute: null,
                MappingMethod: method,
                IsExposed: true,
                HasDuplicateMapping: false,
                ExternalFieldName: fieldName
            ));
        }
        
        return (builder.ToImmutable(), diagnostics.ToImmutable());
    }

    private static bool SignaturesMatch(IMethodSymbol method1, IMethodSymbol method2)
    {
        // Check return type compatibility
        if (!ReturnTypesCompatible(method1.ReturnType, method2.ReturnType))
            return false;
        
        // Check parameter count and types
        if (method1.Parameters.Length != method2.Parameters.Length)
            return false;
        
        for (int i = 0; i < method1.Parameters.Length; i++)
        {
            var p1 = method1.Parameters[i];
            var p2 = method2.Parameters[i];
            
            if (!SymbolEqualityComparer.Default.Equals(p1.Type, p2.Type))
                return false;
            
            if (p1.RefKind != p2.RefKind)
                return false;
        }
        
        return true;
    }

    private static bool ReturnTypesCompatible(ITypeSymbol type1, ITypeSymbol type2)
    {
        if (SymbolEqualityComparer.Default.Equals(type1, type2))
            return true;
        
        // Check async compatibility (Task<T> vs ValueTask<T>)
        if (IsTaskLike(type1) && IsTaskLike(type2))
        {
            var unwrapped1 = UnwrapTaskType(type1);
            var unwrapped2 = UnwrapTaskType(type2);
            return SymbolEqualityComparer.Default.Equals(unwrapped1, unwrapped2);
        }
        
        return false;
    }

    private static bool IsAsyncMethod(IMethodSymbol method)
    {
        var returnType = method.ReturnType.ToDisplayString();
        return returnType.StartsWith("System.Threading.Tasks.Task") ||
               returnType.StartsWith("System.Threading.Tasks.ValueTask");
    }

    private static bool IsTaskLike(ITypeSymbol type)
    {
        var name = type.ToDisplayString();
        return name.StartsWith("System.Threading.Tasks.Task") ||
               name.StartsWith("System.Threading.Tasks.ValueTask");
    }

    private static ITypeSymbol UnwrapTaskType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.IsGenericType)
            return named.TypeArguments[0];
        
        // Task/ValueTask without generic argument is void
        return type;
    }

    private static bool HasCancellationTokenParameter(IMethodSymbol method)
    {
        return method.Parameters.Any(p => 
            p.Type.ToDisplayString() == "System.Threading.CancellationToken");
    }

    private static string GetAsyncReturnType(IMethodSymbol method)
    {
        if (method.ReturnType is INamedTypeSymbol namedType && namedType.IsGenericType && namedType.TypeArguments.Length > 0)
        {
            return namedType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        return "object";
    }

    private static T? GetAttributeProperty<T>(AttributeData attr, string propertyName)
    {
        var prop = attr.NamedArguments.FirstOrDefault(x => x.Key == propertyName);
        if (prop.Value.Value is T value)
            return value;
        return default;
    }

    private static string[]? GetStringArrayProperty(AttributeData attr, string propertyName)
    {
        var prop = attr.NamedArguments.FirstOrDefault(x => x.Key == propertyName);
        if (prop.Value.IsNull || prop.Value.Kind != TypedConstantKind.Array)
            return null;
        
        return prop.Value.Values
            .Select(v => v.Value as string)
            .Where(s => s is not null)
            .ToArray()!;
    }

    private static void GenerateFacade(SourceProductionContext context, FacadeInfo info)
    {
        // Report any diagnostics
        if (info.Diagnostics is not null)
        {
            foreach (var diagnostic in info.Diagnostics.Value)
            {
                context.ReportDiagnostic(diagnostic);
            }
            
            // If there are error diagnostics, don't generate code
            if (info.Diagnostics.Value.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return;
            }
        }
        
        // Validate
        if (!ValidateFacade(context, info))
            return;

        // Generate code
        string source;
        
        if (info.IsAutoFacade)
        {
            source = GenerateAutoFacade(context, info);
        }
        else if (info.IsHostFirst)
        {
            source = GenerateHostFirstFacade(context, info);
        }
        else
        {
            source = GenerateContractFirstFacade(context, info);
        }

        if (!string.IsNullOrEmpty(source))
        {
            var fileName = $"{info.FacadeTypeName}.Facade.g.cs";
            context.AddSource(fileName, source);
        }
    }

    private static bool ValidateFacade(SourceProductionContext context, FacadeInfo info)
    {
        var location = info.TargetType.Locations.FirstOrDefault();

        // For contract-first, type must be partial
        if (!info.IsHostFirst && !IsPartial(info.TargetType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.TypeMustBePartial,
                location,
                info.TargetType.Name));
            return false;
        }

        // Check for type name conflicts (PKFCD005)
        var facadeTypeName = info.FacadeTypeName;
        var containingNamespace = info.TargetType.ContainingNamespace;
        
        // Search for existing type with same name in the same namespace
        var existingType = containingNamespace.GetTypeMembers(facadeTypeName).FirstOrDefault();
        if (existingType != null && !SymbolEqualityComparer.Default.Equals(existingType, info.TargetType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.TypeNameConflict,
                location,
                facadeTypeName,
                containingNamespace.ToDisplayString()));
            return false;
        }

        // Check for unmapped contract methods
        if (!info.IsHostFirst)
        {
            foreach (var method in info.Methods)
            {
                // Check for duplicate mappings (PKFCD003)
                if (method.HasDuplicateMapping)
                {
                    var methodLocation = method.Symbol.Locations.FirstOrDefault() ?? location;
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.MultipleMappings,
                        methodLocation,
                        method.ContractName));
                }
                
                if (method.MappingMethod is null)
                {
                    if (info.MissingMapPolicy == 0) // Error
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.NoMappedMethod,
                            location,
                            method.ContractName));
                    }
                }
                else
                {
                    // Validate signature match
                    if (!SignaturesMatch(method.Symbol, method.MappingMethod))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.MapSignatureMismatch,
                            location,
                            method.MappingMethod.Name,
                            method.ContractName));
                    }

                    // Check for async mapping without generation enabled
                    if (IsAsyncMethod(method.MappingMethod) && !info.GenerateAsync)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.AsyncMappingDisabled,
                            location,
                            method.MappingMethod.Name));
                    }
                }
            }
        }

        return true;
    }

    private static bool IsPartial(INamedTypeSymbol type)
    {
        return type.DeclaringSyntaxReferences
            .Select(syntaxRef => syntaxRef.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(typeDecl => typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
    }

    private static string GenerateContractFirstFacade(SourceProductionContext context, FacadeInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();

        if (info.Namespace is not null)
        {
            sb.AppendLine($"namespace {info.Namespace};");
            sb.AppendLine();
        }

        // Generate implementation class
        var baseType = info.TargetType.TypeKind == TypeKind.Interface 
            ? $" : {info.TargetType.Name}"
            : "";

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Facade implementation for {info.TargetType.Name}.");
        sb.AppendLine("/// Provides a simplified interface to complex subsystem operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed class {info.FacadeTypeName}{baseType}");
        sb.AppendLine("{");

        // Collect all subsystem dependencies from mapping methods
        var dependencies = CollectDependencies(info);
        
        // Generate fields for dependencies
        foreach (var dep in dependencies)
        {
            sb.AppendLine($"    private readonly {dep.Type} {dep.FieldName};");
        }

        if (dependencies.Any())
            sb.AppendLine();

        // Generate constructor
        if (dependencies.Any())
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Initializes a new instance of {info.FacadeTypeName}.");
            sb.AppendLine("    /// </summary>");
            
            var ctorParams = string.Join(", ", dependencies.Select(d => $"{d.Type} {d.ParameterName}"));
            sb.AppendLine($"    public {info.FacadeTypeName}({ctorParams})");
            sb.AppendLine("    {");
            
            foreach (var dep in dependencies)
            {
                sb.AppendLine($"        {dep.FieldName} = {dep.ParameterName};");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Generate methods in deterministic order
        var orderedMethods = info.Methods.OrderBy(m => m.ContractName).ToList();
        
        foreach (var method in orderedMethods)
        {
            GenerateContractMethod(sb, method, info, dependencies);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateContractMethod(
        StringBuilder sb,
        MethodInfo method,
        FacadeInfo info,
        ImmutableArray<DependencyInfo> dependencies)
    {
        var methodSymbol = method.Symbol;
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {method.ContractName} facade operation.");
        sb.AppendLine("    /// </summary>");

        // Build method signature
        var returnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isAsync = method.IsAsync || info.ForceAsync;
        var asyncModifier = isAsync ? "async " : "";
        
        // Prefer ValueTask for generated async methods
        if (isAsync && returnType == "void")
        {
            returnType = "System.Threading.Tasks.ValueTask";
        }

        var parameters = string.Join(", ", methodSymbol.Parameters.Select(p => 
            $"{GetRefKind(p.RefKind)}{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));

        sb.AppendLine($"    public {asyncModifier}{returnType} {method.ContractName}({parameters})");
        sb.AppendLine("    {");

        if (method.MappingMethod is null)
        {
            // Generate stub based on policy
            if (info.MissingMapPolicy == 1) // Stub
            {
                sb.AppendLine($"        throw new System.NotImplementedException(\"Method {method.ContractName} is not yet implemented.\");");
            }
            else if (info.MissingMapPolicy == 2) // Ignore
            {
                if (isAsync)
                {
                    // Handle async return types
                    var returnTypeStr = methodSymbol.ReturnType.ToDisplayString();
                    if (returnTypeStr.StartsWith("System.Threading.Tasks.ValueTask<"))
                    {
                        sb.AppendLine($"        return System.Threading.Tasks.ValueTask.FromResult<{GetAsyncReturnType(methodSymbol)}>(default!);");
                    }
                    else if (returnTypeStr.StartsWith("System.Threading.Tasks.Task<"))
                    {
                        sb.AppendLine($"        return System.Threading.Tasks.Task.FromResult<{GetAsyncReturnType(methodSymbol)}>(default!);");
                    }
                    else if (returnTypeStr == "System.Threading.Tasks.ValueTask")
                    {
                        sb.AppendLine($"        return System.Threading.Tasks.ValueTask.CompletedTask;");
                    }
                    else if (returnTypeStr == "System.Threading.Tasks.Task")
                    {
                        sb.AppendLine($"        return System.Threading.Tasks.Task.CompletedTask;");
                    }
                    else
                    {
                        sb.AppendLine($"        return default!;");
                    }
                }
                else if (returnType != "void")
                {
                    sb.AppendLine($"        return default!;");
                }
            }
        }
        else
        {
            // Call mapping method
            var mapping = method.MappingMethod;
            var callArgs = BuildCallArguments(mapping, dependencies, methodSymbol.Parameters);
            var awaitKeyword = IsAsyncMethod(mapping) ? "await " : "";
            var returnKeyword = returnType == "void" || (isAsync && returnType == "System.Threading.Tasks.ValueTask") ? "" : "return ";
            
            sb.AppendLine($"        {returnKeyword}{awaitKeyword}{mapping.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{mapping.Name}({callArgs});");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static string GetRefKind(RefKind refKind)
    {
        switch (refKind)
        {
            case RefKind.Ref:
                return "ref ";
            case RefKind.Out:
                return "out ";
            case RefKind.In:
                return "in ";
            default:
                return "";
        }
    }

    private static string BuildCallArguments(
        IMethodSymbol mappingMethod,
        ImmutableArray<DependencyInfo> dependencies,
        ImmutableArray<IParameterSymbol> contractParameters)
    {
        var args = new List<string>();
        
        foreach (var param in mappingMethod.Parameters)
        {
            // Check if it's a dependency
            var dep = dependencies.FirstOrDefault(d => d.Type == param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            if (dep.Type is not null)
            {
                args.Add(dep.FieldName);
            }
            else
            {
                // Match with contract parameter
                var contractParam = contractParameters.FirstOrDefault(cp => 
                    SymbolEqualityComparer.Default.Equals(cp.Type, param.Type));
                
                if (contractParam is not null)
                {
                    var refPrefix = GetRefKind(contractParam.RefKind).Trim();
                    args.Add(string.IsNullOrEmpty(refPrefix) ? contractParam.Name : $"{refPrefix} {contractParam.Name}");
                }
                else
                {
                    // Fallback: pass through the mapping method parameter itself
                    var refPrefix = GetRefKind(param.RefKind).Trim();
                    args.Add(string.IsNullOrEmpty(refPrefix) ? param.Name : $"{refPrefix} {param.Name}");
                }
            }
        }
        
        return string.Join(", ", args);
    }

    private static ImmutableArray<DependencyInfo> CollectDependencies(FacadeInfo info)
    {
        var dependencies = new Dictionary<string, DependencyInfo>();
        var usedFieldNames = new HashSet<string>();
        
        foreach (var method in info.Methods)
        {
            if (method.MappingMethod is null)
                continue;
            
            // Look for parameters that are not in the contract
            foreach (var param in method.MappingMethod.Parameters)
            {
                // Skip if it matches a contract parameter
                if (method.Symbol.Parameters.Any(cp => 
                    SymbolEqualityComparer.Default.Equals(cp.Type, param.Type)))
                    continue;
                
                var typeStr = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                
                if (!dependencies.ContainsKey(typeStr))
                {
                    var baseName = ToCamelCase(param.Type.Name);
                    var fieldName = $"_{baseName}";
                    var paramName = baseName;
                    
                    // Handle name collisions by appending numbers
                    var counter = 1;
                    while (usedFieldNames.Contains(fieldName))
                    {
                        fieldName = $"_{baseName}{counter}";
                        paramName = $"{baseName}{counter}";
                        counter++;
                    }
                    
                    usedFieldNames.Add(fieldName);
                    
                    dependencies[typeStr] = new DependencyInfo(
                        Type: typeStr,
                        FieldName: fieldName,
                        ParameterName: paramName
                    );
                }
            }
        }
        
        return dependencies.Values.OrderBy(d => d.Type).ToImmutableArray();
    }

    private static string GenerateHostFirstFacade(SourceProductionContext context, FacadeInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();

        if (info.Namespace is not null)
        {
            sb.AppendLine($"namespace {info.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Facade for {info.TargetType.Name}.");
        sb.AppendLine("/// Provides a simplified interface to complex subsystem operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed class {info.FacadeTypeName}");
        sb.AppendLine("{");

        // Collect all subsystem dependencies from host methods
        var dependencies = CollectHostDependencies(info);
        
        // Generate fields for dependencies
        foreach (var dep in dependencies)
        {
            sb.AppendLine($"    private readonly {dep.Type} {dep.FieldName};");
        }

        if (dependencies.Any())
            sb.AppendLine();

        // Generate constructor
        if (dependencies.Any())
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Initializes a new instance of {info.FacadeTypeName}.");
            sb.AppendLine("    /// </summary>");
            
            var ctorParams = string.Join(", ", dependencies.Select(d => $"{d.Type} {d.ParameterName}"));
            sb.AppendLine($"    public {info.FacadeTypeName}({ctorParams})");
            sb.AppendLine("    {");
            
            foreach (var dep in dependencies)
            {
                sb.AppendLine($"        {dep.FieldName} = {dep.ParameterName};");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Generate methods in deterministic order
        var orderedMethods = info.Methods.OrderBy(m => m.ContractName).ToList();
        
        foreach (var method in orderedMethods)
        {
            GenerateHostMethod(sb, method, info, dependencies);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateAutoFacade(SourceProductionContext context, FacadeInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();
        
        if (info.Namespace is not null)
        {
            sb.AppendLine($"namespace {info.Namespace};");
            sb.AppendLine();
        }
        
        var baseType = info.TargetType.TypeKind == TypeKind.Interface
            ? $" : {info.TargetType.Name}"
            : "";
        
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Auto-generated facade implementation for {info.TargetType.Name}.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed class {info.FacadeTypeName}{baseType}");
        sb.AppendLine("{");
        
        // Group by external type (for multiple [GenerateFacade] attributes)
        var groupedByField = info.Methods
            .GroupBy(m => m.ExternalFieldName ?? "_target")
            .ToList();
        
        // Generate fields
        foreach (var group in groupedByField)
        {
            var firstMethod = group.First();
            var externalType = firstMethod.MappingMethod!.ContainingType;
            var typeFullName = externalType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"    private readonly {typeFullName} {group.Key};");
        }
        sb.AppendLine();
        
        // Generate constructor
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of {info.FacadeTypeName}.");
        sb.AppendLine("    /// </summary>");
        
        var ctorParams = groupedByField.Select(g =>
        {
            var fieldName = g.Key;
            // Generate parameter name: if field starts with underscore, remove it; otherwise, use field name as-is
            // The parameter will be different from field if no underscore, which is the standard convention
            var paramName = fieldName.StartsWith("_") ? fieldName.Substring(1) : fieldName;
            var externalType = g.First().MappingMethod!.ContainingType;
            var typeFullName = externalType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"{typeFullName} {paramName}";
        });
        
        sb.AppendLine($"    public {info.FacadeTypeName}({string.Join(", ", ctorParams)})");
        sb.AppendLine("    {");
        
        // Generate constructor body with null checks
        // For fields without underscore, the parameter name matches the field name which is valid C#
        foreach (var (fieldName, paramName) in groupedByField.Select(g => 
            (g.Key, g.Key.StartsWith("_") ? g.Key.Substring(1) : g.Key)))
        {
            sb.AppendLine($"        this.{fieldName} = {paramName} ?? throw new System.ArgumentNullException(nameof({paramName}));");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Generate forwarding methods
        foreach (var method in info.Methods.OrderBy(m => m.ContractName))
        {
            GenerateAutoFacadeMethod(sb, method);
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    private static void GenerateAutoFacadeMethod(StringBuilder sb, MethodInfo method)
    {
        var sym = method.Symbol;
        var fieldName = method.ExternalFieldName ?? "_target";
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Forwards to {sym.ContainingType.Name}.{sym.Name}");
        sb.AppendLine("    /// </summary>");
        
        // Build signature
        var returnType = sym.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var typeParams = sym.TypeParameters.Length > 0
            ? $"<{string.Join(", ", sym.TypeParameters.Select(tp => tp.Name))}>"
            : "";
        
        var parameters = string.Join(", ", sym.Parameters.Select(p =>
            $"{GetRefKind(p.RefKind)}{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"
        ));
        
        sb.AppendLine($"    public {returnType} {method.ContractName}{typeParams}({parameters})");
        
        // Type constraints
        if (sym.TypeParameters.Length > 0)
        {
            foreach (var tp in sym.TypeParameters)
            {
                var constraints = BuildTypeConstraints(tp);
                if (!string.IsNullOrEmpty(constraints))
                    sb.AppendLine($"        where {tp.Name} : {constraints}");
            }
        }
        
        sb.AppendLine("    {");
        
        // Forward call
        var args = string.Join(", ", sym.Parameters.Select(p =>
            $"{GetRefKind(p.RefKind)}{p.Name}"
        ));
        
        var call = $"{fieldName}.{sym.Name}{typeParams}({args})";
        
        if (sym.ReturnsVoid)
            sb.AppendLine($"        {call};");
        else
            sb.AppendLine($"        return {call};");
        
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static string BuildTypeConstraints(ITypeParameterSymbol tp)
    {
        var constraints = new List<string>();
        
        if (tp.HasReferenceTypeConstraint)
        {
            // Handle nullable reference type constraint (class?)
            var constraint = "class";
            if (tp.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated)
            {
                constraint = "class?";
            }
            constraints.Add(constraint);
        }
        if (tp.HasValueTypeConstraint)
            constraints.Add("struct");
        if (tp.HasUnmanagedTypeConstraint)
            constraints.Add("unmanaged");
        if (tp.HasNotNullConstraint)
            constraints.Add("notnull");
        
        foreach (var constraintType in tp.ConstraintTypes)
        {
            constraints.Add(constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
        
        if (tp.HasConstructorConstraint)
            constraints.Add("new()");
        
        return string.Join(", ", constraints);
    }

    private static void GenerateHostMethod(
        StringBuilder sb,
        MethodInfo method,
        FacadeInfo info,
        ImmutableArray<DependencyInfo> dependencies)
    {
        var hostMethod = method.Symbol;
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {method.ContractName} operation.");
        sb.AppendLine("    /// </summary>");

        // Build method signature - convert from static to instance
        var returnType = hostMethod.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isAsync = method.IsAsync || info.ForceAsync;
        var asyncModifier = isAsync ? "async " : "";

        // Skip dependency parameters, only include operation parameters
        var operationParams = hostMethod.Parameters
            .Where(p => !dependencies.Any(d => d.Type == p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToList();

        var parameters = string.Join(", ", operationParams.Select(p => 
            $"{GetRefKind(p.RefKind)}{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));

        sb.AppendLine($"    public {asyncModifier}{returnType} {method.ContractName}({parameters})");
        sb.AppendLine("    {");

        // Call host method with dependencies and operation parameters
        var callArgs = BuildHostCallArguments(hostMethod, dependencies, operationParams);
        var awaitKeyword = IsAsyncMethod(hostMethod) ? "await " : "";
        var returnKeyword = returnType == "void" ? "" : "return ";
        
        sb.AppendLine($"        {returnKeyword}{awaitKeyword}{info.TargetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{hostMethod.Name}({callArgs});");

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static string BuildHostCallArguments(
        IMethodSymbol hostMethod,
        ImmutableArray<DependencyInfo> dependencies,
        List<IParameterSymbol> operationParams)
    {
        var args = new List<string>();
        
        foreach (var param in hostMethod.Parameters)
        {
            // Check if it's a dependency
            var dep = dependencies.FirstOrDefault(d => d.Type == param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            if (dep.Type is not null)
            {
                args.Add(dep.FieldName);
            }
            else
            {
                // It's an operation parameter - use FirstOrDefault for safety
                var opParam = operationParams.FirstOrDefault(op => SymbolEqualityComparer.Default.Equals(op.Type, param.Type));
                if (opParam is not null)
                {
                    var refPrefix = GetRefKind(opParam.RefKind).Trim();
                    args.Add(string.IsNullOrEmpty(refPrefix) ? opParam.Name : $"{refPrefix} {opParam.Name}");
                }
                else
                {
                    // Fallback: use parameter name directly
                    args.Add(param.Name);
                }
            }
        }
        
        return string.Join(", ", args);
    }

    private static ImmutableArray<DependencyInfo> CollectHostDependencies(FacadeInfo info)
    {
        var dependencies = new Dictionary<string, DependencyInfo>();
        var usedFieldNames = new HashSet<string>();
        
        foreach (var method in info.Methods)
        {
            // In host-first, the first N parameters that appear consistently across methods are dependencies
            // For simplicity, we'll identify parameters that are reference types and appear early
            
            foreach (var param in method.Symbol.Parameters)
            {
                // Heuristic: dependencies are typically the first parameters and are reference types
                // We'll collect all unique parameter types and let the user define them properly
                var typeStr = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                
                // Skip primitive types
                if (IsPrimitiveType(param.Type))
                    continue;
                
                if (!dependencies.ContainsKey(typeStr))
                {
                    var baseName = ToCamelCase(param.Type.Name);
                    var fieldName = $"_{baseName}";
                    var paramName = baseName;
                    
                    // Handle name collisions by appending numbers
                    var counter = 1;
                    while (usedFieldNames.Contains(fieldName))
                    {
                        fieldName = $"_{baseName}{counter}";
                        paramName = $"{baseName}{counter}";
                        counter++;
                    }
                    
                    usedFieldNames.Add(fieldName);
                    
                    dependencies[typeStr] = new DependencyInfo(
                        Type: typeStr,
                        FieldName: fieldName,
                        ParameterName: paramName
                    );
                }
            }
        }
        
        return dependencies.Values.OrderBy(d => d.Type).ToImmutableArray();
    }

    private static bool IsPrimitiveType(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
            case SpecialType.System_Char:
            case SpecialType.System_String:
            case SpecialType.System_DateTime:
                return true;
            default:
                return type.ToDisplayString() == "System.Threading.CancellationToken";
        }
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        
        // Handle I-prefix for interfaces
        if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            name = name.Substring(1);
        
        if (name.Length == 0)
            return name;
        
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private record FacadeInfo(
        INamedTypeSymbol TargetType,
        string? Namespace,
        string FacadeTypeName,
        bool GenerateAsync,
        bool ForceAsync,
        int MissingMapPolicy,
        bool IsHostFirst,
        ImmutableArray<MethodInfo> Methods,
        bool IsAutoFacade = false,
        ImmutableArray<INamedTypeSymbol>? ExternalTypes = null,
        ImmutableArray<Diagnostic>? Diagnostics = null
    );

    private record MethodInfo(
        IMethodSymbol Symbol,
        string ContractName,
        bool IsAsync,
        bool HasCancellationToken,
        AttributeData? MapAttribute,
        IMethodSymbol? MappingMethod = null,
        bool IsExposed = false,
        bool HasDuplicateMapping = false,
        string? ExternalFieldName = null
    );

    private record struct DependencyInfo(
        string Type,
        string FieldName,
        string ParameterName
    );

    /// <summary>
    /// Diagnostic descriptors for facade pattern generation.
    /// </summary>
    private static class Diagnostics
    {
        private const string Category = "PatternKit.Generators.Facade";

        /// <summary>
        /// PKFCD001: Type must be partial.
        /// </summary>
        public static readonly DiagnosticDescriptor TypeMustBePartial = new(
            "PKFCD001",
            "Type must be partial",
            "Type '{0}' must be declared as partial to allow facade generation",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// PKFCD002: No mapped methods found for facade members.
        /// </summary>
        public static readonly DiagnosticDescriptor NoMappedMethod = new(
            "PKFCD002",
            "No mapped method found",
            "Contract member '{0}' has no corresponding method marked with [FacadeMap]",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// PKFCD003: Multiple mappings found for single facade member.
        /// </summary>
        public static readonly DiagnosticDescriptor MultipleMappings = new(
            "PKFCD003",
            "Multiple mappings found",
            "Contract member '{0}' has multiple methods marked with [FacadeMap]",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// PKFCD004: Map method signature mismatch.
        /// </summary>
        public static readonly DiagnosticDescriptor MapSignatureMismatch = new(
            "PKFCD004",
            "Signature mismatch",
            "Method '{0}' signature does not match contract member '{1}'",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// PKFCD005: Facade type name conflicts.
        /// </summary>
        public static readonly DiagnosticDescriptor TypeNameConflict = new(
            "PKFCD005",
            "Type name conflict",
            "Facade type name '{0}' conflicts with an existing type",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// PKFCD006: Async mapping detected but generation disabled.
        /// </summary>
        public static readonly DiagnosticDescriptor AsyncMappingDisabled = new(
            "PKFCD006",
            "Async mapping with generation disabled",
            "Method '{0}' is async but GenerateAsync is disabled in the attribute",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>
        /// PKFAC001: Target type not found.
        /// </summary>
        public static readonly DiagnosticDescriptor TargetTypeNotFound = new(
            "PKFAC001",
            "Target type not found",
            "Cannot resolve type '{0}'. Ensure the type exists and is referenced.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// PKFAC002: Include and Exclude are mutually exclusive.
        /// </summary>
        public static readonly DiagnosticDescriptor MutuallyExclusiveFilters = new(
            "PKFAC002",
            "Include and Exclude are mutually exclusive",
            "Cannot specify both Include and Exclude on [GenerateFacade].",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// PKFAC003: No public members found.
        /// </summary>
        public static readonly DiagnosticDescriptor NoPublicMembers = new(
            "PKFAC003",
            "No public members found",
            "Type '{0}' has no public members matching filter criteria.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>
        /// PKFAC004: Specified member not found.
        /// </summary>
        public static readonly DiagnosticDescriptor MemberNotFound = new(
            "PKFAC004",
            "Specified member not found",
            "Member '{0}' not found in type '{1}'.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>
        /// PKFAC005: Auto-facade mode only works with interfaces.
        /// </summary>
        public static readonly DiagnosticDescriptor AutoFacadeOnlyForInterfaces = new(
            "PKFAC005",
            "Auto-facade mode only works with interfaces",
            "Auto-facade mode (TargetTypeName) can only be used with partial interfaces. Type '{0}' is not an interface.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
