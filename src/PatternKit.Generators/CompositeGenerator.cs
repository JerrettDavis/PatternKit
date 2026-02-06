using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Composite pattern.
/// Generates a ComponentBase (leaf with default no-op implementations) and
/// a CompositeBase (child management and delegation to children) from a component contract.
/// </summary>
[Generator]
public sealed class CompositeGenerator : IIncrementalGenerator
{
    private const string DiagIdTypeNotPartial = "PKCPS001";
    private const string DiagIdNotInterfaceOrAbstract = "PKCPS002";
    private const string DiagIdNameConflict = "PKCPS003";
    private const string DiagIdUnsupportedMember = "PKCPS004";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [CompositeComponent] should be partial for extensibility",
        messageFormat: "Type '{0}' is marked with [CompositeComponent] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Composite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NotInterfaceOrAbstractDescriptor = new(
        id: DiagIdNotInterfaceOrAbstract,
        title: "Component type must be an interface or abstract class",
        messageFormat: "Type '{0}' is not an interface or abstract class. [CompositeComponent] requires an interface or abstract class.",
        category: "PatternKit.Generators.Composite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NameConflictDescriptor = new(
        id: DiagIdNameConflict,
        title: "Name conflict for generated composite types",
        messageFormat: "Generated type name '{0}' conflicts with an existing type. Use ComponentBaseName or CompositeBaseName to specify a different name.",
        category: "PatternKit.Generators.Composite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedMemberDescriptor = new(
        id: DiagIdUnsupportedMember,
        title: "Unsupported member kind for composite generation",
        messageFormat: "Member '{0}' of kind '{1}' is not supported in composite generation. Only methods and properties are supported.",
        category: "PatternKit.Generators.Composite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Composite.CompositeComponentAttribute",
            predicate: static (node, _) => node is InterfaceDeclarationSyntax or ClassDeclarationSyntax or RecordDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(targets, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol componentSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Composite.CompositeComponentAttribute");
            if (attr is null)
                return;

            GenerateComposite(spc, componentSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateComposite(
        SourceProductionContext context,
        INamedTypeSymbol componentSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        // 1. Validate interface or abstract class
        var isInterface = componentSymbol.TypeKind == TypeKind.Interface;
        var isAbstractClass = componentSymbol.TypeKind == TypeKind.Class && componentSymbol.IsAbstract;
        if (!isInterface && !isAbstractClass)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotInterfaceOrAbstractDescriptor, node.GetLocation(), componentSymbol.Name));
            return;
        }

