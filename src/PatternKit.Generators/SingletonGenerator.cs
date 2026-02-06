using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Singleton pattern.
/// Generates a static Instance property that provides a single shared instance of the decorated type.
/// Supports eager and lazy initialization with configurable threading models.
/// </summary>
[Generator]
public sealed class SingletonGenerator : IIncrementalGenerator
{
    private const string DiagIdTypeNotPartial = "PKSNG001";
    private const string DiagIdNotClass = "PKSNG002";
    private const string DiagIdNoCtorOrFactory = "PKSNG003";
    private const string DiagIdMultipleFactories = "PKSNG004";
    private const string DiagIdPublicCtor = "PKSNG005";
    private const string DiagIdNameConflict = "PKSNG006";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [Singleton] must be partial",
        messageFormat: "Type '{0}' is marked with [Singleton] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NotClassDescriptor = new(
        id: DiagIdNotClass,
        title: "Type marked with [Singleton] must be a class",
        messageFormat: "Type '{0}' is marked with [Singleton] but is not a class. Singleton can only be applied to classes.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoCtorOrFactoryDescriptor = new(
        id: DiagIdNoCtorOrFactory,
        title: "No parameterless constructor or [SingletonFactory] method found",
        messageFormat: "Type '{0}' has [Singleton] but no accessible parameterless constructor or static method marked with [SingletonFactory].",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleFactoriesDescriptor = new(
        id: DiagIdMultipleFactories,
        title: "Multiple [SingletonFactory] methods found",
        messageFormat: "Type '{0}' has multiple methods marked with [SingletonFactory]. Only one factory method is allowed.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor PublicCtorDescriptor = new(
        id: DiagIdPublicCtor,
        title: "Singleton type has public constructor",
        messageFormat: "Type '{0}' has a public constructor. Consider making constructors private or internal to prevent external instantiation.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NameConflictDescriptor = new(
        id: DiagIdNameConflict,
        title: "Instance property name conflicts with existing member",
        messageFormat: "Type '{0}' already has a member named '{1}'. Choose a different InstancePropertyName.",
        category: "PatternKit.Generators.Singleton",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Singleton.SingletonAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(targets, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Singleton.SingletonAttribute");
            if (attr is null)
                return;

            GenerateSingleton(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateSingleton(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        // PKSNG002: Must be a class
        if (typeSymbol.TypeKind != TypeKind.Class)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotClassDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // PKSNG001: Must be partial
        if (!GeneratorUtilities.IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Parse config
        var config = ParseConfig(attribute);

        // PKSNG006: Check name conflict
        var existingMember = typeSymbol.GetMembers(config.InstancePropertyName).FirstOrDefault();
        if (existingMember is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NameConflictDescriptor,
                node.GetLocation(),
                typeSymbol.Name,
                config.InstancePropertyName));
            return;
        }

        // Collect factory methods
        var factoryMethods = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Singleton.SingletonFactoryAttribute"))
            .ToList();

        // PKSNG004: Multiple factories
        if (factoryMethods.Count > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MultipleFactoriesDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var factoryMethod = factoryMethods.FirstOrDefault();

        // Check for parameterless constructor
        var hasParameterlessCtor = typeSymbol.InstanceConstructors
            .Any(c => c.Parameters.Length == 0 && !c.IsImplicitlyDeclared == false || c.Parameters.Length == 0);

        // Actually, check more carefully:
        // We need either a parameterless ctor (explicit or implicit) or a factory method
        var hasAccessibleParameterlessCtor = typeSymbol.InstanceConstructors
            .Any(c => c.Parameters.Length == 0);

        // PKSNG003: No ctor or factory
        if (factoryMethod is null && !hasAccessibleParameterlessCtor)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoCtorOrFactoryDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // PKSNG005: Public constructor warning
        var hasPublicCtor = typeSymbol.InstanceConstructors
            .Any(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsImplicitlyDeclared);
        if (hasPublicCtor)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PublicCtorDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
        }

        // Generate
        var source = GenerateSource(typeSymbol, config, factoryMethod);
        var fileName = $"{typeSymbol.Name}.Singleton.g.cs";
        context.AddSource(fileName, source);
    }

    private static SingletonConfig ParseConfig(AttributeData attribute)
    {
        var config = new SingletonConfig();

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "Mode":
                    config.Mode = namedArg.Value.Value is int m ? m : 0;
                    break;
                case "Threading":
                    config.Threading = namedArg.Value.Value is int t ? t : 0;
                    break;
                case "InstancePropertyName":
                    config.InstancePropertyName = namedArg.Value.Value?.ToString() ?? "Instance";
                    break;
            }
        }

        return config;
    }

    private static string GenerateSource(
        INamedTypeSymbol typeSymbol,
        SingletonConfig config,
        IMethodSymbol factoryMethod)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;
        var fullyQualifiedName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var propertyName = config.InstancePropertyName;

        var creationExpr = factoryMethod is not null
            ? $"{factoryMethod.Name}()"
            : $"new {typeName}()";

        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial class {typeName}");
        sb.AppendLine("{");

        if (config.Mode == 0) // Eager
        {
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Gets the singleton instance of <see cref=\"{typeName}\"/>.");
            sb.AppendLine($"    /// The instance is created eagerly when the type is first loaded.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static {typeName} {propertyName} {{ get; }} = {creationExpr};");
        }
        else // Lazy
        {
            var threadSafetyMode = config.Threading == 0
                ? "System.Threading.LazyThreadSafetyMode.ExecutionAndPublication"
                : "System.Threading.LazyThreadSafetyMode.None";

            sb.AppendLine($"    private static readonly System.Lazy<{typeName}> _instance = new System.Lazy<{typeName}>(() => {creationExpr}, {threadSafetyMode});");
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Gets the singleton instance of <see cref=\"{typeName}\"/>.");
            sb.AppendLine($"    /// The instance is created lazily on first access.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static {typeName} {propertyName} => _instance.Value;");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private class SingletonConfig
    {
        public int Mode { get; set; } // 0 = Eager, 1 = Lazy
        public int Threading { get; set; } // 0 = ThreadSafe, 1 = SingleThreadedFast
        public string InstancePropertyName { get; set; } = "Instance";
    }
}
