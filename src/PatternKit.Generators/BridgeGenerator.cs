using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Bridge pattern.
/// Generates a protected constructor, implementor property, and forwarding methods
/// on an abstraction class that delegates to an implementor contract.
/// </summary>
[Generator]
public sealed class BridgeGenerator : IIncrementalGenerator
{
    private const string DiagIdTypeNotPartial = "PKBRG001";
    private const string DiagIdImplementorNotAbstract = "PKBRG002";
    private const string DiagIdUnsupportedMember = "PKBRG003";
    private const string DiagIdNameConflict = "PKBRG004";
    private const string DiagIdInaccessibleMember = "PKBRG005";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [BridgeAbstraction] must be partial",
        messageFormat: "Type '{0}' is marked with [BridgeAbstraction] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Bridge",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ImplementorNotAbstractDescriptor = new(
        id: DiagIdImplementorNotAbstract,
        title: "Implementor type must be an interface or abstract class",
        messageFormat: "Type '{0}' specified as implementor is not an interface or abstract class.",
        category: "PatternKit.Generators.Bridge",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedMemberDescriptor = new(
        id: DiagIdUnsupportedMember,
        title: "Unsupported member kind for bridge generation",
        messageFormat: "Member '{0}' of kind '{1}' is not supported in bridge generation. Only methods and properties are supported.",
        category: "PatternKit.Generators.Bridge",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NameConflictDescriptor = new(
        id: DiagIdNameConflict,
        title: "Name conflict in generated bridge code",
        messageFormat: "Generated member name '{0}' conflicts with an existing member in type '{1}'.",
        category: "PatternKit.Generators.Bridge",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InaccessibleMemberDescriptor = new(
        id: DiagIdInaccessibleMember,
        title: "Member is not accessible for bridge generation",
        messageFormat: "Member '{0}' on the implementor cannot be forwarded because it is not accessible.",
        category: "PatternKit.Generators.Bridge",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Bridge.BridgeAbstractionAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(targets, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Bridge.BridgeAbstractionAttribute");
            if (attr is null)
                return;

            GenerateBridge(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateBridge(
        SourceProductionContext context,
        INamedTypeSymbol abstractionSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        // 1. Validate partial
        if (!GeneratorUtilities.IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor, node.GetLocation(), abstractionSymbol.Name));
            return;
        }

        // 2. Extract implementor type from constructor arg
        if (attribute.ConstructorArguments.Length == 0 ||
            attribute.ConstructorArguments[0].Value is not INamedTypeSymbol implementorSymbol)
            return;

        // 3. Validate implementor is interface or abstract class
        var isInterface = implementorSymbol.TypeKind == TypeKind.Interface;
        var isAbstractClass = implementorSymbol.TypeKind == TypeKind.Class && implementorSymbol.IsAbstract;
        if (!isInterface && !isAbstractClass)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ImplementorNotAbstractDescriptor, node.GetLocation(), implementorSymbol.Name));
            return;
        }

        // 4. Parse config
        var config = ParseConfig(attribute, implementorSymbol);

        // 5. Check for property name conflict
        var existingMembers = new System.Collections.Generic.HashSet<string>(abstractionSymbol.GetMembers().Select(m => m.Name));
        if (existingMembers.Contains(config.ImplementorPropertyName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NameConflictDescriptor, node.GetLocation(),
                config.ImplementorPropertyName, abstractionSymbol.Name));
            return;
        }

        // 6. Collect members to forward
        var members = CollectMembers(implementorSymbol, isInterface, context);
        if (members is null)
            return; // errors reported

        // 7. Check for forwarding name conflicts
        foreach (var m in members)
        {
            if (existingMembers.Contains(m.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NameConflictDescriptor, node.GetLocation(),
                    m.Name, abstractionSymbol.Name));
                return;
            }
        }

        // 8. Generate main bridge file
        var source = EmitBridge(abstractionSymbol, implementorSymbol, config, members);
        context.AddSource($"{abstractionSymbol.Name}.Bridge.g.cs", source);

