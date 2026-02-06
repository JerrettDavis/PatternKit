using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PatternKit.Generators.Factories;

[Generator]
public sealed class FactoriesGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat ParameterFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var keyedFactories = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "PatternKit.Generators.Factories.FactoryMethodAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => CreateKeyedFactoryModel(ctx))
            .Where(static r => r is not null);

        context.RegisterSourceOutput(keyedFactories, static (spc, result) =>
        {
            if (result is null)
                return;

            foreach (var diag in result.Diagnostics)
            {
                spc.ReportDiagnostic(diag);
            }

            if (result.Model is { } model)
            {
                spc.AddSource($"{model.Type.Name}.FactoryMethod.g.cs", EmitKeyedFactory(model));
            }
        });

        var creatorBases = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "PatternKit.Generators.Factories.FactoryClassAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) => CreateCreatorBase(ctx))
            .Where(static r => r is not null);

        var creatorKeys = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "PatternKit.Generators.Factories.FactoryClassKeyAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => CreateCreatorKey(ctx))
            .Where(static r => r is not null);

        var creatorPipeline = context.CompilationProvider.Combine(creatorBases.Collect()).Combine(creatorKeys.Collect());

        context.RegisterSourceOutput(creatorPipeline, static (spc, tuple) =>
        {
            var compilation = tuple.Left.Item1;
            var bases = tuple.Left.Item2;
            var keys = tuple.Item2;

            foreach (var b in bases)
                foreach (var diag in b.Diagnostics)
                    spc.ReportDiagnostic(diag);

            foreach (var k in keys)
                foreach (var diag in k.Diagnostics)
                    spc.ReportDiagnostic(diag);

            var validBases = bases.Where(static b => b.Model is not null).Select(static b => b.Model!).ToImmutableArray();
            if (validBases.IsDefaultOrEmpty)
                return;

            foreach (var model in BuildCreatorFactories(compilation, validBases, keys, spc))
            {
                spc.AddSource($"{model.FactoryName}.FactoryClass.g.cs", EmitCreatorFactory(model));
            }
        });
    }

    private static KeyedFactoryResult CreateKeyedFactoryModel(GeneratorAttributeSyntaxContext ctx)
    {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        var attribute = ctx.Attributes[0];
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = ctx.TargetNode.GetLocation();

        var keyType = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as ITypeSymbol
            : null;

        if (keyType is null)
        {
            return new KeyedFactoryResult(null, diagnostics.ToImmutable());
        }

        var createName = ReadNamedArgument(attribute, "CreateMethodName", "Create");
        var caseInsensitive = ReadNamedArgument(attribute, "CaseInsensitiveStrings", true);

        if (!IsStaticPartial(symbol))
        {
            diagnostics.Add(Diagnostic.Create(Diagnostics.KeyedMustBeStaticPartial, location, symbol.Name));
            return new KeyedFactoryResult(null, diagnostics.ToImmutable());
        }

        var members = GatherKeyedMembers(symbol);
        if (members.Cases.Length == 0 && members.DefaultMethods.Length == 0)
        {
            return new KeyedFactoryResult(null, diagnostics.ToImmutable());
        }

        var buildResult = BuildKeyedModel(ctx.SemanticModel.Compilation, symbol, keyType, caseInsensitive, members, location, diagnostics);
        if (buildResult is null)
        {
            return new KeyedFactoryResult(null, diagnostics.ToImmutable());
        }

        var model = new KeyedFactoryModel(
            symbol,
            keyType,
            createName,
            caseInsensitive,
            buildResult.Value.Signature,
            buildResult.Value.Cases,
            buildResult.Value.DefaultCase,
            buildResult.Value.HasAsync);

        return new KeyedFactoryResult(model, diagnostics.ToImmutable());
    }

    private static CreatorBaseResult CreateCreatorBase(GeneratorAttributeSyntaxContext ctx)
    {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        var attribute = ctx.Attributes[0];
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = ctx.TargetNode.GetLocation();

        var keyType = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as ITypeSymbol
            : null;

        if (keyType is null)
        {
            return new CreatorBaseResult(null, diagnostics.ToImmutable());
        }

        if (!(symbol.TypeKind == TypeKind.Interface || (symbol.TypeKind == TypeKind.Class && symbol.IsAbstract)))
        {
            diagnostics.Add(Diagnostic.Create(Diagnostics.CreatorMustBeInterfaceOrAbstract, location, symbol.Name));
            return new CreatorBaseResult(null, diagnostics.ToImmutable());
        }

        var factoryNameOverride = ReadOptionalNamedArgument(attribute, "FactoryTypeName");
        var generateTryCreate = ReadNamedArgument(attribute, "GenerateTryCreate", true);
        var generateEnumKeys = ReadNamedArgument(attribute, "GenerateEnumKeys", false);

        var model = new CreatorFactoryBase(
            symbol,
            keyType,
            factoryNameOverride,
            generateTryCreate,
            generateEnumKeys);

        return new CreatorBaseResult(model, diagnostics.ToImmutable());
    }

    private static CreatorKeyResult CreateCreatorKey(GeneratorAttributeSyntaxContext ctx)
    {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        var attribute = ctx.Attributes[0];
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = ctx.TargetNode.GetLocation();

        if (!TryReadKey(attribute, out var keyConstant))
        {
            diagnostics.Add(Diagnostic.Create(Diagnostics.CreatorInvalidKeyValue, location, symbol.Name));
            return new CreatorKeyResult(null, diagnostics.ToImmutable());
        }

        var model = new CreatorKeyModel(symbol, keyConstant);
        return new CreatorKeyResult(model, diagnostics.ToImmutable());
    }

    private static ImmutableArray<CreatorFactoryModel> BuildCreatorFactories(
        Compilation compilation,
        ImmutableArray<CreatorFactoryBase> bases,
        ImmutableArray<CreatorKeyResult> keys,
        SourceProductionContext context)
    {
        var keyMap = keys.Where(static k => k?.Model is not null).Select(static k => k!.Model!).ToImmutableArray();
        var result = ImmutableArray.CreateBuilder<CreatorFactoryModel>();

        var validKeys = ImmutableArray.CreateBuilder<CreatorKeyModel>();
        foreach (var key in keyMap)
        {
            var matches = bases.Count(b => Implements(key.Type, b.Type));
            if (matches != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CreatorMultipleBases, key.Type.Locations.FirstOrDefault() ?? Location.None, key.Type.Name));
                continue;
            }

            validKeys.Add(key);
        }

        foreach (var creatorBase in bases)
        {
            var baseType = creatorBase.Type;
            var matchingKeys = validKeys.Where(k => Implements(k.Type, baseType)).ToImmutableArray();

            ReportMultiBaseKeys(bases, keyMap, context);
            var enumNames = creatorBase.GenerateEnumKeys ? new HashSet<string>(StringComparer.Ordinal) : null;
            var products = BuildCreatorProducts(compilation, creatorBase, baseType, matchingKeys, enumNames, context);

            if (products.IsDefaultOrEmpty)
            {
                continue;
            }

            var factoryName = creatorBase.FactoryTypeName ?? BuildDefaultFactoryName(baseType);
            var needsAsync = products.Any(static p => p.AsyncFactoryMethod is not null);

            var model = new CreatorFactoryModel(
                creatorBase.Type,
                creatorBase.KeyType,
                factoryName,
                creatorBase.GenerateTryCreate,
                creatorBase.GenerateEnumKeys,
                products,
                needsAsync);

            result.Add(model);
        }

        return result.ToImmutable();
    }

    private static KeyedMembers GatherKeyedMembers(INamedTypeSymbol symbol)
    {
        var caseMethods = ImmutableArray.CreateBuilder<FactoryCaseInfo>();
        var defaultMethods = ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (var method in symbol.GetMembers().OfType<IMethodSymbol>().Where(static m => m.MethodKind == MethodKind.Ordinary))
        {
            foreach (var attr in method.GetAttributes())
            {
                var name = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (name is "global::PatternKit.Generators.Factories.FactoryCaseAttribute")
                {
                    caseMethods.Add(new FactoryCaseInfo(method, attr));
                }
                else if (name is "global::PatternKit.Generators.Factories.FactoryDefaultAttribute")
                {
                    defaultMethods.Add(method);
                }
            }
        }

        return new KeyedMembers(caseMethods.ToImmutable(), defaultMethods.ToImmutable());
    }

    private static KeyedBuildResult? BuildKeyedModel(
        Compilation compilation,
        INamedTypeSymbol symbol,
        ITypeSymbol keyType,
        bool caseInsensitive,
        KeyedMembers members,
        Location location,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var signature = default(MethodSignature?);
        var keyComparer = BuildKeyComparer(caseInsensitive);
        var seenKeys = new Dictionary<KeyWrapper, Location?>(keyComparer);
        var processedCases = ImmutableArray.CreateBuilder<KeyedCase>();
        MethodInfo? defaultInfo = null;

        foreach (var entry in members.Cases)
        {
            if (!entry.Method.IsStatic)
            {
                diagnostics.Add(Diagnostic.Create(Diagnostics.KeyedMethodsMustBeStatic, entry.Method.Locations.FirstOrDefault() ?? location, entry.Method.Name));
                continue;
            }

            if (!TryReadKey(entry.Attribute, out var keyConstant) || !IsKeyCompatible(compilation, keyType, keyConstant))
            {
                diagnostics.Add(Diagnostic.Create(Diagnostics.KeyedInvalidKeyValue, entry.Method.Locations.FirstOrDefault() ?? location, entry.Method.Name, keyType.ToDisplayString(GeneratorUtilities.TypeFormat)));
                continue;
            }

            var methodInfo = BuildFactoryMethodInfo(entry.Method, compilation);
            signature ??= methodInfo.Signature;

            if (!SignatureEquals(signature, methodInfo.Signature))
            {
                diagnostics.Add(Diagnostic.Create(Diagnostics.KeyedSignatureMismatch, entry.Method.Locations.FirstOrDefault() ?? location, symbol.Name));
                continue;
            }

            var wrapper = new KeyWrapper(keyConstant, keyType);
            var literal = ToLiteral(keyConstant);
            if (seenKeys.ContainsKey(wrapper))
            {
                diagnostics.Add(Diagnostic.Create(Diagnostics.KeyedDuplicateKey, entry.Method.Locations.FirstOrDefault() ?? location, literal, symbol.Name));
                continue;
            }

            seenKeys[wrapper] = entry.Method.Locations.FirstOrDefault();
            processedCases.Add(new KeyedCase(literal, methodInfo.MethodSymbol, methodInfo.AsyncKind));
        }

        if (members.DefaultMethods.Length > 1)
        {
            foreach (var method in members.DefaultMethods.Skip(1))
            {
                diagnostics.Add(Diagnostic.Create(Diagnostics.KeyedMultipleDefaults, method.Locations.FirstOrDefault() ?? location, symbol.Name));
            }
        }

        var primaryDefault = members.DefaultMethods.FirstOrDefault();
        if (primaryDefault is not null)
        {
            if (!primaryDefault.IsStatic)
            {
                diagnostics.Add(Diagnostic.Create(Diagnostics.KeyedMethodsMustBeStatic, primaryDefault.Locations.FirstOrDefault() ?? location, primaryDefault.Name));
            }
            else
            {
                var methodInfo = BuildFactoryMethodInfo(primaryDefault, compilation);
                if (signature != null && !SignatureEquals(signature, methodInfo.Signature))
                {
                    diagnostics.Add(Diagnostic.Create(Diagnostics.KeyedSignatureMismatch, primaryDefault.Locations.FirstOrDefault() ?? location, symbol.Name));
                }
                else
                {
                    signature ??= methodInfo.Signature;
                    defaultInfo = methodInfo;
                }
            }
        }

        if (signature is null || diagnostics.Count > 0)
        {
            return null;
        }

        var cases = processedCases.ToImmutable();
        var hasAsync = cases.Any(static c => c.AsyncKind != AsyncKind.Sync) || (defaultInfo is { AsyncKind: not AsyncKind.Sync });

        return new KeyedBuildResult(signature, cases, defaultInfo, hasAsync);
    }

    private static void ReportMultiBaseKeys(
        ImmutableArray<CreatorFactoryBase> bases,
        ImmutableArray<CreatorKeyModel> keyMap,
        SourceProductionContext context)
    {
        foreach (var key in keyMap)
        {
            var candidates = bases.Count(b => Implements(key.Type, b.Type));
            if (candidates > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CreatorMultipleBases, key.Type.Locations.FirstOrDefault() ?? Location.None, key.Type.Name));
            }
        }
    }

    private static ImmutableArray<CreatorProduct> BuildCreatorProducts(
        Compilation compilation,
        CreatorFactoryBase creatorBase,
        INamedTypeSymbol baseType,
        ImmutableArray<CreatorKeyModel> matchingKeys,
        HashSet<string>? enumNames,
        SourceProductionContext context)
    {
        var seen = new Dictionary<KeyWrapper, INamedTypeSymbol>(BuildKeyComparer(false));
        var products = ImmutableArray.CreateBuilder<CreatorProduct>();

        foreach (var keyModel in matchingKeys)
        {
            if (keyModel.Type.IsAbstract)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CreatorMustBeConcrete, keyModel.Type.Locations.FirstOrDefault() ?? Location.None, keyModel.Type.Name));
                continue;
            }

            var ctor = keyModel.Type.InstanceConstructors.FirstOrDefault(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);
            var asyncMethod = FindAsyncFactory(compilation, keyModel.Type, baseType);
            if (ctor is null && asyncMethod is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CreatorMissingCtor, keyModel.Type.Locations.FirstOrDefault() ?? Location.None, keyModel.Type.Name));
                continue;
            }

            if (!IsKeyCompatible(compilation, creatorBase.KeyType, keyModel.Key))
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CreatorInvalidKeyValue, keyModel.Type.Locations.FirstOrDefault() ?? Location.None, keyModel.Type.Name));
                continue;
            }

            var literal = ToLiteral(keyModel.Key);
            var wrapper = new KeyWrapper(keyModel.Key, creatorBase.KeyType);
            if (seen.ContainsKey(wrapper))
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CreatorDuplicateKey, keyModel.Type.Locations.FirstOrDefault() ?? Location.None, literal, creatorBase.Type.Name));
                continue;
            }

            seen[wrapper] = keyModel.Type;
            var enumMemberName = creatorBase.GenerateEnumKeys
                ? BuildEnumMemberName(keyModel.Key, creatorBase.KeyType, enumNames!)
                : null;
            var creationKind = asyncMethod switch
            {
                null => CreationKind.Sync,
                { } m when IsValueTask(compilation, m.ReturnType, out _) => CreationKind.ValueTask,
                _ => CreationKind.Task
            };

            products.Add(new CreatorProduct(keyModel.Type, literal, creationKind, ctor, asyncMethod, enumMemberName));
        }

        return products.ToImmutable();
    }

    private static string EmitKeyedFactory(KeyedFactoryModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");

        var ns = model.Type.ContainingNamespace.IsGlobalNamespace
            ? null
            : model.Type.ContainingNamespace.ToDisplayString();

        if (!string.IsNullOrWhiteSpace(ns))
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        var accessibility = AccessibilityToString(model.Type.DeclaredAccessibility);
        var typeName = model.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        sb.Append(accessibility).Append(" static partial class ").Append(typeName).AppendLine();
        sb.AppendLine("{");

        var keyTypeName = model.KeyType.ToDisplayString(GeneratorUtilities.TypeFormat);
        var parameterList = BuildParameterList(model.Signature.Parameters);
        var argumentList = BuildArgumentList(model.Signature.Parameters);
        var resultTypeName = model.Signature.ResultTypeName;

        EmitKeyedCreateBody(sb, model, keyTypeName, parameterList, argumentList, resultTypeName);
        EmitKeyedTryCreateBody(sb, model, keyTypeName, parameterList, argumentList, resultTypeName);

        if (model.HasAsync)
        {
            EmitKeyedCreateAsync(sb, model, keyTypeName, parameterList, argumentList, resultTypeName);
            EmitKeyedTryCreateAsync(sb, model, keyTypeName, parameterList, argumentList, resultTypeName);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitKeyedCreateBody(
        StringBuilder sb,
        KeyedFactoryModel model,
        string keyTypeName,
        string parameterList,
        string argumentList,
        string resultTypeName)
    {
        sb.Append("    public static ").Append(resultTypeName).Append(' ').Append(model.CreateMethodName)
            .Append('(').Append(keyTypeName).Append(" key");
        if (parameterList.Length > 0)
        {
            sb.Append(", ").Append(parameterList);
        }
        sb.AppendLine(")");
        sb.AppendLine("    {");

        if (NeedsNullCheck(model.KeyType))
        {
            sb.AppendLine("        if (key is null) throw new global::System.ArgumentNullException(nameof(key));");
            sb.AppendLine();
        }

        if (IsStringType(model.KeyType))
        {
            foreach (var c in model.Cases)
            {
                sb.Append("        if (global::System.String.Equals(key, ").Append(c.KeyLiteral)
                    .Append(", global::System.StringComparison.")
                    .Append(model.CaseInsensitiveStrings ? "OrdinalIgnoreCase" : "Ordinal").AppendLine("))");
                sb.Append("            return ").Append(BuildSyncValue(c.AsyncKind, $"{c.Method.Name}({argumentList})")).AppendLine(";");
                sb.AppendLine();
            }

            if (model.DefaultCase is not null)
            {
                sb.Append("        return ").Append(BuildSyncValue(model.DefaultCase.AsyncKind, $"{model.DefaultCase.MethodSymbol.Name}({argumentList})")).AppendLine(";");
            }
            else
            {
                sb.AppendLine("        throw new global::System.ArgumentOutOfRangeException(nameof(key));");
            }
        }
        else
        {
            sb.AppendLine("        switch (key)");
            sb.AppendLine("        {");
            foreach (var c in model.Cases)
            {
                sb.Append("            case ").Append(c.KeyLiteral).AppendLine(":");
                sb.Append("                return ").Append(BuildSyncValue(c.AsyncKind, $"{c.Method.Name}({argumentList})")).AppendLine(";");
            }

            if (model.DefaultCase is not null)
            {
                sb.AppendLine("            default:");
                sb.Append("                return ").Append(BuildSyncValue(model.DefaultCase.AsyncKind, $"{model.DefaultCase.MethodSymbol.Name}({argumentList})")).AppendLine(";");
            }
            else
            {
                sb.AppendLine("            default:");
                sb.AppendLine("                throw new global::System.ArgumentOutOfRangeException(nameof(key));");
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitKeyedTryCreateBody(
        StringBuilder sb,
        KeyedFactoryModel model,
        string keyTypeName,
        string parameterList,
        string argumentList,
        string resultTypeName)
    {
        sb.Append("    public static bool TryCreate(").Append(keyTypeName).Append(" key, out ").Append(resultTypeName).Append(" value");
        if (parameterList.Length > 0)
        {
            sb.Append(", ").Append(parameterList);
        }
        sb.AppendLine(")");
        sb.AppendLine("    {");

        if (NeedsNullCheck(model.KeyType))
        {
            sb.AppendLine("        if (key is null)");
            sb.AppendLine("        {");
            sb.AppendLine("            value = default!;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        if (IsStringType(model.KeyType))
        {
            foreach (var c in model.Cases)
            {
                sb.Append("        if (global::System.String.Equals(key, ").Append(c.KeyLiteral)
                    .Append(", global::System.StringComparison.")
                    .Append(model.CaseInsensitiveStrings ? "OrdinalIgnoreCase" : "Ordinal").AppendLine("))");
                sb.Append("        {").AppendLine();
                sb.Append("            value = ").Append(BuildSyncValue(c.AsyncKind, $"{c.Method.Name}({argumentList})")).AppendLine(";");
                sb.AppendLine("            return true;");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            if (model.DefaultCase is not null)
            {
                sb.Append("        value = ").Append(BuildSyncValue(model.DefaultCase.AsyncKind, $"{model.DefaultCase.MethodSymbol.Name}({argumentList})")).AppendLine(";");
                sb.AppendLine("        return false;");
            }
            else
            {
                sb.AppendLine("        value = default!;");
                sb.AppendLine("        return false;");
            }
        }
        else
        {
            sb.AppendLine("        switch (key)");
            sb.AppendLine("        {");
            foreach (var c in model.Cases)
            {
                sb.Append("            case ").Append(c.KeyLiteral).AppendLine(":");
                sb.Append("                value = ").Append(BuildSyncValue(c.AsyncKind, $"{c.Method.Name}({argumentList})")).AppendLine(";");
                sb.AppendLine("                return true;");
            }

            if (model.DefaultCase is not null)
            {
                sb.AppendLine("            default:");
                sb.Append("                value = ").Append(BuildSyncValue(model.DefaultCase.AsyncKind, $"{model.DefaultCase.MethodSymbol.Name}({argumentList})")).AppendLine(";");
                sb.AppendLine("                return false;");
            }
            else
            {
                sb.AppendLine("            default:");
                sb.AppendLine("                value = default!;");
                sb.AppendLine("                return false;");
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitKeyedCreateAsync(
        StringBuilder sb,
        KeyedFactoryModel model,
        string keyTypeName,
        string parameterList,
        string argumentList,
        string resultTypeName)
    {
        var asyncReturn = $"global::System.Threading.Tasks.ValueTask<{resultTypeName}>";

        sb.Append("    public static ").Append(asyncReturn).Append(' ').Append(model.CreateMethodName).Append("Async(").Append(keyTypeName).Append(" key");
        if (parameterList.Length > 0)
        {
            sb.Append(", ").Append(parameterList);
        }
        sb.AppendLine(")");
        sb.AppendLine("    {");

        if (NeedsNullCheck(model.KeyType))
        {
            sb.AppendLine("        if (key is null) throw new global::System.ArgumentNullException(nameof(key));");
            sb.AppendLine();
        }

        if (IsStringType(model.KeyType))
        {
            foreach (var c in model.Cases)
            {
                sb.Append("        if (global::System.String.Equals(key, ").Append(c.KeyLiteral)
                    .Append(", global::System.StringComparison.")
                    .Append(model.CaseInsensitiveStrings ? "OrdinalIgnoreCase" : "Ordinal").AppendLine("))");
                sb.Append("            ").Append(BuildAsyncReturn(c.AsyncKind, resultTypeName, $"{c.Method.Name}({argumentList})")).AppendLine();
                sb.AppendLine();
            }

            if (model.DefaultCase is not null)
            {
                sb.Append("        ").Append(BuildAsyncReturn(model.DefaultCase.AsyncKind, resultTypeName, $"{model.DefaultCase.MethodSymbol.Name}({argumentList})")).AppendLine();
            }
            else
            {
                sb.AppendLine("        throw new global::System.ArgumentOutOfRangeException(nameof(key));");
            }
        }
        else
        {
            sb.AppendLine("        switch (key)");
            sb.AppendLine("        {");
            foreach (var c in model.Cases)
            {
                sb.Append("            case ").Append(c.KeyLiteral).AppendLine(":");
                sb.Append("                ").Append(BuildAsyncReturn(c.AsyncKind, resultTypeName, $"{c.Method.Name}({argumentList})")).AppendLine();
            }

            if (model.DefaultCase is not null)
            {
                sb.AppendLine("            default:");
                sb.Append("                ").Append(BuildAsyncReturn(model.DefaultCase.AsyncKind, resultTypeName, $"{model.DefaultCase.MethodSymbol.Name}({argumentList})")).AppendLine();
            }
            else
            {
                sb.AppendLine("            default:");
                sb.AppendLine("                throw new global::System.ArgumentOutOfRangeException(nameof(key));");
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitKeyedTryCreateAsync(
        StringBuilder sb,
        KeyedFactoryModel model,
        string keyTypeName,
        string parameterList,
        string argumentList,
        string resultTypeName)
    {
        var asyncReturn = $"global::System.Threading.Tasks.ValueTask<(bool Success, {resultTypeName} Result)>";
        sb.Append("    public static async ").Append(asyncReturn).Append(" TryCreateAsync(").Append(keyTypeName).Append(" key");
        if (parameterList.Length > 0)
        {
            sb.Append(", ").Append(parameterList);
        }
        sb.AppendLine(")");
        sb.AppendLine("    {");

        if (NeedsNullCheck(model.KeyType))
        {
            sb.AppendLine("        if (key is null) return (false, default!);");
            sb.AppendLine();
        }

        if (IsStringType(model.KeyType))
        {
            foreach (var c in model.Cases)
            {
                sb.Append("        if (global::System.String.Equals(key, ").Append(c.KeyLiteral)
                    .Append(", global::System.StringComparison.")
                    .Append(model.CaseInsensitiveStrings ? "OrdinalIgnoreCase" : "Ordinal").AppendLine("))");
                sb.AppendLine("        {");
                sb.Append("            return (true, ").Append(BuildAwaitedValue(c.AsyncKind, $"{c.Method.Name}({argumentList})")).AppendLine(");");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            if (model.DefaultCase is not null)
            {
                sb.Append("        return (false, ").Append(BuildAwaitedValue(model.DefaultCase.AsyncKind, $"{model.DefaultCase.MethodSymbol.Name}({argumentList})")).AppendLine(");");
            }
            else
            {
                sb.AppendLine("        return (false, default!);");
            }
        }
        else
        {
            sb.AppendLine("        switch (key)");
            sb.AppendLine("        {");
            foreach (var c in model.Cases)
            {
                sb.Append("            case ").Append(c.KeyLiteral).AppendLine(":");
                sb.Append("                return (true, ").Append(BuildAwaitedValue(c.AsyncKind, $"{c.Method.Name}({argumentList})")).AppendLine(");");
            }

            if (model.DefaultCase is not null)
            {
                sb.AppendLine("            default:");
                sb.Append("                return (false, ").Append(BuildAwaitedValue(model.DefaultCase.AsyncKind, $"{model.DefaultCase.MethodSymbol.Name}({argumentList})")).AppendLine(");");
            }
            else
            {
                sb.AppendLine("            default:");
                sb.AppendLine("                return (false, default!);");
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
    }

    private static string EmitCreatorFactory(CreatorFactoryModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");

        var ns = model.BaseType.ContainingNamespace.IsGlobalNamespace
            ? null
            : model.BaseType.ContainingNamespace.ToDisplayString();

        if (!string.IsNullOrWhiteSpace(ns))
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        var accessibility = AccessibilityToString(model.BaseType.DeclaredAccessibility);
        sb.Append(accessibility).Append(" sealed partial class ").Append(model.FactoryName).AppendLine();
        sb.AppendLine("{");

        var keyTypeName = model.KeyType.ToDisplayString(GeneratorUtilities.TypeFormat);
        var baseTypeName = model.BaseType.ToDisplayString(GeneratorUtilities.TypeFormat);

        if (model.GenerateEnumKeys)
        {
            EmitCreatorEnum(sb, model, keyTypeName);
        }

        EmitCreatorCreate(sb, model, keyTypeName, baseTypeName);

        if (model.GenerateEnumKeys)
        {
            EmitCreatorCreateEnum(sb, baseTypeName);
        }

        if (model.GenerateTryCreate)
        {
            EmitCreatorTryCreate(sb, model, keyTypeName, baseTypeName);

            if (model.GenerateEnumKeys)
            {
                EmitCreatorTryCreateEnum(sb, baseTypeName);
            }
        }

        if (model.NeedsAsync)
        {
            EmitCreatorCreateAsync(sb, model, keyTypeName, baseTypeName);

            if (model.GenerateEnumKeys)
            {
                EmitCreatorCreateAsyncEnum(sb, baseTypeName);
            }

            if (model.GenerateTryCreate)
            {
                EmitCreatorTryCreateAsync(sb, model, keyTypeName, baseTypeName);

                if (model.GenerateEnumKeys)
                {
                    EmitCreatorTryCreateAsyncEnum(sb, baseTypeName);
                }
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitCreatorCreate(
        StringBuilder sb,
        CreatorFactoryModel model,
        string keyTypeName,
        string baseTypeName)
    {
        sb.Append("    public ").Append(baseTypeName).Append(" Create(").Append(keyTypeName).AppendLine(" key)");
        sb.AppendLine("    {");

        if (NeedsNullCheck(model.KeyType))
        {
            sb.AppendLine("        if (key is null) throw new global::System.ArgumentNullException(nameof(key));");
            sb.AppendLine();
        }

        sb.AppendLine("        return key switch");
        sb.AppendLine("        {");
        foreach (var product in model.Products)
        {
            sb.Append("            ").Append(product.KeyLiteral).Append(" => ").Append(BuildCreatorCreation(product)).AppendLine(",");
        }

        sb.AppendLine("            _ => throw new global::System.ArgumentOutOfRangeException(nameof(key))");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitCreatorTryCreate(
        StringBuilder sb,
        CreatorFactoryModel model,
        string keyTypeName,
        string baseTypeName)
    {
        sb.Append("    public bool TryCreate(").Append(keyTypeName).Append(" key, out ").Append(baseTypeName).AppendLine(" result)");
        sb.AppendLine("    {");

        if (NeedsNullCheck(model.KeyType))
        {
            sb.AppendLine("        if (key is null)");
            sb.AppendLine("        {");
            sb.AppendLine("            result = default!;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("        switch (key)");
        sb.AppendLine("        {");
        foreach (var product in model.Products)
        {
            sb.Append("            case ").Append(product.KeyLiteral).AppendLine(":");
            sb.Append("                result = ").Append(BuildCreatorCreation(product)).AppendLine(";");
            sb.AppendLine("                return true;");
        }

        sb.AppendLine("            default:");
        sb.AppendLine("                result = default!;");
        sb.AppendLine("                return false;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitCreatorCreateAsync(
        StringBuilder sb,
        CreatorFactoryModel model,
        string keyTypeName,
        string baseTypeName)
    {
        sb.Append("    public ").Append("global::System.Threading.Tasks.ValueTask<").Append(baseTypeName).Append("> CreateAsync(").Append(keyTypeName).AppendLine(" key)");
        sb.AppendLine("    {");

        if (NeedsNullCheck(model.KeyType))
        {
            sb.AppendLine("        if (key is null) throw new global::System.ArgumentNullException(nameof(key));");
            sb.AppendLine();
        }

        sb.AppendLine("        return key switch");
        sb.AppendLine("        {");
        foreach (var product in model.Products)
        {
            sb.Append("            ").Append(product.KeyLiteral).Append(" => ").Append(BuildCreatorAsyncReturn(product, baseTypeName)).AppendLine(",");
        }
        sb.AppendLine("            _ => throw new global::System.ArgumentOutOfRangeException(nameof(key))");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitCreatorTryCreateAsync(
        StringBuilder sb,
        CreatorFactoryModel model,
        string keyTypeName,
        string baseTypeName)
    {
        sb.Append("    public async global::System.Threading.Tasks.ValueTask<(bool Success, ").Append(baseTypeName).Append(" Result)> TryCreateAsync(").Append(keyTypeName).AppendLine(" key)");
        sb.AppendLine("    {");

        if (NeedsNullCheck(model.KeyType))
        {
            sb.AppendLine("        if (key is null) return (false, default!);");
            sb.AppendLine();
        }

        sb.AppendLine("        switch (key)");
        sb.AppendLine("        {");
        foreach (var product in model.Products)
        {
            sb.Append("            case ").Append(product.KeyLiteral).AppendLine(":");
            sb.Append("                return (true, ").Append(BuildCreatorAwaited(product)).AppendLine(");");
        }
        sb.AppendLine("            default:");
        sb.AppendLine("                return (false, default!);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static void EmitCreatorEnum(
        StringBuilder sb,
        CreatorFactoryModel model,
        string keyTypeName)
    {
        sb.AppendLine("    public enum Keys");
        sb.AppendLine("    {");
        foreach (var product in model.Products)
        {
            sb.Append("        ").Append(product.EnumMemberName).Append(',').AppendLine();
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.Append("    private static ").Append(keyTypeName).Append(" MapKey(Keys key) => key switch").AppendLine();
        sb.AppendLine("    {");
        foreach (var product in model.Products)
        {
            sb.Append("        Keys.").Append(product.EnumMemberName).Append(" => ").Append(product.KeyLiteral).AppendLine(",");
        }
        sb.AppendLine("        _ => throw new global::System.ArgumentOutOfRangeException(nameof(key))");
        sb.AppendLine("    };");
        sb.AppendLine();
    }

    private static void EmitCreatorCreateEnum(
        StringBuilder sb,
        string baseTypeName)
    {
        sb.Append("    public ").Append(baseTypeName).AppendLine(" Create(Keys key)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Create(MapKey(key));");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitCreatorTryCreateEnum(
        StringBuilder sb,
        string baseTypeName)
    {
        sb.Append("    public bool TryCreate(Keys key, out ").Append(baseTypeName).AppendLine(" result)");
        sb.AppendLine("    {");
        sb.AppendLine("        return TryCreate(MapKey(key), out result);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitCreatorCreateAsyncEnum(
        StringBuilder sb,
        string baseTypeName)
    {
        sb.Append("    public ").Append("global::System.Threading.Tasks.ValueTask<").Append(baseTypeName).AppendLine("> CreateAsync(Keys key)");
        sb.AppendLine("    {");
        sb.AppendLine("        return CreateAsync(MapKey(key));");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitCreatorTryCreateAsyncEnum(
        StringBuilder sb,
        string baseTypeName)
    {
        sb.Append("    public ").Append("global::System.Threading.Tasks.ValueTask<(bool Success, ").Append(baseTypeName).AppendLine(" Result)> TryCreateAsync(Keys key)");
        sb.AppendLine("    {");
        sb.AppendLine("        return TryCreateAsync(MapKey(key));");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static bool IsStaticPartial(INamedTypeSymbol typeSymbol)
    {
        if (!typeSymbol.IsStatic)
            return false;

        foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDecl &&
                typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadKey(AttributeData attribute, out TypedConstant constant)
    {
        if (attribute.ConstructorArguments.Length == 1)
        {
            constant = attribute.ConstructorArguments[0];
            return true;
        }

        constant = default;
        return false;
    }

    private static MethodInfo BuildFactoryMethodInfo(IMethodSymbol method, Compilation compilation)
    {
        var signature = BuildSignature(method, compilation);
        var asyncKind = signature.AsyncKind;
        return new MethodInfo(method, signature, asyncKind);
    }

    private static MethodSignature BuildSignature(IMethodSymbol method, Compilation compilation)
    {
        var asyncKind = AsyncKind.Sync;
        var returnType = method.ReturnType;
        var resultType = returnType;

        if (IsValueTask(compilation, method.ReturnType, out var vt))
        {
            asyncKind = AsyncKind.ValueTask;
            resultType = vt!;
        }
        else if (IsTask(compilation, method.ReturnType, out var taskType))
        {
            asyncKind = AsyncKind.Task;
            resultType = taskType!;
        }

        return new MethodSignature(resultType, resultType.ToDisplayString(GeneratorUtilities.TypeFormat), method.Parameters, asyncKind);
    }

    private static bool SignatureEquals(MethodSignature left, MethodSignature right)
    {
        if (!SymbolEqualityComparer.Default.Equals(left.ResultType, right.ResultType))
            return false;

        if (left.Parameters.Length != right.Parameters.Length)
            return false;

        for (var i = 0; i < left.Parameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(left.Parameters[i].Type, right.Parameters[i].Type) ||
                left.Parameters[i].RefKind != right.Parameters[i].RefKind)
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildParameterList(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.Length == 0)
            return string.Empty;

        var builder = new StringBuilder();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
                builder.Append(", ");
            builder.Append(parameters[i].ToDisplayString(ParameterFormat));
        }

        return builder.ToString();
    }

    private static string BuildArgumentList(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.Length == 0)
            return string.Empty;

        var builder = new StringBuilder();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
                builder.Append(", ");

            var prefix = parameters[i].RefKind switch
            {
                RefKind.Out => "out ",
                RefKind.Ref => "ref ",
                RefKind.In => "in ",
                _ => string.Empty
            };
            builder.Append(prefix).Append(parameters[i].Name);
        }

        return builder.ToString();
    }

    private static bool IsTask(Compilation compilation, ITypeSymbol type, out ITypeSymbol? result)
    {
        if (type is INamedTypeSymbol named &&
            named.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1")))
        {
            result = named.TypeArguments[0];
            return true;
        }

        result = null;
        return false;
    }

    private static bool IsValueTask(Compilation compilation, ITypeSymbol type, out ITypeSymbol? result)
    {
        if (type is INamedTypeSymbol named &&
            named.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1")))
        {
            result = named.TypeArguments[0];
            return true;
        }

        result = null;
        return false;
    }

    private static bool NeedsNullCheck(ITypeSymbol keyType)
    {
        return keyType.NullableAnnotation == NullableAnnotation.Annotated || keyType.IsReferenceType;
    }

    private static bool IsStringType(ITypeSymbol type)
    {
        return type.SpecialType == SpecialType.System_String;
    }

    private static bool IsKeyCompatible(Compilation compilation, ITypeSymbol keyType, TypedConstant key)
    {
        if (key.IsNull)
        {
            return keyType.IsReferenceType || keyType.NullableAnnotation == NullableAnnotation.Annotated;
        }

        if (key.Type is null)
            return false;

        var conversion = compilation.ClassifyConversion(key.Type, keyType);
        return conversion.Exists && conversion.IsImplicit;
    }

    private static string ToLiteral(TypedConstant constant)
    {
        if (constant.IsNull)
            return "null";

        if (constant.Kind == TypedConstantKind.Enum && constant.Type is not null && constant.Value is not null)
        {
            var enumType = constant.Type.ToDisplayString(GeneratorUtilities.TypeFormat);
            var field = constant.Type.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, constant.Value));
            if (field is not null)
            {
                return $"{enumType}.{field.Name}";
            }

            return $"({enumType}){constant.ToCSharpString()}";
        }

        return constant.ToCSharpString();
    }

    private static string AccessibilityToString(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Private => "private",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "public"
        };
    }

    private static IEqualityComparer<KeyWrapper> BuildKeyComparer(bool caseInsensitiveStrings)
    {
        return new KeyWrapperComparer(caseInsensitiveStrings);
    }

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol target)
    {
        if (SymbolEqualityComparer.Default.Equals(type, target))
            return true;

        if (type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, target)))
            return true;

        var current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
                return true;
            current = current.BaseType;
        }

        return false;
    }

    private static string BuildDefaultFactoryName(INamedTypeSymbol baseType)
    {
        var name = baseType.Name;
        if (baseType.TypeKind == TypeKind.Interface && name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name.Substring(1);
        }
        return name + "Factory";
    }

    private static string BuildEnumMemberName(TypedConstant key, ITypeSymbol keyType, HashSet<string> existing)
    {
        var raw = key.IsNull ? "Null" : key.Kind switch
        {
            TypedConstantKind.Enum when key.Type is not null && key.Value is not null => key.Type
                .GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, key.Value))
                ?.Name ?? key.ToCSharpString(),
            TypedConstantKind.Primitive when key.Value is string s => s,
            TypedConstantKind.Primitive when key.Value is bool b => b ? "True" : "False",
            TypedConstantKind.Primitive when key.Value is IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => key.ToCSharpString()
        };

        var builder = new StringBuilder();
        var capitalize = true;
        foreach (var ch in raw)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(capitalize ? char.ToUpperInvariant(ch) : ch);
                capitalize = false;
            }
            else
            {
                capitalize = true;
            }
        }

        var candidate = builder.Length == 0 ? "Key" : builder.ToString();
        if (!SyntaxFacts.IsIdentifierStartCharacter(candidate[0]))
        {
            candidate = "Key" + candidate;
        }

        var unique = candidate;
        var suffix = 1;
        while (existing.Contains(unique))
        {
            unique = candidate + (++suffix).ToString(CultureInfo.InvariantCulture);
        }

        existing.Add(unique);
        return unique;
    }

    private static string BuildAsyncReturn(AsyncKind asyncKind, string resultTypeName, string invocation)
    {
        return asyncKind switch
        {
            AsyncKind.ValueTask => $"return {invocation};",
            AsyncKind.Task => $"return new global::System.Threading.Tasks.ValueTask<{resultTypeName}>({invocation});",
            _ => $"return global::System.Threading.Tasks.ValueTask.FromResult<{resultTypeName}>({invocation});"
        };
    }

    private static string BuildSyncValue(AsyncKind asyncKind, string invocation)
    {
        return asyncKind switch
        {
            AsyncKind.Sync => invocation,
            _ => $"{invocation}.GetAwaiter().GetResult()"
        };
    }

    private static string BuildAwaitedValue(AsyncKind asyncKind, string invocation)
    {
        return asyncKind switch
        {
            AsyncKind.Sync => invocation,
            _ => $"await {invocation}"
        };
    }

    private static string BuildCreatorCreation(CreatorProduct product)
    {
        if (product.Constructor is not null)
        {
            return $"new {product.Type.ToDisplayString(GeneratorUtilities.TypeFormat)}()";
        }

        if (product.AsyncFactoryMethod is not null)
        {
            var call = $"{product.AsyncFactoryMethod.ContainingType.ToDisplayString(GeneratorUtilities.TypeFormat)}.{product.AsyncFactoryMethod.Name}()";
            return $"{call}.GetAwaiter().GetResult()";
        }

        return "default!";
    }

    private static string BuildCreatorAsyncReturn(CreatorProduct product, string baseTypeName)
    {
        if (product.AsyncFactoryMethod is not null)
        {
            var call = $"{product.AsyncFactoryMethod.ContainingType.ToDisplayString(GeneratorUtilities.TypeFormat)}.{product.AsyncFactoryMethod.Name}()";
            return product.Kind switch
            {
                CreationKind.ValueTask => call,
                CreationKind.Task => $"new global::System.Threading.Tasks.ValueTask<{baseTypeName}>({call})",
                _ => call
            };
        }

        return $"global::System.Threading.Tasks.ValueTask.FromResult<{baseTypeName}>(new {product.Type.ToDisplayString(GeneratorUtilities.TypeFormat)}())";
    }

    private static string BuildCreatorAwaited(CreatorProduct product)
    {
        if (product.AsyncFactoryMethod is not null)
        {
            var call = $"{product.AsyncFactoryMethod.ContainingType.ToDisplayString(GeneratorUtilities.TypeFormat)}.{product.AsyncFactoryMethod.Name}()";
            return $"await {call}";
        }

        return $"new {product.Type.ToDisplayString(GeneratorUtilities.TypeFormat)}()";
    }

    private static string ReadNamedArgument(AttributeData attribute, string name, string fallback)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is string s)
            {
                return s;
            }
        }
        return fallback;
    }

    private static bool ReadNamedArgument(AttributeData attribute, string name, bool fallback)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is bool b)
            {
                return b;
            }
        }
        return fallback;
    }

    private static string? ReadOptionalNamedArgument(AttributeData attribute, string name)
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

    private static IMethodSymbol? FindAsyncFactory(Compilation compilation, INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (!method.IsStatic || method.Name != "CreateAsync")
                continue;
            if (method.Parameters.Length != 0)
                continue;

            var returnType = method.ReturnType;
            if (returnType is not INamedTypeSymbol named || !named.IsGenericType)
                continue;

            var targetType = named.TypeArguments[0];
            var conversion = compilation.ClassifyConversion(targetType, baseType);
            if (!conversion.Exists)
                continue;

            return method;
        }

        return null;
    }

    private static class Diagnostics
    {
        public static readonly DiagnosticDescriptor KeyedMustBeStaticPartial = new(
            "PKKF001",
            "Factory method host must be static partial",
            "Type '{0}' must be a static partial class to use [FactoryMethod]",
            "PatternKit.FactoryMethod",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor KeyedSignatureMismatch = new(
            "PKKF002",
            "Factory methods must share the same signature",
            "All [FactoryCase] and [FactoryDefault] methods for '{0}' must have matching signatures",
            "PatternKit.FactoryMethod",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor KeyedDuplicateKey = new(
            "PKKF003",
            "Duplicate factory key",
            "Key '{0}' is already defined for factory '{1}'",
            "PatternKit.FactoryMethod",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor KeyedMultipleDefaults = new(
            "PKKF004",
            "Multiple default factory methods",
            "Only one [FactoryDefault] method may be declared for '{0}'",
            "PatternKit.FactoryMethod",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor KeyedInvalidKeyValue = new(
            "PKKF005",
            "Invalid factory key value",
            "Factory key for '{0}' is not compatible with the declared KeyType '{1}'",
            "PatternKit.FactoryMethod",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor KeyedMethodsMustBeStatic = new(
            "PKKF006",
            "Factory methods must be static",
            "Factory method '{0}' must be static",
            "PatternKit.FactoryMethod",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CreatorMustBeInterfaceOrAbstract = new(
            "PKCF001",
            "Factory class base must be abstract",
            "[FactoryClass] can only be applied to an interface or abstract class ('{0}')",
            "PatternKit.FactoryClass",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CreatorMultipleBases = new(
            "PKCF002",
            "FactoryClassKey type maps to multiple bases",
            "Type '{0}' must implement exactly one [FactoryClass] base type",
            "PatternKit.FactoryClass",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CreatorMustBeConcrete = new(
            "PKCF003",
            "FactoryClassKey type must be concrete",
            "Type '{0}' must be non-abstract and supply an accessible constructor",
            "PatternKit.FactoryClass",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CreatorDuplicateKey = new(
            "PKCF004",
            "Duplicate factory key",
            "Key '{0}' is already defined for factory '{1}'",
            "PatternKit.FactoryClass",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CreatorInvalidKeyValue = new(
            "PKCF005",
            "Invalid factory key value",
            "Key value for '{0}' is not compatible with the declared KeyType",
            "PatternKit.FactoryClass",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CreatorMissingCtor = new(
            "PKCF006",
            "Missing accessible constructor",
            "Type '{0}' must expose a parameterless constructor or static CreateAsync method",
            "PatternKit.FactoryClass",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }

    private sealed record FactoryCaseInfo(IMethodSymbol Method, AttributeData Attribute);

    private sealed record MethodInfo(IMethodSymbol MethodSymbol, MethodSignature Signature, AsyncKind AsyncKind);

    private sealed record MethodSignature(ITypeSymbol ResultType, string ResultTypeName, ImmutableArray<IParameterSymbol> Parameters, AsyncKind AsyncKind);

    private sealed record KeyedFactoryResult(KeyedFactoryModel? Model, ImmutableArray<Diagnostic> Diagnostics);

    private sealed record KeyedMembers(ImmutableArray<FactoryCaseInfo> Cases, ImmutableArray<IMethodSymbol> DefaultMethods);

    private readonly record struct KeyedBuildResult(
        MethodSignature Signature,
        ImmutableArray<KeyedCase> Cases,
        MethodInfo? DefaultCase,
        bool HasAsync);

    private sealed record KeyedFactoryModel(
        INamedTypeSymbol Type,
        ITypeSymbol KeyType,
        string CreateMethodName,
        bool CaseInsensitiveStrings,
        MethodSignature Signature,
        ImmutableArray<KeyedCase> Cases,
        MethodInfo? DefaultCase,
        bool HasAsync);

    private sealed record KeyedCase(string KeyLiteral, IMethodSymbol Method, AsyncKind AsyncKind);

    private sealed record CreatorBaseResult(CreatorFactoryBase? Model, ImmutableArray<Diagnostic> Diagnostics);

    private sealed record CreatorFactoryBase(
        INamedTypeSymbol Type,
        ITypeSymbol KeyType,
        string? FactoryTypeName,
        bool GenerateTryCreate,
        bool GenerateEnumKeys);

    private sealed record CreatorKeyResult(CreatorKeyModel? Model, ImmutableArray<Diagnostic> Diagnostics);

    private sealed record CreatorKeyModel(INamedTypeSymbol Type, TypedConstant Key);

    private sealed record CreatorFactoryModel(
        INamedTypeSymbol BaseType,
        ITypeSymbol KeyType,
        string FactoryName,
        bool GenerateTryCreate,
        bool GenerateEnumKeys,
        ImmutableArray<CreatorProduct> Products,
        bool NeedsAsync);

    private sealed record CreatorProduct(
        INamedTypeSymbol Type,
        string KeyLiteral,
        CreationKind Kind,
        IMethodSymbol? Constructor,
        IMethodSymbol? AsyncFactoryMethod,
        string? EnumMemberName);

    private sealed record KeyWrapper(TypedConstant Constant, ITypeSymbol KeyType);

    private sealed class KeyWrapperComparer : IEqualityComparer<KeyWrapper>
    {
        private readonly bool _caseInsensitive;

        public KeyWrapperComparer(bool caseInsensitive)
        {
            _caseInsensitive = caseInsensitive;
        }

        public bool Equals(KeyWrapper? x, KeyWrapper? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x is null || y is null)
                return false;

            if (x.Constant.IsNull && y.Constant.IsNull)
                return true;

            if (IsStringType(x.KeyType))
            {
                var left = x.Constant.Value?.ToString() ?? string.Empty;
                var right = y.Constant.Value?.ToString() ?? string.Empty;
                return string.Equals(left, right, _caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }

            return Equals(x.Constant.Value, y.Constant.Value);
        }

        public int GetHashCode(KeyWrapper obj)
        {
            if (obj.Constant.IsNull)
                return 0;

            if (IsStringType(obj.KeyType))
            {
                var value = obj.Constant.Value?.ToString() ?? string.Empty;
                return _caseInsensitive
                    ? StringComparer.OrdinalIgnoreCase.GetHashCode(value)
                    : StringComparer.Ordinal.GetHashCode(value);
            }

            return obj.Constant.Value?.GetHashCode() ?? 0;
        }
    }

    private enum AsyncKind
    {
        Sync,
        Task,
        ValueTask
    }

    private enum CreationKind
    {
        Sync,
        Task,
        ValueTask
    }
}
