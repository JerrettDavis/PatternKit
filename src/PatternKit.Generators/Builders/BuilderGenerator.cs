using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PatternKit.Generators.Builders;

// Local copy of BuilderModel enum to avoid assembly loading issues
internal enum BuilderModel
{
    MutableInstance = 0,
    StateProjection = 1
}

[Generator]
public sealed class BuilderGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "PatternKit.Generators.Builders.GenerateBuilderAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) => CreateModel(ctx))
            .Where(static r => r is not null);

        context.RegisterSourceOutput(candidates, static (spc, result) =>
        {
            if (result is null)
                return;

            foreach (var diag in result.Diagnostics)
                spc.ReportDiagnostic(diag);

            if (!string.IsNullOrEmpty(result.Source))
                spc.AddSource(result.HintName, result.Source);

            if (!string.IsNullOrEmpty(result.ExtraSource) && !string.IsNullOrEmpty(result.ExtraHint))
                spc.AddSource(result.ExtraHint!, result.ExtraSource!);
        });
    }

    private static GenerationResult CreateModel(GeneratorAttributeSyntaxContext ctx)
    {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        var attribute = ctx.Attributes[0];
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = ctx.TargetNode.GetLocation();

        if (!IsPartial(symbol) || symbol.IsUnboundGenericType || symbol.IsGenericType)
        {
            diagnostics.Add(Diagnostic.Create(Diagnostics.MustBePartial, location, symbol.Name));
            return new GenerationResult(string.Empty, string.Empty, diagnostics.ToImmutable());
        }

        var builderTypeName = ReadNamedArgument(attribute, "BuilderTypeName");
        var newMethodName = ReadNamedArgument(attribute, "NewMethodName") ?? "New";
        var buildMethodName = ReadNamedArgument(attribute, "BuildMethodName") ?? "Build";
        var model = ReadNamedArgument(attribute, "Model", BuilderModel.MutableInstance);
        var generateBuilderMethods = ReadNamedArgument(attribute, "GenerateBuilderMethods", false);
        var forceAsync = ReadNamedArgument(attribute, "ForceAsync", false);
        var includeFields = ReadNamedArgument(attribute, "IncludeFields", false);

        builderTypeName ??= symbol.Name.EndsWith("Builder", StringComparison.Ordinal)
            ? symbol.Name
            : $"{symbol.Name}Builder";

        if (symbol.IsStatic && string.Equals(builderTypeName, symbol.Name, StringComparison.Ordinal) || 
            symbol.ContainingNamespace.GetTypeMembers(builderTypeName).Any(static _ => true) &&
            !string.Equals(builderTypeName, symbol.Name, StringComparison.Ordinal))
        {
            diagnostics.Add(Diagnostic.Create(Diagnostics.BuilderTypeConflict, location, builderTypeName));
            return new GenerationResult(string.Empty, string.Empty, diagnostics.ToImmutable());
        }

        if (model == BuilderModel.MutableInstance)
        {
            return BuildMutable(symbol, builderTypeName, newMethodName, buildMethodName, generateBuilderMethods, forceAsync, includeFields, diagnostics);
        }

        return BuildProjection(symbol, builderTypeName, newMethodName, buildMethodName, generateBuilderMethods, forceAsync, diagnostics);
    }

    private static GenerationResult BuildMutable(
        INamedTypeSymbol target,
        string builderTypeName,
        string newMethodName,
        string buildMethodName,
        bool generateBuilderMethods,
        bool forceAsync,
        bool includeFields,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var location = target.Locations.FirstOrDefault() ?? Location.None;
        var constructors = target.Constructors.Where(static c => !c.IsStatic).ToArray();
        var markedConstructors = constructors.Where(static c => HasAttribute(c, "PatternKit.Generators.Builders.BuilderConstructorAttribute")).ToArray();

        if (markedConstructors.Length > 1)
        {
            diagnostics.Add(Diagnostic.Create(Diagnostics.MultipleConstructors, location, target.Name));
            return new GenerationResult(string.Empty, string.Empty, diagnostics.ToImmutable());
        }

        var selectedCtor = markedConstructors.Length == 1
            ? markedConstructors[0]
            : constructors.FirstOrDefault(static c => c.Parameters.Length == 0 && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);

        if (selectedCtor is null && target.TypeKind != TypeKind.Struct)
        {
            diagnostics.Add(Diagnostic.Create(Diagnostics.NoConstructor, location, target.Name));
            return new GenerationResult(string.Empty, string.Empty, diagnostics.ToImmutable());
        }

        var members = GatherMutableMembers(target, includeFields, diagnostics);
        if (members.IsDefault)
        {
            return new GenerationResult(string.Empty, string.Empty, diagnostics.ToImmutable());
        }

        var productTypeName = target.ToDisplayString(TypeFormat);
        var ns = target.ContainingNamespace.IsGlobalNamespace ? null : target.ContainingNamespace.ToDisplayString();
        var accessibility = GetAccessibility(target.DeclaredAccessibility);
        _ = forceAsync;
        var asyncEnabled = true;

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(ns))
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append(accessibility).Append(" partial class ").Append(builderTypeName).AppendLine();
        sb.AppendLine("{");
        sb.Append("    private readonly List<Action<").Append(productTypeName).Append(">> _steps = new();").AppendLine();
        sb.Append("    private readonly List<Func<").Append(productTypeName).Append(", string?>> _requirements = new();").AppendLine();
        if (asyncEnabled)
        {
            sb.Append("    private readonly List<Func<").Append(productTypeName).Append(", ValueTask>> _asyncSteps = new();").AppendLine();
            sb.Append("    private readonly List<Func<").Append(productTypeName).Append(", ValueTask<string?>>> _asyncRequirements = new();").AppendLine();
        }

        foreach (var member in members.Where(static m => m.IsRequired))
        {
            sb.Append("    private bool _set").Append(member.SafeName).AppendLine(";");
        }

        sb.AppendLine();
        sb.Append("    public static ").Append(builderTypeName).Append(' ').Append(newMethodName).AppendLine("() => new();");
        sb.AppendLine();

        foreach (var member in members)
        {
            sb.Append("    public ").Append(builderTypeName).Append(' ').Append("With").Append(member.Name).Append('(').Append(member.TypeName).Append(" value)").AppendLine();
            sb.AppendLine("    {");
            if (member.IsRequired)
            {
                sb.Append("        _set").Append(member.SafeName).AppendLine(" = true;");
            }

            sb.Append("        _steps.Add(item => item.").Append(member.Name).Append(" = value);").AppendLine();
            sb.AppendLine("        return this;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.Append("    public ").Append(builderTypeName).Append(" With(Action<").Append(productTypeName).AppendLine("> step)");
        sb.AppendLine("    {");
        sb.AppendLine("        _steps.Add(step);");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        if (asyncEnabled)
        {
            sb.Append("    public ").Append(builderTypeName).Append(" WithAsync(Func<").Append(productTypeName).AppendLine(", ValueTask> step)");
            sb.AppendLine("    {");
            sb.AppendLine("        _asyncSteps.Add(step);");
            sb.AppendLine("        return this;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.Append("    public ").Append(builderTypeName).Append(" Require(Func<").Append(productTypeName).AppendLine(", string?> requirement)");
        sb.AppendLine("    {");
        sb.AppendLine("        _requirements.Add(requirement);");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        if (asyncEnabled)
        {
            sb.Append("    public ").Append(builderTypeName).Append(" RequireAsync(Func<").Append(productTypeName).AppendLine(", ValueTask<string?>> requirement)");
            sb.AppendLine("    {");
            sb.AppendLine("        _asyncRequirements.Add(requirement);");
            sb.AppendLine("        return this;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.Append("    public ").Append(productTypeName).Append(' ').Append(buildMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        var item = ");
        if (target.TypeKind == TypeKind.Struct)
        {
            sb.Append("new ").Append(productTypeName).Append("();").AppendLine();
        }
        else
        {
            if (selectedCtor is not null && selectedCtor.Parameters.Length > 0)
            {
                sb.Append("new ").Append(productTypeName).Append('(');
                sb.Append(string.Join(", ", selectedCtor.Parameters.Select(static _ => "default")));
                sb.Append(");").AppendLine();
            }
            else
            {
                sb.Append("new ").Append(productTypeName).Append("();").AppendLine();
            }
        }

        sb.AppendLine("        foreach (var step in _steps) step(item);");

        foreach (var member in members.Where(static m => m.IsRequired))
        {
            var message = string.IsNullOrWhiteSpace(member.RequiredMessage)
                ? $"\"{member.Name} is required.\""
                : $"\"{member.RequiredMessage}\"";
            sb.Append("        if (!_set").Append(member.SafeName).Append(')').AppendLine();
            sb.Append("            throw new InvalidOperationException(").Append(message).AppendLine(");");
        }

        sb.AppendLine("        foreach (var requirement in _requirements)");
        sb.AppendLine("        {");
        sb.AppendLine("            var message = requirement(item);");
        sb.AppendLine("            if (message is not null)");
        sb.AppendLine("                throw new InvalidOperationException(message);");
        sb.AppendLine("        }");

        sb.AppendLine();
        sb.AppendLine("        return item;");
        sb.AppendLine("    }");

        if (asyncEnabled)
        {
            sb.AppendLine();
            sb.Append("    public async ValueTask<").Append(productTypeName).Append("> ").Append(buildMethodName).AppendLine("Async()");
            sb.AppendLine("    {");
            sb.Append("        var item = ");
            if (target.TypeKind == TypeKind.Struct)
            {
                sb.Append("new ").Append(productTypeName).Append("();").AppendLine();
            }
            else
            {
                if (selectedCtor is not null && selectedCtor.Parameters.Length > 0)
                {
                    sb.Append("new ").Append(productTypeName).Append('(');
                    sb.Append(string.Join(", ", selectedCtor.Parameters.Select(static _ => "default")));
                    sb.Append(");").AppendLine();
                }
                else
                {
                    sb.Append("new ").Append(productTypeName).Append("();").AppendLine();
                }
            }

            sb.AppendLine("        foreach (var step in _steps) step(item);");
            sb.AppendLine("        foreach (var step in _asyncSteps) await step(item).ConfigureAwait(false);");

            foreach (var member in members.Where(static m => m.IsRequired))
            {
                var message = string.IsNullOrWhiteSpace(member.RequiredMessage)
                    ? $"\"{member.Name} is required.\""
                    : $"\"{member.RequiredMessage}\"";
                sb.Append("        if (!_set").Append(member.SafeName).Append(')').AppendLine();
                sb.Append("            throw new InvalidOperationException(").Append(message).AppendLine(");");
            }

            sb.AppendLine("        foreach (var requirement in _requirements)");
            sb.AppendLine("        {");
            sb.AppendLine("            var message = requirement(item);");
            sb.AppendLine("            if (message is not null)");
            sb.AppendLine("                throw new InvalidOperationException(message);");
            sb.AppendLine("        }");

            sb.AppendLine("        foreach (var requirement in _asyncRequirements)");
            sb.AppendLine("        {");
            sb.AppendLine("            var message = await requirement(item).ConfigureAwait(false);");
            sb.AppendLine("            if (message is not null)");
            sb.AppendLine("                throw new InvalidOperationException(message);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        return item;");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        string? builderMethodsSource = null;
        string? builderMethodsHint = null;
        if (generateBuilderMethods)
        {
            var mb = new StringBuilder();
            mb.AppendLine("#nullable enable");
            mb.AppendLine("// <auto-generated />");
            mb.AppendLine("using System;");
            mb.AppendLine("using System.Threading.Tasks;");
            mb.AppendLine();
            if (!string.IsNullOrEmpty(ns))
            {
                mb.Append("namespace ").Append(ns).AppendLine(";");
                mb.AppendLine();
            }

            var typeModifiers = target.IsStatic ? "static partial" : "partial";
            mb.Append(accessibility).Append(' ').Append(typeModifiers).Append(" class ").Append(target.Name).AppendLine();
            mb.AppendLine("{");
            mb.Append("    public static ").Append(productTypeName).Append(" Build(Action<").Append(builderTypeName).AppendLine("> configure)");
            mb.AppendLine("    {");
            mb.Append("        var builder = ").Append(builderTypeName).Append('.').Append(newMethodName).AppendLine("();");
            mb.AppendLine("        configure(builder);");
            mb.Append("        return builder.").Append(buildMethodName).AppendLine("();");
            mb.AppendLine("    }");
            mb.AppendLine();
            if (asyncEnabled)
            {
                mb.Append("    public static ValueTask<").Append(productTypeName).Append("> BuildAsync(Func<").Append(builderTypeName).AppendLine(", ValueTask> configure)");
                mb.AppendLine("    {");
                mb.Append("        var builder = ").Append(builderTypeName).Append('.').Append(newMethodName).AppendLine("();");
                mb.AppendLine("        var pending = configure(builder);");
                mb.AppendLine("        if (!pending.IsCompletedSuccessfully)");
                mb.AppendLine("        {");
                mb.AppendLine("            return AwaitAsync(pending, builder);");
                mb.AppendLine("        }");
                mb.AppendLine();
                mb.Append("        return builder.").Append(buildMethodName).AppendLine("Async();");
                mb.AppendLine();
                mb.Append("        static async ValueTask<").Append(productTypeName).Append("> AwaitAsync(ValueTask wait, ").Append(builderTypeName).AppendLine(" b)");
                mb.AppendLine("        {");
                mb.AppendLine("            await wait.ConfigureAwait(false);");
                mb.Append("            return await b.").Append(buildMethodName).AppendLine("Async().ConfigureAwait(false);");
                mb.AppendLine("        }");
                mb.AppendLine("    }");
            }

            mb.AppendLine("}");
            builderMethodsSource = mb.ToString();
            builderMethodsHint = $"{target.Name}.BuilderMethods.g.cs";
        }

        var hint = $"{builderTypeName}.g.cs";
        return new GenerationResult(hint, sb.ToString(), diagnostics.ToImmutable(), builderMethodsHint, builderMethodsSource);
    }

    private static GenerationResult BuildProjection(
        INamedTypeSymbol target,
        string builderTypeName,
        string newMethodName,
        string buildMethodName,
        bool generateBuilderMethods,
        bool forceAsync,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var location = target.Locations.FirstOrDefault() ?? Location.None;
        var seed = target.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(static m => m.IsStatic && m.Parameters.Length == 0 && string.Equals(m.Name, "Seed", StringComparison.Ordinal));
        if (seed is null || seed.ReturnsVoid)
        {
            diagnostics.Add(Diagnostic.Create(Diagnostics.ProjectionNeedsSeed, location, target.Name));
            return new GenerationResult(string.Empty, string.Empty, diagnostics.ToImmutable());
        }

        var stateTypeName = seed.ReturnType.ToDisplayString(TypeFormat);
        var projectorMethods = target.GetMembers().OfType<IMethodSymbol>()
            .Where(static m => HasAttribute(m, "PatternKit.Generators.Builders.BuilderProjectorAttribute"))
            .ToArray();

        IMethodSymbol? defaultProjector = null;
        if (projectorMethods.Length > 1)
        {
            diagnostics.Add(Diagnostic.Create(Diagnostics.MultipleProjectors, location, target.Name));
        }
        else if (projectorMethods.Length == 1)
        {
            defaultProjector = projectorMethods[0];
            if (!IsValidProjector(defaultProjector, seed.ReturnType))
            {
                diagnostics.Add(Diagnostic.Create(Diagnostics.InvalidProjector, defaultProjector.Locations.FirstOrDefault() ?? location, target.Name));
                defaultProjector = null;
            }
        }

        var ns = target.ContainingNamespace.IsGlobalNamespace ? null : target.ContainingNamespace.ToDisplayString();
        var accessibility = GetAccessibility(target.DeclaredAccessibility);
        _ = forceAsync;
        var asyncEnabled = true;
        var targetTypeName = target.ToDisplayString(TypeFormat);
        var projectorName = defaultProjector?.Name ?? "Project";

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(ns))
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append(accessibility).Append(" partial class ").Append(builderTypeName).AppendLine();
        sb.AppendLine("{");
        sb.Append("    private ").Append(stateTypeName).AppendLine(" _state;");
        sb.Append("    private readonly List<Func<").Append(stateTypeName).Append(", ").Append(stateTypeName).Append(">> _steps = new();").AppendLine();
        sb.Append("    private readonly List<Func<").Append(stateTypeName).Append(", string?>> _requirements = new();").AppendLine();
        if (asyncEnabled)
        {
            sb.Append("    private readonly List<Func<").Append(stateTypeName).Append(", ValueTask<").Append(stateTypeName).Append(">>> _asyncSteps = new();").AppendLine();
            sb.Append("    private readonly List<Func<").Append(stateTypeName).Append(", ValueTask<string?>>> _asyncRequirements = new();").AppendLine();
        }

        sb.AppendLine();
        sb.Append("    public static ").Append(builderTypeName).Append(' ').Append(newMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        return new ").Append(builderTypeName).AppendLine("()");
        sb.AppendLine("        {");
        sb.Append("            _state = ").Append(targetTypeName).AppendLine(".Seed()");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.Append("    public ").Append(builderTypeName).Append(" With(Func<").Append(stateTypeName).Append(", ").Append(stateTypeName).AppendLine("> step)");
        sb.AppendLine("    {");
        sb.AppendLine("        _steps.Add(step);");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        if (asyncEnabled)
        {
            sb.Append("    public ").Append(builderTypeName).Append(" WithAsync(Func<").Append(stateTypeName).Append(", ValueTask<").Append(stateTypeName).AppendLine(">> step)");
            sb.AppendLine("    {");
            sb.AppendLine("        _asyncSteps.Add(step);");
            sb.AppendLine("        return this;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.Append("    public ").Append(builderTypeName).Append(" Require(Func<").Append(stateTypeName).AppendLine(", string?> requirement)");
        sb.AppendLine("    {");
        sb.AppendLine("        _requirements.Add(requirement);");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        if (asyncEnabled)
        {
            sb.Append("    public ").Append(builderTypeName).Append(" RequireAsync(Func<").Append(stateTypeName).AppendLine(", ValueTask<string?>> requirement)");
            sb.AppendLine("    {");
            sb.AppendLine("        _asyncRequirements.Add(requirement);");
            sb.AppendLine("        return this;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        if (defaultProjector is not null)
        {
            var returnType = defaultProjector.ReturnType.ToDisplayString(TypeFormat);
            sb.Append("    public ").Append(returnType).Append(' ').Append(buildMethodName).AppendLine("()");
            sb.AppendLine("    {");
            sb.Append("        return ").Append(buildMethodName).Append("(static state => ").Append(targetTypeName).Append('.').Append(projectorName).AppendLine("(state));");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.Append("    public TResult ").Append(buildMethodName).Append("<TResult>(Func<").Append(stateTypeName).Append(", TResult> projector)").AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        var state = _state;");
        sb.AppendLine("        foreach (var step in _steps) state = step(state);");
        sb.AppendLine();
        sb.AppendLine("        foreach (var requirement in _requirements)");
        sb.AppendLine("        {");
        sb.AppendLine("            var message = requirement(state);");
        sb.AppendLine("            if (message is not null)");
        sb.AppendLine("                throw new InvalidOperationException(message);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return projector(state);");
        sb.AppendLine("    }");

        if (asyncEnabled)
        {
            if (defaultProjector is not null)
            {
                var returnType = defaultProjector.ReturnType.ToDisplayString(TypeFormat);
                if (IsValueTask(defaultProjector.ReturnType))
                {
                    sb.AppendLine();
                    sb.Append("    public ValueTask<").Append(GetValueTaskInner(defaultProjector.ReturnType)).Append("> ").Append(buildMethodName).AppendLine("Async()");
                    sb.AppendLine("    {");
                    sb.Append("        return ").Append(buildMethodName).Append("Async(static state => ").Append(targetTypeName).Append('.').Append(projectorName).AppendLine("(state));");
                    sb.AppendLine("    }");
                }
                else
                {
                    sb.AppendLine();
                    sb.Append("    public ValueTask<").Append(returnType).Append("> ").Append(buildMethodName).AppendLine("Async()");
                    sb.AppendLine("    {");
                    sb.Append("        return ").Append(buildMethodName).Append("Async(static state => new ValueTask<").Append(returnType).Append('>').Append('(').Append(targetTypeName).Append('.').Append(projectorName).AppendLine("(state)));");
                    sb.AppendLine("    }");
                }
            }

            sb.AppendLine();
            sb.Append("    public async ValueTask<TResult> ").Append(buildMethodName).Append("Async<TResult>(Func<").Append(stateTypeName).Append(", ValueTask<TResult>> projector)").AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine("        var state = _state;");
            sb.AppendLine("        foreach (var step in _steps) state = step(state);");
            sb.AppendLine("        foreach (var step in _asyncSteps) state = await step(state).ConfigureAwait(false);");
            sb.AppendLine();
            sb.AppendLine("        foreach (var requirement in _requirements)");
            sb.AppendLine("        {");
            sb.AppendLine("            var message = requirement(state);");
            sb.AppendLine("            if (message is not null)");
            sb.AppendLine("                throw new InvalidOperationException(message);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        foreach (var requirement in _asyncRequirements)");
            sb.AppendLine("        {");
            sb.AppendLine("            var message = await requirement(state).ConfigureAwait(false);");
            sb.AppendLine("            if (message is not null)");
            sb.AppendLine("                throw new InvalidOperationException(message);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        return await projector(state).ConfigureAwait(false);");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        string? builderMethodsSource = null;
        string? builderMethodsHint = null;
        if (generateBuilderMethods && defaultProjector is not null)
        {
            var returnType = defaultProjector.ReturnType.ToDisplayString(TypeFormat);
            var mb = new StringBuilder();
            mb.AppendLine("#nullable enable");
            mb.AppendLine("// <auto-generated />");
            mb.AppendLine("using System;");
            mb.AppendLine("using System.Threading.Tasks;");
            mb.AppendLine();
            if (!string.IsNullOrEmpty(ns))
            {
                mb.Append("namespace ").Append(ns).AppendLine(";");
                mb.AppendLine();
            }

            var typeModifiers = target.IsStatic ? "static partial" : "partial";
            mb.Append(accessibility).Append(' ').Append(typeModifiers).Append(" class ").Append(target.Name).AppendLine();
            mb.AppendLine("{");
            mb.Append("    public static ").Append(returnType).Append(" Build(Action<").Append(builderTypeName).AppendLine("> configure)");
            mb.AppendLine("    {");
            mb.Append("        var builder = ").Append(builderTypeName).Append('.').Append(newMethodName).AppendLine("();");
            mb.AppendLine("        configure(builder);");
            mb.Append("        return builder.").Append(buildMethodName).AppendLine("();");
            mb.AppendLine("    }");
            mb.AppendLine();
            if (asyncEnabled)
            {
                mb.Append("    public static ValueTask<").Append(returnType).Append("> BuildAsync(Func<").Append(builderTypeName).AppendLine(", ValueTask> configure)");
                mb.AppendLine("    {");
                mb.Append("        var builder = ").Append(builderTypeName).Append('.').Append(newMethodName).AppendLine("();");
                mb.AppendLine("        var pending = configure(builder);");
                mb.AppendLine("        if (!pending.IsCompletedSuccessfully)");
                mb.AppendLine("        {");
                mb.AppendLine("            return AwaitAsync(pending, builder);");
                mb.AppendLine("        }");
                mb.AppendLine();
                if (IsValueTask(defaultProjector.ReturnType))
                {
                    mb.Append("        return builder.").Append(buildMethodName).AppendLine("Async();");
                }
                else
                {
                    mb.Append("        return builder.").Append(buildMethodName).AppendLine("Async();");
                }
                mb.AppendLine();
                mb.Append("        static async ValueTask<").Append(returnType).Append("> AwaitAsync(ValueTask wait, ").Append(builderTypeName).AppendLine(" b)");
                mb.AppendLine("        {");
                mb.AppendLine("            await wait.ConfigureAwait(false);");
                mb.Append("            return await b.").Append(buildMethodName).AppendLine("Async().ConfigureAwait(false);");
                mb.AppendLine("        }");
                mb.AppendLine("    }");
            }

            mb.AppendLine("}");
            builderMethodsSource = mb.ToString();
            builderMethodsHint = $"{target.Name}.BuilderMethods.g.cs";
        }

        var hint = $"{builderTypeName}.g.cs";
        return new GenerationResult(hint, sb.ToString(), diagnostics.ToImmutable(), builderMethodsHint, builderMethodsSource);
    }

    private static ImmutableArray<MemberModel> GatherMutableMembers(
        INamedTypeSymbol target,
        bool includeFields,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var members = ImmutableArray.CreateBuilder<MemberModel>();
        var location = target.Locations.FirstOrDefault() ?? Location.None;

        foreach (var member in target.GetMembers())
        {
            if (member is IPropertySymbol prop)
            {
                if (prop.IsStatic || prop.IsIndexer || HasAttribute(prop, "PatternKit.Generators.Builders.BuilderIgnoreAttribute"))
                {
                    continue;
                }

                if (prop.SetMethod is null || prop.SetMethod.DeclaredAccessibility == Accessibility.Private)
                {
                    diagnostics.Add(Diagnostic.Create(Diagnostics.MemberNotWritable, prop.Locations.FirstOrDefault() ?? location, prop.Name));
                    continue;
                }

                var requiredAttr = GetRequired(prop);
                members.Add(new MemberModel(prop.Name, ToSafeName(prop.Name), prop.Type.ToDisplayString(TypeFormat), requiredAttr.IsRequired, requiredAttr.Message, true));
            }
            else if (includeFields && member is IFieldSymbol field)
            {
                if (field.IsStatic || field.IsConst || field.IsReadOnly || HasAttribute(field, "PatternKit.Generators.Builders.BuilderIgnoreAttribute"))
                {
                    continue;
                }

                var requiredAttr = GetRequired(field);
                members.Add(new MemberModel(field.Name, ToSafeName(field.Name), field.Type.ToDisplayString(TypeFormat), requiredAttr.IsRequired, requiredAttr.Message, false));
            }
        }

        return members.ToImmutable();
    }

    private static (bool IsRequired, string? Message) GetRequired(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (string.Equals(attr.AttributeClass?.ToDisplayString(TypeFormat), "global::PatternKit.Generators.Builders.BuilderRequiredAttribute", StringComparison.Ordinal))
            {
                var msg = attr.NamedArguments.FirstOrDefault(static kvp => kvp.Key == "Message").Value.Value as string;
                return (true, msg);
            }
        }

        return (false, null);
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
        => symbol.GetAttributes().Any(a => string.Equals(a.AttributeClass?.ToDisplayString(TypeFormat), $"global::{metadataName}", StringComparison.Ordinal));

    private static string ToSafeName(string name)
        => name.Length == 0 ? "_member" : char.ToUpperInvariant(name[0]) + name.Substring(1);

    private static bool IsPartial(INamedTypeSymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is TypeDeclarationSyntax tds &&
                tds.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetAccessibility(Accessibility accessibility)
        => accessibility switch
        {
            Accessibility.Private => "private",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "protected internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "public"
        };

    private static bool IsValidProjector(IMethodSymbol method, ITypeSymbol stateType)
        => method.IsStatic &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stateType) &&
           !method.ReturnsVoid;

    private static bool IsValueTask(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return false;
        
        var constructed = named.ConstructedFrom;
        var full = constructed.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return string.Equals(full, "global::System.Threading.Tasks.ValueTask<T>", StringComparison.Ordinal) ||
               string.Equals(full, "global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal);

    }

    private static string GetValueTaskInner(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0].ToDisplayString(TypeFormat);
        }

        return "void";
    }

    private static string? ReadNamedArgument(AttributeData attribute, string name)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is string s)
            {
                return s;
            }
        }

        return null;
    }

    private static T ReadNamedArgument<T>(AttributeData attribute, string name, T fallback)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key != name)
            {
                continue;
            }

            if (arg.Value.Value is T value)
            {
                return value;
            }

            if (typeof(T).IsEnum && arg.Value.Value is int i)
            {
                return (T)Enum.ToObject(typeof(T), i);
            }
        }

        return fallback;
    }

    private sealed record MemberModel(string Name, string SafeName, string TypeName, bool IsRequired, string? RequiredMessage, bool IsProperty);

    private sealed record GenerationResult(
        string HintName,
        string Source,
        ImmutableArray<Diagnostic> Diagnostics,
        string? ExtraHint = null,
        string? ExtraSource = null);

    private static class Diagnostics
    {
        private const string Category = "PatternKit.Builders";

        public static readonly DiagnosticDescriptor MustBePartial = new(
            "B001",
            "Type must be partial",
            "Type '{0}' must be a non-generic partial type to generate a builder",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor BuilderTypeConflict = new(
            "B002",
            "Builder name conflicts",
            "Builder type name '{0}' conflicts with an existing type",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NoConstructor = new(
            "B003",
            "No usable constructor",
            "No usable constructor found for '{0}'",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MultipleConstructors = new(
            "B004",
            "Multiple builder constructors",
            "Multiple constructors are annotated with [BuilderConstructor] on '{0}'",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MemberNotWritable = new(
            "B005",
            "Member not writable",
            "Member '{0}' is not assignable by the builder",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor BuilderMethodCollision = new(
            "B006",
            "Builder method collision",
            "Generated builder methods would collide with existing members on '{0}'",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor RequiredMemberNotSet = new(
            "BR001",
            "Required member not set",
            "Required member '{0}' is never set",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor RequiredInvalidUsage = new(
            "BR002",
            "Required attribute unsupported",
            "Required member annotation on '{0}' is not supported",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor RequiredSignatureMismatch = new(
            "BR003",
            "Required member incompatible",
            "Required member '{0}' type is incompatible with the generated setter",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ProjectionNeedsSeed = new(
            "BP001",
            "Seed method missing",
            "State projection builder on '{0}' requires a static Seed() method",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MultipleProjectors = new(
            "BP002",
            "Multiple projectors",
            "Multiple projector methods were annotated on '{0}'",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidProjector = new(
            "BP003",
            "Invalid projector signature",
            "Projector on '{0}' must be static, take the builder state, and return a value",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor AsyncConfiguration = new(
            "BA001",
            "Async generation disabled",
            "Async generation requested but not enabled for '{0}'",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor AsyncSignature = new(
            "BA002",
            "Async signature invalid",
            "Async step, requirement, or projector signature was invalid for '{0}'",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
