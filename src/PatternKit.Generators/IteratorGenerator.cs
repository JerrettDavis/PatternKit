using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Iterator pattern.
/// Generates struct Enumerator, TryMoveNext, Current, and GetEnumerator
/// based on a user-provided step function.
/// Also supports tree traversal generation (DFS/BFS).
/// </summary>
[Generator]
public sealed class IteratorGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKIT001";
    private const string DiagIdNoStep = "PKIT002";
    private const string DiagIdMultipleSteps = "PKIT003";
    private const string DiagIdInvalidStepSignature = "PKIT004";
    private const string DiagIdTraversalNotStaticPartial = "PKIT005";
    private const string DiagIdTraversalMethodInvalid = "PKIT006";
    private const string DiagIdNoChildrenProvider = "PKIT007";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [Iterator] must be partial",
        messageFormat: "Type '{0}' is marked with [Iterator] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Iterator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoStepDescriptor = new(
        id: DiagIdNoStep,
        title: "No iterator step found",
        messageFormat: "Type '{0}' has [Iterator] but no method marked with [IteratorStep]. Exactly one step method is required.",
        category: "PatternKit.Generators.Iterator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleStepsDescriptor = new(
        id: DiagIdMultipleSteps,
        title: "Multiple iterator steps found",
        messageFormat: "Type '{0}' has multiple methods marked with [IteratorStep]. Only one step method is allowed.",
        category: "PatternKit.Generators.Iterator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidStepSignatureDescriptor = new(
        id: DiagIdInvalidStepSignature,
        title: "Invalid step method signature",
        messageFormat: "Step method '{0}' has an invalid signature. Expected: bool TryStep(ref TState state, out T item).",
        category: "PatternKit.Generators.Iterator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TraversalNotStaticPartialDescriptor = new(
        id: DiagIdTraversalNotStaticPartial,
        title: "Traversal host must be a static partial class",
        messageFormat: "Type '{0}' is marked with [TraversalIterator] but is not a static partial class.",
        category: "PatternKit.Generators.Iterator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TraversalMethodInvalidDescriptor = new(
        id: DiagIdTraversalMethodInvalid,
        title: "Invalid traversal method",
        messageFormat: "Traversal method '{0}' has an invalid signature. It must be a partial method returning IEnumerable<T> with a single parameter of type T.",
        category: "PatternKit.Generators.Iterator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoChildrenProviderDescriptor = new(
        id: DiagIdNoChildrenProvider,
        title: "No children provider found",
        messageFormat: "Type '{0}' has [TraversalIterator] but no method marked with [TraversalChildren]. A children provider is required for traversal generation.",
        category: "PatternKit.Generators.Iterator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // State-machine iterator
        var iteratorTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Iterator.IteratorAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(iteratorTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Iterator.IteratorAttribute");
            if (attr is null)
                return;

            GenerateIterator(spc, typeSymbol, attr, typeContext.TargetNode);
        });

        // Traversal iterator
        var traversalTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Iterator.TraversalIteratorAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(traversalTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            GenerateTraversal(spc, typeSymbol, typeContext.TargetNode);
        });
    }

    #region State-Machine Iterator

    private void GenerateIterator(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        if (!GeneratorUtilities.IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var config = ParseIteratorConfig(attribute);
        var steps = CollectStepMethods(typeSymbol);

        if (steps.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoStepDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        if (steps.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MultipleStepsDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var stepMethod = steps[0];

        // Validate signature: bool TryStep(ref TState state, out T item)
        if (!ValidateStepSignature(stepMethod, context))
            return;

        // Extract types
        var stateType = stepMethod.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var itemType = stepMethod.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var source = GenerateIteratorSource(typeSymbol, config, stepMethod, stateType, itemType);
        var fileName = $"{typeSymbol.Name}.Iterator.g.cs";
        context.AddSource(fileName, source);
    }

    private IteratorConfig ParseIteratorConfig(AttributeData attribute)
    {
        var config = new IteratorConfig();

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "GenerateEnumerator":
                    config.GenerateEnumerator = namedArg.Value.Value is bool ge && ge;
                    break;
                case "GenerateTryMoveNext":
                    config.GenerateTryMoveNext = namedArg.Value.Value is bool gm && gm;
                    break;
            }
        }

        return config;
    }

    private ImmutableArray<IMethodSymbol> CollectStepMethods(INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Iterator.IteratorStepAttribute");

            if (attr is not null)
                builder.Add(method);
        }

        return builder.ToImmutable();
    }

    private bool ValidateStepSignature(IMethodSymbol method, SourceProductionContext context)
    {
        // Must return bool
        if (method.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStepSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        // Must have exactly 2 parameters: ref TState, out T
        if (method.Parameters.Length != 2)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStepSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        if (method.Parameters[0].RefKind != RefKind.Ref)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStepSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        if (method.Parameters[1].RefKind != RefKind.Out)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStepSignatureDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
            return false;
        }

        return true;
    }

    private string GenerateIteratorSource(
        INamedTypeSymbol typeSymbol,
        IteratorConfig config,
        IMethodSymbol stepMethod,
        string stateType,
        string itemType)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;
        var typeKind = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";
        var recordKeyword = typeSymbol.IsRecord ? "record " : "";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial {recordKeyword}{typeKind} {typeName}");
        sb.AppendLine("{");

        // Generate TryMoveNext
        if (config.GenerateTryMoveNext)
        {
            sb.AppendLine($"    public bool TryMoveNext(ref {stateType} state, out {itemType} current)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return {stepMethod.Name}(ref state, out current);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Generate struct Enumerator
        if (config.GenerateEnumerator)
        {
            sb.AppendLine($"    public Enumerator GetEnumerator() => new Enumerator(this);");
            sb.AppendLine();
            sb.AppendLine($"    public struct Enumerator");
            sb.AppendLine("    {");
            sb.AppendLine($"        private {typeName} _source;");
            sb.AppendLine($"        private {stateType} _state;");
            sb.AppendLine($"        private {itemType} _current;");
            sb.AppendLine();
            sb.AppendLine($"        public Enumerator({typeName} source)");
            sb.AppendLine("        {");
            sb.AppendLine("            _source = source;");
            sb.AppendLine($"            _state = default!;");
            sb.AppendLine($"            _current = default!;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public {itemType} Current => _current;");
            sb.AppendLine();
            sb.AppendLine("        public bool MoveNext()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return _source.{stepMethod.Name}(ref _state, out _current);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region Traversal Iterator

    private void GenerateTraversal(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        SyntaxNode node)
    {
        // Must be partial
        if (!GeneratorUtilities.IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TraversalNotStaticPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Must be static
        if (!typeSymbol.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TraversalNotStaticPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Find children provider
        var childrenProviders = FindChildrenProviders(typeSymbol);
        if (childrenProviders.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoChildrenProviderDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var childrenProvider = childrenProviders[0];

        // Find DFS/BFS methods
        var dfsMethods = FindTraversalMethods(typeSymbol, "PatternKit.Generators.Iterator.DepthFirstAttribute");
        var bfsMethods = FindTraversalMethods(typeSymbol, "PatternKit.Generators.Iterator.BreadthFirstAttribute");

        if (dfsMethods.Length == 0 && bfsMethods.Length == 0)
            return; // Nothing to generate

        // Determine node type from children provider
        // Children provider: IEnumerable<T> GetChildren(T node)
        if (childrenProvider.Parameters.Length < 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TraversalMethodInvalidDescriptor,
                childrenProvider.Locations.FirstOrDefault(),
                childrenProvider.Name));
            return;
        }

        var nodeType = childrenProvider.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var source = GenerateTraversalSource(typeSymbol, childrenProvider, dfsMethods, bfsMethods, nodeType);
        var fileName = $"{typeSymbol.Name}.Traversal.g.cs";
        context.AddSource(fileName, source);
    }

    private ImmutableArray<IMethodSymbol> FindChildrenProviders(INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Iterator.TraversalChildrenAttribute");

            if (attr is not null)
                builder.Add(method);
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<IMethodSymbol> FindTraversalMethods(INamedTypeSymbol typeSymbol, string attributeName)
    {
        var builder = ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == attributeName);

            if (attr is not null)
                builder.Add(method);
        }

        return builder.ToImmutable();
    }

    private string GenerateTraversalSource(
        INamedTypeSymbol typeSymbol,
        IMethodSymbol childrenProvider,
        ImmutableArray<IMethodSymbol> dfsMethods,
        ImmutableArray<IMethodSymbol> bfsMethods,
        string nodeType)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"static partial class {typeName}");
        sb.AppendLine("{");

        // Generate DFS methods
        foreach (var dfs in dfsMethods)
        {
            var dfsAccess = GetAccessibility(dfs);
            sb.AppendLine($"    {dfsAccess}static partial {GetReturnTypeString(dfs)} {dfs.Name}({nodeType} root)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var result = new global::System.Collections.Generic.List<{nodeType}>();");
            sb.AppendLine($"        var stack = new global::System.Collections.Generic.Stack<{nodeType}>();");
            sb.AppendLine("        stack.Push(root);");
            sb.AppendLine();
            sb.AppendLine("        while (stack.Count > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            var node = stack.Pop();");
            sb.AppendLine("            result.Add(node);");
            sb.AppendLine();
            sb.AppendLine($"            var children = {childrenProvider.Name}(node);");
            sb.AppendLine($"            var childList = new global::System.Collections.Generic.List<{nodeType}>(children);");
            sb.AppendLine("            for (int i = childList.Count - 1; i >= 0; i--)");
            sb.AppendLine("            {");
            sb.AppendLine("                stack.Push(childList[i]);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        return result;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Generate BFS methods
        foreach (var bfs in bfsMethods)
        {
            var bfsAccess = GetAccessibility(bfs);
            sb.AppendLine($"    {bfsAccess}static partial {GetReturnTypeString(bfs)} {bfs.Name}({nodeType} root)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var result = new global::System.Collections.Generic.List<{nodeType}>();");
            sb.AppendLine($"        var queue = new global::System.Collections.Generic.Queue<{nodeType}>();");
            sb.AppendLine("        queue.Enqueue(root);");
            sb.AppendLine();
            sb.AppendLine("        while (queue.Count > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            var node = queue.Dequeue();");
            sb.AppendLine("            result.Add(node);");
            sb.AppendLine();
            sb.AppendLine($"            foreach (var child in {childrenProvider.Name}(node))");
            sb.AppendLine("            {");
            sb.AppendLine("                queue.Enqueue(child);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        return result;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetReturnTypeString(IMethodSymbol method)
    {
        return method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string GetAccessibility(IMethodSymbol method)
    {
        return method.DeclaredAccessibility switch
        {
            Accessibility.Public => "public ",
            Accessibility.Internal => "internal ",
            Accessibility.Protected => "protected ",
            Accessibility.ProtectedOrInternal => "protected internal ",
            Accessibility.ProtectedAndInternal => "private protected ",
            Accessibility.Private => "private ",
            _ => ""
        };
    }

    #endregion

    private class IteratorConfig
    {
        public bool GenerateEnumerator { get; set; } = true;
        public bool GenerateTryMoveNext { get; set; } = true;
    }
}
