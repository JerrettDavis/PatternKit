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
        context.RegisterSourceOutput(facadeTypes.Collect(), (spc, infos) =>
        {
            foreach (var info in infos.Where(static info => info is not null))
            {
                GenerateFacade(spc, info!);
            }
        });
    }

    private static FacadeInfo? GetFacadeInfo(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol targetType)
            return null;

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
                
                foreach (var methodDecl in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => true))
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

    private static T? GetAttributeProperty<T>(AttributeData attr, string propertyName)
    {
        var prop = attr.NamedArguments.FirstOrDefault(x => x.Key == propertyName);
        if (prop.Value.Value is T value)
            return value;
        return default;
    }

    private static void GenerateFacade(SourceProductionContext context, FacadeInfo info)
    {
        // Validate
        if (!ValidateFacade(context, info))
            return;

        // Generate code
        var source = info.IsHostFirst 
            ? GenerateHostFirstFacade(context, info)
            : GenerateContractFirstFacade(context, info);

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
                sb.AppendLine($"        {dep.FieldName} = {dep.ParameterName} ?? throw new System.ArgumentNullException(nameof({dep.ParameterName}));");
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
            else if (info.MissingMapPolicy == 2 && returnType != "void" && !isAsync) // Ignore
            {
                sb.AppendLine($"        return default!;");
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
            }
        }
        
        return string.Join(", ", args);
    }

    private static ImmutableArray<DependencyInfo> CollectDependencies(FacadeInfo info)
    {
        var dependencies = new Dictionary<string, DependencyInfo>();
        
        foreach (var method in info.Methods)
        {
            if (method.MappingMethod is null)
                continue;
            
            // Look for parameters that are not in the contract
            foreach (var param in method.MappingMethod.Parameters.Where(p => true))
            {
                // Skip if it matches a contract parameter
                if (method.Symbol.Parameters.Any(cp => 
                    SymbolEqualityComparer.Default.Equals(cp.Type, param.Type)))
                    continue;
                
                var typeStr = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                
                if (!dependencies.ContainsKey(typeStr))
                {
                    var fieldName = $"_{ToCamelCase(param.Type.Name)}";
                    var paramName = ToCamelCase(param.Type.Name);
                    
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
                sb.AppendLine($"        {dep.FieldName} = {dep.ParameterName} ?? throw new System.ArgumentNullException(nameof({dep.ParameterName}));");
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
                    var fieldName = $"_{ToCamelCase(param.Type.Name)}";
                    var paramName = ToCamelCase(param.Type.Name);
                    
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
        ImmutableArray<MethodInfo> Methods
    );

    private record MethodInfo(
        IMethodSymbol Symbol,
        string ContractName,
        bool IsAsync,
        bool HasCancellationToken,
        AttributeData? MapAttribute,
        IMethodSymbol? MappingMethod = null,
        bool IsExposed = false,
        bool HasDuplicateMapping = false
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
    }
}
