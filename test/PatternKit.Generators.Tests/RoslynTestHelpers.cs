using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PatternKit.Generators.Tests;

public static class RoslynTestHelpers
{
    private static readonly StringComparison OrdIgnoreCase = StringComparison.InvariantCultureIgnoreCase;

    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName,
        LanguageVersion lang = LanguageVersion.Preview,
        params MetadataReference[]? extra)
    {
        var parse = new CSharpParseOptions(lang);
        var tree = CSharpSyntaxTree.ParseText(source, parse);

        var refs = new List<MetadataReference>
        {
            RefFromTPA("System.Private.CoreLib.dll"),
            RefFromTPA("System.Runtime.dll"),
            RefFromTPA("System.Console.dll"),
            RefFromTPA("System.Collections.dll"),
            RefFromTPA("System.Linq.dll"),
            RefFromTPA("System.Memory.dll"),
            RefFromTPA("System.Runtime.Extensions.dll"),
            RefFromTPA("netstandard.dll"),
            MetadataReference.CreateFromFile(typeof(PatternKit.Generators.Builders.GenerateBuilderAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(PatternKit.Generators.Builders.BuilderGenerator).Assembly.Location),
        };
        if (extra is not null) refs.AddRange(extra);

        return CSharpCompilation.Create(
            assemblyName,
            [tree],
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }


    private static MetadataReference RefFromTPA(string simpleName)
    {
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        var path = tpa.First(p => string.Equals(Path.GetFileName(p), simpleName, OrdIgnoreCase));
        return MetadataReference.CreateFromFile(path);
    }

    public static GeneratorDriver Run(
        Compilation compilation,
        IIncrementalGenerator gen,
        out GeneratorDriverRunResult result,
        out Compilation updated)
    {
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;

        // Convert incremental -> classic source generator
        var sg = gen.AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [sg],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out updated, out _);
        result = driver.GetRunResult();
        return driver;
    }

    public static GeneratorDriver Run(
        Compilation compilation,
        ISourceGenerator gen,
        out GeneratorDriverRunResult result,
        out Compilation updated)
    {
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [gen],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out updated, out _);
        result = driver.GetRunResult();
        return driver;
    }
}
