using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators.Singleton;

/// <summary>
/// Source generator for the Singleton pattern.
/// Generates thread-safe singleton instance accessors with configurable initialization modes.
/// </summary>
[Generator]
public sealed class SingletonGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKSNG001";
    private const string DiagIdNotClass = "PKSNG002";
    private const string DiagIdNoConstructorOrFactory = "PKSNG003";
    private const string DiagIdMultipleFactories = "PKSNG004";
    private const string DiagIdPublicConstructor = "PKSNG005";
    private const string DiagIdNameConflict = "PKSNG006";
    private const string DiagIdGenericType = "PKSNG007";
    private const string DiagIdNestedType = "PKSNG008";
    private const string DiagIdInvalidPropertyName = "PKSNG009";
    private const string DiagIdAbstractType = "PKSNG010";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [Singleton] must be partial",
        messageFormat: "Type '{0}' is marked with [Singleton] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NotClassDescriptor = new(
        id: DiagIdNotClass,
        title: "Singleton type must be a class",
        messageFormat: "Type '{0}' is marked with [Singleton] but is not a class. Only classes and record classes are supported for singleton generation.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoConstructorOrFactoryDescriptor = new(
        id: DiagIdNoConstructorOrFactory,
        title: "No usable constructor or factory method found",
        messageFormat: "Type '{0}' has no accessible parameterless constructor and no method marked with [SingletonFactory]. Add a parameterless constructor or mark a static factory method with [SingletonFactory].",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleFactoriesDescriptor = new(
        id: DiagIdMultipleFactories,
        title: "Multiple [SingletonFactory] methods found",
        messageFormat: "Type '{0}' has multiple methods marked with [SingletonFactory]. Only one factory method is allowed per singleton type.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor PublicConstructorDescriptor = new(
        id: DiagIdPublicConstructor,
        title: "Public constructor detected",
        messageFormat: "Type '{0}' has a public constructor. The singleton pattern can be bypassed by direct instantiation. Consider making the constructor private.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NameConflictDescriptor = new(
        id: DiagIdNameConflict,
        title: "Instance property name conflicts with existing member",
        messageFormat: "The instance property name '{0}' conflicts with an existing member in type '{1}'. Use InstancePropertyName to specify a different name.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor GenericTypeDescriptor = new(
        id: DiagIdGenericType,
        title: "Generic types are not supported",
        messageFormat: "Type '{0}' is a generic type. Generic types are not supported for singleton generation.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NestedTypeDescriptor = new(
        id: DiagIdNestedType,
        title: "Nested types are not supported",
        messageFormat: "Type '{0}' is a nested type. Nested types are not supported for singleton generation.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidPropertyNameDescriptor = new(
        id: DiagIdInvalidPropertyName,
        title: "Invalid instance property name",
        messageFormat: "The instance property name '{0}' is not a valid C# identifier.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AbstractTypeDescriptor = new(
        id: DiagIdAbstractType,
        title: "Abstract types not supported for Singleton pattern",
        messageFormat: "Type '{0}' is abstract and cannot be directly instantiated. Either provide a [SingletonFactory] method or use a concrete type.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all type declarations with [Singleton] attribute
        var singletonTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Singleton.SingletonAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Generate for each type
        context.RegisterSourceOutput(singletonTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Singleton.SingletonAttribute");
            if (attr is null)
                return;

            GenerateSingletonForType(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateSingletonForType(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        // Check if type is partial
        if (!IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Check if type is a class (not struct or interface)
        if (typeSymbol.TypeKind != TypeKind.Class)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotClassDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Check for unsupported generic types
        if (typeSymbol.IsGenericType)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                GenericTypeDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Check for unsupported nested types
        if (typeSymbol.ContainingType != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NestedTypeDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Find factory method early to check if abstract types have one
        var factoryMethods = FindFactoryMethods(typeSymbol);
        if (factoryMethods.Count > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MultipleFactoriesDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var factoryMethod = factoryMethods.FirstOrDefault();

        // Check for unsupported abstract types (unless they have a factory method)
        if (typeSymbol.IsAbstract && factoryMethod is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AbstractTypeDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Parse attribute arguments
        var config = ParseSingletonConfig(attribute);

        // Validate InstancePropertyName
        if (!IsValidCSharpIdentifier(config.InstancePropertyName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidPropertyNameDescriptor,
                node.GetLocation(),
                config.InstancePropertyName ?? "(null)"));
            return;
        }

        // Check for name conflicts with existing members
        if (HasNameConflict(typeSymbol, config.InstancePropertyName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NameConflictDescriptor,
                node.GetLocation(),
                config.InstancePropertyName,
                typeSymbol.Name));
            return;
        }

        // Check for usable constructor or factory
        var hasParameterlessConstructor = HasAccessibleParameterlessConstructor(typeSymbol);
        if (!hasParameterlessConstructor && factoryMethod is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoConstructorOrFactoryDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Warn about public constructors
        if (HasPublicConstructor(typeSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PublicConstructorDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
        }

        // Create type info
        var typeInfo = new SingletonTypeInfo
        {
            TypeSymbol = typeSymbol,
            TypeName = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            IsRecordClass = typeSymbol.IsRecord,
            FactoryMethodName = factoryMethod?.Name
        };

        // Generate singleton code
        var source = GenerateSingletonCode(typeInfo, config);
        if (!string.IsNullOrEmpty(source))
        {
            // Use full type name (namespace + type) to avoid collisions when types share the same name
            var hintName = typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? $"{typeSymbol.Name}.Singleton.g.cs"
                : $"{typeSymbol.ContainingNamespace.ToDisplayString()}.{typeSymbol.Name}.Singleton.g.cs";
            context.AddSource(hintName, source);
        }
    }

    private static bool IsPartialType(SyntaxNode node)
    {
        return node switch
        {
            ClassDeclarationSyntax classDecl => classDecl.Modifiers.Any(SyntaxKind.PartialKeyword),
            RecordDeclarationSyntax recordDecl => recordDecl.Modifiers.Any(SyntaxKind.PartialKeyword),
            StructDeclarationSyntax structDecl => structDecl.Modifiers.Any(SyntaxKind.PartialKeyword),
            _ => false
        };
    }

    private static SingletonConfig ParseSingletonConfig(AttributeData attribute)
    {
        var config = new SingletonConfig();

        foreach (var named in attribute.NamedArguments)
        {
            switch (named.Key)
            {
                case "Mode":
                    var modeValue = (int)named.Value.Value!;
                    if (Enum.IsDefined(typeof(SingletonModeValue), modeValue))
                    {
                        config.Mode = (SingletonModeValue)modeValue;
                    }
                    // Invalid values default to Eager (the default)
                    break;
                case "Threading":
                    var threadingValue = (int)named.Value.Value!;
                    if (Enum.IsDefined(typeof(SingletonThreadingValue), threadingValue))
                    {
                        config.Threading = (SingletonThreadingValue)threadingValue;
                    }
                    // Invalid values default to ThreadSafe (the default)
                    break;
                case "InstancePropertyName":
                    config.InstancePropertyName = (string)named.Value.Value!;
                    break;
            }
        }

        return config;
    }

    private static bool HasNameConflict(INamedTypeSymbol typeSymbol, string propertyName)
    {
        // Normalize verbatim identifiers by stripping leading @
        var normalizedName = propertyName.StartsWith("@") ? propertyName.Substring(1) : propertyName;

        // Check for existing members with the same name in declared members
        if (typeSymbol.GetMembers(normalizedName).Length > 0)
            return true;

        // Also check inherited members by walking base types
        var baseType = typeSymbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            if (baseType.GetMembers(normalizedName).Any(m => m.DeclaredAccessibility != Accessibility.Private))
                return true;
            baseType = baseType.BaseType;
        }

        return false;
    }

    private static List<IMethodSymbol> FindFactoryMethods(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic &&
                        m.Parameters.Length == 0 &&
                        m.TypeParameters.Length == 0 && // Exclude generic methods
                        HasAttribute(m, "PatternKit.Generators.Singleton.SingletonFactoryAttribute") &&
                        SymbolEqualityComparer.Default.Equals(m.ReturnType, typeSymbol))
            .ToList();
    }

    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        return symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attributeFullName);
    }

    private static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.InstanceConstructors.Any(c => c.Parameters.Length == 0);
    }

    private static bool IsValidCSharpIdentifier(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Use Roslyn's parser for accurate C# identifier validation
        // name is guaranteed non-null here due to the check above
        var token = SyntaxFactory.ParseToken(name!);

        // Must be a valid identifier token with no trailing trivia/errors
        // and must consume the entire input
        return token.IsKind(SyntaxKind.IdentifierToken) &&
               token.Text == name &&
               !token.IsMissing;
    }

    private static bool HasPublicConstructor(INamedTypeSymbol typeSymbol)
    {
        // Check for explicit public constructors
        var hasExplicitPublicCtor = typeSymbol.InstanceConstructors.Any(c =>
            c.DeclaredAccessibility == Accessibility.Public &&
            !c.IsImplicitlyDeclared);

        if (hasExplicitPublicCtor)
            return true;

        // Check for implicit public constructor (class with no declared constructors)
        // A class with no constructors has an implicit public parameterless ctor
        var hasOnlyImplicitCtor = typeSymbol.InstanceConstructors.All(c => c.IsImplicitlyDeclared);
        if (hasOnlyImplicitCtor && typeSymbol.InstanceConstructors.Any(c =>
            c.DeclaredAccessibility == Accessibility.Public))
        {
            return true;
        }

        return false;
    }

    private string GenerateSingletonCode(SingletonTypeInfo typeInfo, SingletonConfig config)
    {
        var sb = new StringBuilder();

        // File header
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Namespace
        if (!string.IsNullOrEmpty(typeInfo.Namespace))
        {
            sb.AppendLine($"namespace {typeInfo.Namespace};");
            sb.AppendLine();
        }

        // Type declaration
        var typeKind = typeInfo.IsRecordClass ? "record class" : "class";
        sb.AppendLine($"partial {typeKind} {typeInfo.TypeName}");
        sb.AppendLine("{");

        // Generate based on mode
        var isLazy = config.Mode == SingletonModeValue.Lazy;
        var isThreadSafe = config.Threading == SingletonThreadingValue.ThreadSafe;

        var instanceCreation = typeInfo.FactoryMethodName != null
            ? $"{typeInfo.FactoryMethodName}()"
            : $"new {typeInfo.TypeName}()";

        if (isLazy)
        {
            if (isThreadSafe)
            {
                // Lazy<T> with thread-safety
                sb.AppendLine($"    private static readonly global::System.Lazy<{typeInfo.TypeName}> __PatternKit_LazyInstance =");
                sb.AppendLine($"        new global::System.Lazy<{typeInfo.TypeName}>(() => {instanceCreation});");
                sb.AppendLine();
                sb.AppendLine("    /// <summary>Gets the singleton instance of this type.</summary>");
                sb.AppendLine($"    public static {typeInfo.TypeName} {config.InstancePropertyName} => __PatternKit_LazyInstance.Value;");
            }
            else
            {
                // Non-thread-safe lazy initialization
                sb.AppendLine($"    private static {typeInfo.TypeName}? __PatternKit_Instance;");
                sb.AppendLine();
                sb.AppendLine("    /// <summary>");
                sb.AppendLine("    /// Gets the singleton instance of this type.");
                sb.AppendLine("    /// WARNING: This implementation is not thread-safe.");
                sb.AppendLine("    /// </summary>");
                sb.AppendLine($"    public static {typeInfo.TypeName} {config.InstancePropertyName} => __PatternKit_Instance ??= {instanceCreation};");
            }
        }
        else
        {
            // Eager initialization
            sb.AppendLine($"    private static readonly {typeInfo.TypeName} __PatternKit_Instance = {instanceCreation};");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Gets the singleton instance of this type.</summary>");
            sb.AppendLine($"    public static {typeInfo.TypeName} {config.InstancePropertyName} => __PatternKit_Instance;");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // Helper classes and enums

    /// <summary>
    /// Internal enum mirroring SingletonMode from abstractions.
    /// </summary>
    private enum SingletonModeValue
    {
        Eager = 0,
        Lazy = 1
    }

    /// <summary>
    /// Internal enum mirroring SingletonThreading from abstractions.
    /// </summary>
    private enum SingletonThreadingValue
    {
        ThreadSafe = 0,
        SingleThreadedFast = 1
    }

    private class SingletonConfig
    {
        public SingletonModeValue Mode { get; set; } = SingletonModeValue.Eager;
        public SingletonThreadingValue Threading { get; set; } = SingletonThreadingValue.ThreadSafe;
        public string InstancePropertyName { get; set; } = "Instance";
    }

    private class SingletonTypeInfo
    {
        public INamedTypeSymbol TypeSymbol { get; set; } = null!;
        public string TypeName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public bool IsRecordClass { get; set; }
        public string? FactoryMethodName { get; set; }
    }
}