        // 9. Generate default implementor if requested
        if (config.GenerateDefault)
        {
            var defaultSource = EmitDefault(abstractionSymbol, implementorSymbol, config, members);
            context.AddSource($"{abstractionSymbol.Name}.Bridge.Default.g.cs", defaultSource);
        }
    }

    private BridgeConfig ParseConfig(AttributeData attribute, INamedTypeSymbol implementorSymbol)
    {
        var config = new BridgeConfig
        {
            ImplementorPropertyName = "Implementor",
            GenerateDefault = false,
            DefaultTypeName = null
        };

        foreach (var named in attribute.NamedArguments)
        {
            switch (named.Key)
            {
                case "ImplementorPropertyName":
                    if (named.Value.Value is string propName && !string.IsNullOrWhiteSpace(propName))
                        config.ImplementorPropertyName = propName;
                    break;
                case "GenerateDefault":
                    config.GenerateDefault = named.Value.Value is bool b && b;
                    break;
                case "DefaultTypeName":
                    config.DefaultTypeName = named.Value.Value as string;
                    break;
            }
        }

        if (config.GenerateDefault && string.IsNullOrWhiteSpace(config.DefaultTypeName))
        {
            var baseName = implementorSymbol.Name;
            if (baseName.StartsWith("I") && baseName.Length > 1 && char.IsUpper(baseName[1]))
                baseName = baseName.Substring(1);
            config.DefaultTypeName = $"Default{baseName}";
        }

        return config;
    }

    private List<BridgeMember> CollectMembers(
        INamedTypeSymbol implementorSymbol,
        bool isInterface,
        SourceProductionContext context)
    {
        var members = new List<BridgeMember>();
        var hasErrors = false;
        var allMembers = GetAllMembers(implementorSymbol, isInterface);

        foreach (var member in allMembers)
        {
            if (GeneratorUtilities.HasAttribute(member,"PatternKit.Generators.Bridge.BridgeIgnoreAttribute"))
                continue;

            if (member is IMethodSymbol method)
            {
                if (method.MethodKind != MethodKind.Ordinary)
                    continue;
                if (method.IsStatic)
                    continue;

                if (!IsAccessible(method.DeclaredAccessibility))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InaccessibleMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        member.Name));
                    continue;
                }

                members.Add(new BridgeMember
                {
                    Kind = BridgeMemberKind.Method,
                    Name = method.Name,
                    ReturnType = method.ReturnType.ToDisplayString(GeneratorUtilities.TypeFormat),
                    IsVoid = method.ReturnsVoid,
                    Parameters = method.Parameters.Select(p => new BridgeParam
                    {
                        Name = p.Name,
                        Type = p.Type.ToDisplayString(GeneratorUtilities.TypeFormat),
                        RefKind = p.RefKind,
                        HasDefaultValue = p.HasExplicitDefaultValue,
                        DefaultValue = p.HasExplicitDefaultValue
                            ? GeneratorUtilities.FormatDefaultValue(p)
                            : null
                    }).ToList()
                });
            }
            else if (member is IPropertySymbol prop)
            {
                if (prop.IsStatic || prop.IsIndexer)
                    continue;

                if (!IsAccessible(prop.DeclaredAccessibility))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InaccessibleMemberDescriptor,
                        member.Locations.FirstOrDefault() ?? Location.None,
                        member.Name));
                    continue;
                }

                members.Add(new BridgeMember
                {
                    Kind = BridgeMemberKind.Property,
                    Name = prop.Name,
                    ReturnType = prop.Type.ToDisplayString(GeneratorUtilities.TypeFormat),
                    HasGetter = prop.GetMethod is not null,
                    HasSetter = prop.SetMethod is not null
                });
            }
            else if (member is IEventSymbol)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedMemberDescriptor,
                    member.Locations.FirstOrDefault() ?? Location.None,
                    member.Name, "Event"));
                hasErrors = true;
            }
        }

        if (hasErrors)
            return null;

        return members.OrderBy(m => m.Kind == BridgeMemberKind.Property ? 1 : 0)
                      .ThenBy(m => m.Name, System.StringComparer.Ordinal)
                      .ToList();
    }

    private IEnumerable<ISymbol> GetAllMembers(INamedTypeSymbol type, bool isInterface)
    {
        var seen = new HashSet<string>();
        if (isInterface)
        {
            foreach (var m in type.GetMembers())
                if (seen.Add(GetSig(m))) yield return m;
            foreach (var iface in type.AllInterfaces)
                foreach (var m in iface.GetMembers())
                    if (seen.Add(GetSig(m))) yield return m;
        }
        else
        {
            var current = type;
            while (current is not null && current.SpecialType != SpecialType.System_Object)
            {
                foreach (var m in current.GetMembers())
                    if (seen.Add(GetSig(m))) yield return m;
                current = current.BaseType;
            }
        }
    }

    private static string GetSig(ISymbol s)
    {
        var sb = new StringBuilder();
        sb.Append(s.Kind).Append('_').Append(s.Name);
        if (s is IMethodSymbol m)
        {
            sb.Append('(');
            for (int i = 0; i < m.Parameters.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(m.Parameters[i].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
            sb.Append(')');
        }
        return sb.ToString();
    }

    private static bool IsAccessible(Accessibility a)
    {
        return a == Accessibility.Public ||
               a == Accessibility.Internal ||
               a == Accessibility.ProtectedOrInternal;
    }

    private string EmitBridge(
        INamedTypeSymbol abstraction,
        INamedTypeSymbol implementor,
        BridgeConfig config,
        List<BridgeMember> members)
    {
        var ns = abstraction.ContainingNamespace.IsGlobalNamespace
            ? null
            : abstraction.ContainingNamespace.ToDisplayString();
        var implFullName = implementor.ToDisplayString(GeneratorUtilities.TypeFormat);
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"/// <summary>Bridge abstraction forwarding to {implementor.Name}.</summary>");
        sb.AppendLine($"partial class {abstraction.Name}");
        sb.AppendLine("{");

        // Constructor
        sb.AppendLine($"    /// <summary>Initializes the abstraction with an implementor instance.</summary>");
        sb.AppendLine($"    protected {abstraction.Name}({implFullName} implementor)");
        sb.AppendLine("    {");
        sb.AppendLine($"        {config.ImplementorPropertyName} = implementor ?? throw new System.ArgumentNullException(nameof(implementor));");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Property
        sb.AppendLine($"    /// <summary>Gets the implementor instance.</summary>");
        sb.AppendLine($"    protected {implFullName} {config.ImplementorPropertyName} {{ get; }}");
        sb.AppendLine();

        // Forwarding members
        foreach (var m in members)
        {
            if (m.Kind == BridgeMemberKind.Method)
                EmitMethod(sb, m, config);
            else
                EmitProperty(sb, m, config);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private void EmitMethod(StringBuilder sb, BridgeMember m, BridgeConfig config)
    {
        sb.AppendLine($"    /// <summary>Forwards to {config.ImplementorPropertyName}.{m.Name}.</summary>");
        sb.Append($"    protected {m.ReturnType} {m.Name}(");

        var paramDecl = string.Join(", ", m.Parameters.Select(p =>
        {
            var refMod = p.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => ""
            };
            var defVal = p.HasDefaultValue ? $" = {p.DefaultValue}" : "";
            return $"{refMod}{p.Type} {p.Name}{defVal}";
        }));
        sb.Append(paramDecl);
        sb.AppendLine(")");

        var argList = string.Join(", ", m.Parameters.Select(p =>
        {
            var refMod = p.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => ""
            };
            return $"{refMod}{p.Name}";
        }));

        if (m.IsVoid)
        {
            sb.AppendLine("    {");
            sb.AppendLine($"        {config.ImplementorPropertyName}.{m.Name}({argList});");
            sb.AppendLine("    }");
        }
        else
        {
            sb.AppendLine($"        => {config.ImplementorPropertyName}.{m.Name}({argList});");
        }
        sb.AppendLine();
    }

    private void EmitProperty(StringBuilder sb, BridgeMember m, BridgeConfig config)
    {
        sb.AppendLine($"    /// <summary>Forwards to {config.ImplementorPropertyName}.{m.Name}.</summary>");

        if (m.HasGetter && m.HasSetter)
        {
            sb.AppendLine($"    protected {m.ReturnType} {m.Name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => {config.ImplementorPropertyName}.{m.Name};");
            sb.AppendLine($"        set => {config.ImplementorPropertyName}.{m.Name} = value;");
            sb.AppendLine("    }");
        }
        else if (m.HasGetter)
        {
            sb.AppendLine($"    protected {m.ReturnType} {m.Name}");
            sb.AppendLine($"        => {config.ImplementorPropertyName}.{m.Name};");
        }
        else if (m.HasSetter)
        {
            sb.AppendLine($"    protected {m.ReturnType} {m.Name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        set => {config.ImplementorPropertyName}.{m.Name} = value;");
            sb.AppendLine("    }");
        }
        sb.AppendLine();
    }

    private string EmitDefault(
        INamedTypeSymbol abstraction,
        INamedTypeSymbol implementor,
        BridgeConfig config,
        List<BridgeMember> members)
    {
        var ns = abstraction.ContainingNamespace.IsGlobalNamespace
            ? null
            : abstraction.ContainingNamespace.ToDisplayString();
        var implFullName = implementor.ToDisplayString(GeneratorUtilities.TypeFormat);
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"/// <summary>Default (no-op) implementor for {implementor.Name}.</summary>");
        sb.AppendLine($"public sealed class {config.DefaultTypeName} : {implFullName}");
        sb.AppendLine("{");

        foreach (var m in members)
        {
            if (m.Kind == BridgeMemberKind.Method)
            {
                sb.Append($"    public {m.ReturnType} {m.Name}(");
                sb.Append(string.Join(", ", m.Parameters.Select(p =>
                {
                    var refMod = p.RefKind switch
                    {
                        RefKind.Ref => "ref ",
                        RefKind.Out => "out ",
                        RefKind.In => "in ",
                        _ => ""
                    };
                    return $"{refMod}{p.Type} {p.Name}";
                })));
                sb.AppendLine(")");

                if (m.IsVoid)
                {
                    sb.AppendLine("    {");
                    // Assign out parameters
                    foreach (var p in m.Parameters.Where(p => p.RefKind == RefKind.Out))
                    {
                        sb.AppendLine($"        {p.Name} = default!;");
                    }
                    sb.AppendLine("    }");
                }
                else
                {
                    sb.AppendLine("        => default!;");
                }
                sb.AppendLine();
            }
            else if (m.Kind == BridgeMemberKind.Property)
            {
                if (m.HasGetter && m.HasSetter)
                {
                    sb.AppendLine($"    public {m.ReturnType} {m.Name} {{ get; set; }} = default!;");
                }
                else if (m.HasGetter)
                {
                    sb.AppendLine($"    public {m.ReturnType} {m.Name} => default!;");
                }
                else if (m.HasSetter)
                {
                    sb.AppendLine($"    public {m.ReturnType} {m.Name} {{ set {{ }} }}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    // Helper types
    private class BridgeConfig
    {
        public string ImplementorPropertyName { get; set; } = "Implementor";
        public bool GenerateDefault { get; set; }
        public string DefaultTypeName { get; set; } = null!;
    }

    private class BridgeMember
    {
        public BridgeMemberKind Kind { get; set; }
        public string Name { get; set; } = "";
        public string ReturnType { get; set; } = "";
        public bool IsVoid { get; set; }
        public List<BridgeParam> Parameters { get; set; } = new List<BridgeParam>();
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
    }

    private class BridgeParam
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public RefKind RefKind { get; set; }
        public bool HasDefaultValue { get; set; }
        public string DefaultValue { get; set; } = null!;
    }

    private enum BridgeMemberKind
    {
        Method,
        Property
    }
}