        // 2. Validate partial (needed for user extension of generated code)
        if (!GeneratorUtilities.IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor, node.GetLocation(), componentSymbol.Name));
            return;
        }

        // 3. Parse config
        var config = ParseConfig(attribute, componentSymbol);

        // 4. Check name conflicts
        var ns = componentSymbol.ContainingNamespace;
        if (ns.GetTypeMembers(config.ComponentBaseName).Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NameConflictDescriptor, node.GetLocation(), config.ComponentBaseName));
            return;
        }
        if (ns.GetTypeMembers(config.CompositeBaseName).Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NameConflictDescriptor, node.GetLocation(), config.CompositeBaseName));
            return;
        }

        // 5. Collect members
        var members = CollectMembers(componentSymbol, isInterface, context);
        if (members is null)
            return;

        // 6. Emit main composite file
        var source = EmitComposite(componentSymbol, config, members, isInterface);
        context.AddSource($"{componentSymbol.Name}.Composite.g.cs", source);

        // 7. Emit traversal helpers if requested
        if (config.GenerateTraversalHelpers)
        {
            var traversalSource = EmitTraversal(componentSymbol, config);
            context.AddSource($"{componentSymbol.Name}.Composite.Traversal.g.cs", traversalSource);
        }
    }

    private CompositeConfig ParseConfig(AttributeData attribute, INamedTypeSymbol componentSymbol)
    {
        var baseName = componentSymbol.Name;
        if (baseName.StartsWith("I") && baseName.Length > 1 && char.IsUpper(baseName[1]))
            baseName = baseName.Substring(1);

        var config = new CompositeConfig
        {
            ComponentBaseName = $"{baseName}Base",
            CompositeBaseName = $"{baseName}Composite",
            ChildrenPropertyName = "Children",
            Storage = 0, // List
            GenerateTraversalHelpers = false
        };

        foreach (var named in attribute.NamedArguments)
        {
            switch (named.Key)
            {
                case "ComponentBaseName":
                    if (named.Value.Value is string cbn && !string.IsNullOrWhiteSpace(cbn))
                        config.ComponentBaseName = cbn;
                    break;
                case "CompositeBaseName":
                    if (named.Value.Value is string csn && !string.IsNullOrWhiteSpace(csn))
                        config.CompositeBaseName = csn;
                    break;
                case "ChildrenPropertyName":
                    if (named.Value.Value is string cpn && !string.IsNullOrWhiteSpace(cpn))
                        config.ChildrenPropertyName = cpn;
                    break;
                case "Storage":
                    config.Storage = (int)named.Value.Value!;
                    break;
                case "GenerateTraversalHelpers":
                    config.GenerateTraversalHelpers = named.Value.Value is bool b && b;
                    break;
            }
        }

        return config;
    }

    private List<CompositeMember> CollectMembers(
        INamedTypeSymbol componentSymbol,
        bool isInterface,
        SourceProductionContext context)
    {
        var members = new List<CompositeMember>();
        var hasErrors = false;
        var allMembers = GetAllMembers(componentSymbol, isInterface);

        foreach (var member in allMembers)
        {
            var isIgnored = GeneratorUtilities.HasAttribute(member, "PatternKit.Generators.Composite.CompositeIgnoreAttribute");

            if (member is IMethodSymbol method)
            {
                if (method.MethodKind != MethodKind.Ordinary)
                    continue;
                if (method.IsStatic)
                    continue;

                members.Add(new CompositeMember
                {
                    Kind = CompositeMemberKind.Method,
                    Name = method.Name,
                    ReturnType = method.ReturnType.ToDisplayString(GeneratorUtilities.TypeFormat),
                    IsVoid = method.ReturnsVoid,
                    IsIgnored = isIgnored,
                    Parameters = method.Parameters.Select(p => new CompositeParam
                    {
                        Name = p.Name,
                        Type = p.Type.ToDisplayString(GeneratorUtilities.TypeFormat),
                        RefKind = p.RefKind,
                        HasDefaultValue = p.HasExplicitDefaultValue,
                        DefaultValue = p.HasExplicitDefaultValue ? GeneratorUtilities.FormatDefaultValue(p) : null
                    }).ToList()
                });
            }
            else if (member is IPropertySymbol prop)
            {
                if (prop.IsStatic || prop.IsIndexer)
                    continue;

                members.Add(new CompositeMember
                {
                    Kind = CompositeMemberKind.Property,
                    Name = prop.Name,
                    ReturnType = prop.Type.ToDisplayString(GeneratorUtilities.TypeFormat),
                    HasGetter = prop.GetMethod is not null,
                    HasSetter = prop.SetMethod is not null,
                    IsIgnored = isIgnored
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

        return members.OrderBy(m => m.Kind == CompositeMemberKind.Property ? 1 : 0)
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

    private string EmitComposite(
        INamedTypeSymbol componentSymbol,
        CompositeConfig config,
        List<CompositeMember> members,
        bool isInterface)
    {
        var ns = componentSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : componentSymbol.ContainingNamespace.ToDisplayString();
        var componentFullName = componentSymbol.ToDisplayString(GeneratorUtilities.TypeFormat);
        var accessibility = GeneratorUtilities.GetAccessibility(componentSymbol.DeclaredAccessibility);
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        // ---- ComponentBase (leaf) ----
        sb.AppendLine($"/// <summary>Abstract leaf base for {componentSymbol.Name}. Provides default no-op implementations.</summary>");
        if (isInterface)
            sb.AppendLine($"{accessibility} abstract partial class {config.ComponentBaseName} : {componentFullName}");
        else
            sb.AppendLine($"{accessibility} abstract partial class {config.ComponentBaseName} : {componentFullName}");
        sb.AppendLine("{");

        foreach (var m in members)
        {
            if (m.Kind == CompositeMemberKind.Method)
                EmitLeafMethod(sb, m, isInterface);
            else
                EmitLeafProperty(sb, m, isInterface);
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // ---- CompositeBase ----
        var useImmutable = config.Storage == 1;
        var childType = useImmutable
            ? $"System.Collections.Immutable.ImmutableArray<{componentFullName}>"
            : $"System.Collections.Generic.List<{componentFullName}>";
        var childInit = useImmutable
            ? $"System.Collections.Immutable.ImmutableArray<{componentFullName}>.Empty"
            : $"new System.Collections.Generic.List<{componentFullName}>()";

        sb.AppendLine($"/// <summary>Composite base for {componentSymbol.Name}. Manages children and delegates operations.</summary>");
        if (isInterface)
            sb.AppendLine($"{accessibility} abstract partial class {config.CompositeBaseName} : {componentFullName}");
        else
            sb.AppendLine($"{accessibility} abstract partial class {config.CompositeBaseName} : {componentFullName}");
        sb.AppendLine("{");

        // Children property
        sb.AppendLine($"    /// <summary>Gets the children of this composite.</summary>");
        if (useImmutable)
        {
            sb.AppendLine($"    public {childType} {config.ChildrenPropertyName} {{ get; private set; }} = {childInit};");
        }
        else
        {
            sb.AppendLine($"    private readonly {childType} _{GeneratorUtilities.ToCamelCase(config.ChildrenPropertyName)} = {childInit};");
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>Gets the children as a read-only list.</summary>");
            sb.AppendLine($"    public System.Collections.Generic.IReadOnlyList<{componentFullName}> {config.ChildrenPropertyName} => _{GeneratorUtilities.ToCamelCase(config.ChildrenPropertyName)};");
        }
        sb.AppendLine();

        // Add / Remove
        if (useImmutable)
        {
            sb.AppendLine($"    /// <summary>Returns a new composite with the child added.</summary>");
            sb.AppendLine($"    public void Add({componentFullName} child)");
            sb.AppendLine("    {");
            sb.AppendLine($"        {config.ChildrenPropertyName} = {config.ChildrenPropertyName}.Add(child ?? throw new System.ArgumentNullException(nameof(child)));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>Returns a new composite with the child removed.</summary>");
            sb.AppendLine($"    public void Remove({componentFullName} child)");
            sb.AppendLine("    {");
            sb.AppendLine($"        {config.ChildrenPropertyName} = {config.ChildrenPropertyName}.Remove(child ?? throw new System.ArgumentNullException(nameof(child)));");
            sb.AppendLine("    }");
        }
        else
        {
            sb.AppendLine($"    /// <summary>Adds a child component.</summary>");
            sb.AppendLine($"    public void Add({componentFullName} child)");
            sb.AppendLine("    {");
            sb.AppendLine($"        _{GeneratorUtilities.ToCamelCase(config.ChildrenPropertyName)}.Add(child ?? throw new System.ArgumentNullException(nameof(child)));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>Removes a child component.</summary>");
            sb.AppendLine($"    public void Remove({componentFullName} child)");
            sb.AppendLine("    {");
            sb.AppendLine($"        _{GeneratorUtilities.ToCamelCase(config.ChildrenPropertyName)}.Remove(child ?? throw new System.ArgumentNullException(nameof(child)));");
            sb.AppendLine("    }");
        }
        sb.AppendLine();

        // Forwarding members that iterate children
        foreach (var m in members)
        {
            if (m.Kind == CompositeMemberKind.Method)
                EmitCompositeMethod(sb, m, config, componentFullName, useImmutable, isInterface);
            else
                EmitCompositeProperty(sb, m, config, componentFullName, useImmutable, isInterface);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private void EmitLeafMethod(StringBuilder sb, CompositeMember m, bool isInterface)
    {
        if (m.IsIgnored)
        {
            sb.AppendLine($"    /// <summary>Excluded from composite delegation. Must be implemented by subclasses.</summary>");
            sb.Append($"    public abstract {m.ReturnType} {m.Name}(");
            sb.Append(FormatParamDecl(m.Parameters));
            sb.AppendLine(");");
            sb.AppendLine();
            return;
        }

        var modifier = isInterface ? "public virtual" : "public override";
        sb.AppendLine($"    /// <summary>Default no-op for {m.Name}.</summary>");
        sb.Append($"    {modifier} {m.ReturnType} {m.Name}(");
        sb.Append(FormatParamDecl(m.Parameters));
        sb.AppendLine(")");

        if (m.IsVoid)
        {
            sb.AppendLine("    {");
            foreach (var p in m.Parameters.Where(p => p.RefKind == RefKind.Out))
                sb.AppendLine($"        {p.Name} = default!;");
            sb.AppendLine("    }");
        }
        else
        {
            sb.AppendLine("        => default!;");
        }
        sb.AppendLine();
    }

    private void EmitLeafProperty(StringBuilder sb, CompositeMember m, bool isInterface)
    {
        if (m.IsIgnored)
        {
            sb.AppendLine($"    /// <summary>Excluded from composite delegation. Must be implemented by subclasses.</summary>");
            if (m.HasGetter && m.HasSetter)
                sb.AppendLine($"    public abstract {m.ReturnType} {m.Name} {{ get; set; }}");
            else if (m.HasGetter)
                sb.AppendLine($"    public abstract {m.ReturnType} {m.Name} {{ get; }}");
            else if (m.HasSetter)
                sb.AppendLine($"    public abstract {m.ReturnType} {m.Name} {{ set; }}");
            sb.AppendLine();
            return;
        }

        var modifier = isInterface ? "public virtual" : "public override";
        sb.AppendLine($"    /// <summary>Default implementation for {m.Name}.</summary>");
        if (m.HasGetter && m.HasSetter)
            sb.AppendLine($"    {modifier} {m.ReturnType} {m.Name} {{ get; set; }} = default!;");
        else if (m.HasGetter)
            sb.AppendLine($"    {modifier} {m.ReturnType} {m.Name} => default!;");
        else if (m.HasSetter)
            sb.AppendLine($"    {modifier} {m.ReturnType} {m.Name} {{ set {{ }} }}");
        sb.AppendLine();
    }

    private void EmitCompositeMethod(StringBuilder sb, CompositeMember m, CompositeConfig config,
        string componentFullName, bool useImmutable, bool isInterface)
    {
        if (m.IsIgnored)
        {
            sb.AppendLine($"    /// <summary>Excluded from composite delegation. Must be implemented by subclasses.</summary>");
            sb.Append($"    public abstract {m.ReturnType} {m.Name}(");
            sb.Append(FormatParamDecl(m.Parameters));
            sb.AppendLine(");");
            sb.AppendLine();
            return;
        }

        var modifier = isInterface ? "public virtual" : "public override";
        sb.AppendLine($"    /// <summary>Delegates {m.Name} to all children.</summary>");
        sb.Append($"    {modifier} {m.ReturnType} {m.Name}(");
        sb.Append(FormatParamDecl(m.Parameters));
        sb.AppendLine(")");
        sb.AppendLine("    {");

        var childrenExpr = useImmutable
            ? config.ChildrenPropertyName
            : $"_{GeneratorUtilities.ToCamelCase(config.ChildrenPropertyName)}";

        var argList = FormatArgList(m.Parameters);

        if (m.IsVoid)
        {
            sb.AppendLine($"        for (int __i = 0; __i < {childrenExpr}.Count; __i++)");
            sb.AppendLine("        {");
            sb.AppendLine($"            {childrenExpr}[__i].{m.Name}({argList});");
            sb.AppendLine("        }");
        }
        else
        {
            // For non-void, return the last child's result (common composite convention)
            sb.AppendLine($"        {m.ReturnType} __result = default!;");
            sb.AppendLine($"        for (int __i = 0; __i < {childrenExpr}.Count; __i++)");
            sb.AppendLine("        {");
            sb.AppendLine($"            __result = {childrenExpr}[__i].{m.Name}({argList});");
            sb.AppendLine("        }");
            sb.AppendLine("        return __result;");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private void EmitCompositeProperty(StringBuilder sb, CompositeMember m, CompositeConfig config,
        string componentFullName, bool useImmutable, bool isInterface)
    {
        if (m.IsIgnored)
        {
            sb.AppendLine($"    /// <summary>Excluded from composite delegation. Must be implemented by subclasses.</summary>");
            if (m.HasGetter && m.HasSetter)
                sb.AppendLine($"    public abstract {m.ReturnType} {m.Name} {{ get; set; }}");
            else if (m.HasGetter)
                sb.AppendLine($"    public abstract {m.ReturnType} {m.Name} {{ get; }}");
            else if (m.HasSetter)
                sb.AppendLine($"    public abstract {m.ReturnType} {m.Name} {{ set; }}");
            sb.AppendLine();
            return;
        }

        var childrenExpr = useImmutable
            ? config.ChildrenPropertyName
            : $"_{GeneratorUtilities.ToCamelCase(config.ChildrenPropertyName)}";

        var modifier = isInterface ? "public virtual" : "public override";
        sb.AppendLine($"    /// <summary>Delegates {m.Name} to children.</summary>");

        if (m.HasGetter && m.HasSetter)
        {
            sb.AppendLine($"    {modifier} {m.ReturnType} {m.Name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => {childrenExpr}.Count > 0 ? {childrenExpr}[{childrenExpr}.Count - 1].{m.Name} : default!;");
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            sb.AppendLine($"            for (int __i = 0; __i < {childrenExpr}.Count; __i++)");
            sb.AppendLine("            {");
            sb.AppendLine($"                {childrenExpr}[__i].{m.Name} = value;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }
        else if (m.HasGetter)
        {
            sb.AppendLine($"    {modifier} {m.ReturnType} {m.Name}");
            sb.AppendLine($"        => {childrenExpr}.Count > 0 ? {childrenExpr}[{childrenExpr}.Count - 1].{m.Name} : default!;");
        }
        else if (m.HasSetter)
        {
            sb.AppendLine($"    {modifier} {m.ReturnType} {m.Name}");
            sb.AppendLine("    {");
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            sb.AppendLine($"            for (int __i = 0; __i < {childrenExpr}.Count; __i++)");
            sb.AppendLine("            {");
            sb.AppendLine($"                {childrenExpr}[__i].{m.Name} = value;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }
        sb.AppendLine();
    }

    private string EmitTraversal(INamedTypeSymbol componentSymbol, CompositeConfig config)
    {
        var ns = componentSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : componentSymbol.ContainingNamespace.ToDisplayString();
        var componentFullName = componentSymbol.ToDisplayString(GeneratorUtilities.TypeFormat);
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial class {config.CompositeBaseName}");
        sb.AppendLine("{");

        // DepthFirst
        sb.AppendLine($"    /// <summary>Enumerates all descendants depth-first.</summary>");
        sb.AppendLine($"    public System.Collections.Generic.IEnumerable<{componentFullName}> DepthFirst()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var stack = new System.Collections.Generic.Stack<{componentFullName}>();");
        sb.AppendLine($"        stack.Push(this);");
        sb.AppendLine("        while (stack.Count > 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            var current = stack.Pop();");
        sb.AppendLine("            yield return current;");
        sb.AppendLine($"            if (current is {config.CompositeBaseName} composite)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var kids = composite.{config.ChildrenPropertyName};");
        sb.AppendLine("                for (int __i = kids.Count - 1; __i >= 0; __i--)");
        sb.AppendLine("                {");
        sb.AppendLine("                    stack.Push(kids[__i]);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // BreadthFirst
        sb.AppendLine($"    /// <summary>Enumerates all descendants breadth-first.</summary>");
        sb.AppendLine($"    public System.Collections.Generic.IEnumerable<{componentFullName}> BreadthFirst()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var queue = new System.Collections.Generic.Queue<{componentFullName}>();");
        sb.AppendLine($"        queue.Enqueue(this);");
        sb.AppendLine("        while (queue.Count > 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            var current = queue.Dequeue();");
        sb.AppendLine("            yield return current;");
        sb.AppendLine($"            if (current is {config.CompositeBaseName} composite)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var kids = composite.{config.ChildrenPropertyName};");
        sb.AppendLine("                for (int __i = 0; __i < kids.Count; __i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    queue.Enqueue(kids[__i]);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string FormatParamDecl(List<CompositeParam> parameters)
    {
        return string.Join(", ", parameters.Select(p =>
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
    }

    private static string FormatArgList(List<CompositeParam> parameters)
    {
        return string.Join(", ", parameters.Select(p =>
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
    }

    // Helper types
    private class CompositeConfig
    {
        public string ComponentBaseName { get; set; } = "";
        public string CompositeBaseName { get; set; } = "";
        public string ChildrenPropertyName { get; set; } = "Children";
        public int Storage { get; set; }
        public bool GenerateTraversalHelpers { get; set; }
    }

    private class CompositeMember
    {
        public CompositeMemberKind Kind { get; set; }
        public string Name { get; set; } = "";
        public string ReturnType { get; set; } = "";
        public bool IsVoid { get; set; }
        public bool IsIgnored { get; set; }
        public List<CompositeParam> Parameters { get; set; } = new List<CompositeParam>();
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
    }

    private class CompositeParam
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public RefKind RefKind { get; set; }
        public bool HasDefaultValue { get; set; }
        public string DefaultValue { get; set; } = null!;
    }

    private enum CompositeMemberKind
    {
        Method,
        Property
    }
}
