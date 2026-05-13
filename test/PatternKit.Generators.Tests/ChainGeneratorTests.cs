using PatternKit.Generators.Chain;

namespace PatternKit.Generators.Tests;

public class ChainGeneratorTests
{
    [Fact]
    public void GeneratesResponsibilityChain()
    {
        const string source = """
            using PatternKit.Generators.Chain;

            namespace TestNamespace;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Chain]
            public partial class Router
            {
                [ChainHandler(Order = 0)]
                private bool TryHealth(in Request request, out Response response)
                {
                    response = new Response(200);
                    return request.Path == "/health";
                }

                [ChainDefault]
                private Response NotFound(in Request request) => new(404);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratesResponsibilityChain));
        var gen = new ChainGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "Router.Chain.g.cs").SourceText.ToString();
        Assert.Contains("public bool TryHandle(in global::TestNamespace.Request input, out global::TestNamespace.Response output)", generated);
        Assert.Contains("public global::TestNamespace.Response Handle", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ReportsDuplicateOrder()
    {
        const string source = """
            using PatternKit.Generators.Chain;

            namespace TestNamespace;

            public readonly record struct Request(string Path);
            public readonly record struct Response(int Status);

            [Chain]
            public partial class Router
            {
                [ChainHandler(Order = 0)] private bool A(in Request request, out Response response) { response = default; return false; }
                [ChainHandler(Order = 0)] private bool B(in Request request, out Response response) { response = default; return false; }
                [ChainDefault] private Response NotFound(in Request request) => new(404);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsDuplicateOrder));
        var gen = new ChainGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);
        Assert.Contains(result.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKCH003");
    }
}
