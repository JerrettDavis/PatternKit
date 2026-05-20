using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ProductionReadiness;

[Feature("GoF pattern coverage catalog")]
public sealed class PatternKitPatternCatalogTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static readonly string[] CanonicalGofPatterns =
    [
        "Abstract Factory",
        "Builder",
        "Factory Method",
        "Prototype",
        "Singleton",
        "Adapter",
        "Bridge",
        "Composite",
        "Decorator",
        "Facade",
        "Flyweight",
        "Proxy",
        "Chain of Responsibility",
        "Command",
        "Interpreter",
        "Iterator",
        "Mediator",
        "Memento",
        "Observer",
        "State",
        "Strategy",
        "Template Method",
        "Visitor"
    ];

    [Scenario("Catalog covers every canonical GoF pattern")]
    [Fact]
    public Task Catalog_Covers_Every_Canonical_Gof_Pattern()
        => Given("the PatternKit pattern catalog", () => new PatternKitPatternCatalog())
            .When("reading the catalog entries", catalog => catalog.Patterns)
            .Then("all canonical GoF patterns are represented exactly once", patterns =>
            {
                ScenarioExpect.Equal(CanonicalGofPatterns.OrderBy(static x => x), patterns.Select(static p => p.Name).OrderBy(static x => x));
                ScenarioExpect.Equal(CanonicalGofPatterns.Length, patterns.Select(static p => p.Name).Distinct(StringComparer.Ordinal).Count());
            })
            .And("the catalog keeps the GoF family counts honest", patterns =>
            {
                ScenarioExpect.Equal(5, patterns.Count(static p => p.Family == PatternFamily.Creational));
                ScenarioExpect.Equal(7, patterns.Count(static p => p.Family == PatternFamily.Structural));
                ScenarioExpect.Equal(11, patterns.Count(static p => p.Family == PatternFamily.Behavioral));
            })
            .AssertPassed();

    [Scenario("Each pattern has fluent generated documented and example paths")]
    [Fact]
    public Task Each_Pattern_Has_Fluent_Generated_Documented_And_Example_Paths()
        => Given("the PatternKit pattern catalog and repository root", () => new
            {
                Catalog = new PatternKitPatternCatalog(),
                RepositoryRoot = FindRepoRoot()
            })
            .When("validating implementation paths", ctx => ctx.Catalog.Patterns
                .SelectMany(pattern => ValidatePattern(ctx.RepositoryRoot, pattern))
                .ToArray())
            .Then("all fluent documentation source tests and examples exist", issues =>
                ScenarioExpect.Empty(issues.Where(static issue => !issue.Contains("tracked source-generated gap", StringComparison.Ordinal))))
            .And("only approved source-generator gaps remain tracked", issues =>
            {
                var tracked = issues
                    .Where(static issue => issue.Contains("tracked source-generated gap", StringComparison.Ordinal))
                    .OrderBy(static issue => issue)
                    .ToArray();

                ScenarioExpect.Equal(
                [
                    "Abstract Factory has a tracked source-generated gap: https://github.com/JerrettDavis/PatternKit/issues/207",
                    "Interpreter has a tracked source-generated gap: https://github.com/JerrettDavis/PatternKit/issues/206"
                ], tracked);
            })
            .AssertPassed();

    [Scenario("Pattern catalog is available through IServiceCollection")]
    [Fact]
    public Task Pattern_Catalog_Is_Available_Through_IServiceCollection()
        => Given("a service collection configured with the pattern catalog", () =>
            {
                var services = new ServiceCollection();
                services.AddPatternKitPatternCatalog();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving the catalog", provider =>
            {
                using (provider)
                    return provider.GetRequiredService<IPatternKitPatternCatalog>();
            })
            .Then("the catalog resolves all GoF patterns", catalog =>
                ScenarioExpect.Equal(CanonicalGofPatterns.Length, catalog.Patterns.Count))
            .And("all patterns include user-facing integration notes", catalog =>
                ScenarioExpect.True(catalog.Patterns.All(static pattern => pattern.IntegrationNotes.Count > 0)))
            .AssertPassed();

    private static IEnumerable<string> ValidatePattern(string repositoryRoot, PatternCoverageDescriptor pattern)
    {
        var implementation = pattern.Implementation;

        foreach (var issue in ValidatePath(repositoryRoot, pattern.Name, "fluent docs", implementation.FluentDocumentationPath))
            yield return issue;
        foreach (var issue in ValidatePath(repositoryRoot, pattern.Name, "fluent source", implementation.FluentSourcePath))
            yield return issue;
        foreach (var issue in ValidatePath(repositoryRoot, pattern.Name, "fluent tests", implementation.FluentTestPath))
            yield return issue;
        foreach (var issue in ValidatePath(repositoryRoot, pattern.Name, "example docs", implementation.ExampleDocumentationPath))
            yield return issue;
        foreach (var issue in ValidatePath(repositoryRoot, pattern.Name, "example source", implementation.ExampleSourcePath))
            yield return issue;
        foreach (var issue in ValidatePath(repositoryRoot, pattern.Name, "example tests", implementation.ExampleTestPath))
            yield return issue;

        if (implementation.HasSourceGeneratedPath)
        {
            foreach (var issue in ValidatePath(repositoryRoot, pattern.Name, "generator docs", implementation.GeneratorDocumentationPath!))
                yield return issue;
            foreach (var issue in ValidatePath(repositoryRoot, pattern.Name, "generator source", implementation.GeneratorSourcePath!))
                yield return issue;
            foreach (var issue in ValidatePath(repositoryRoot, pattern.Name, "generator tests", implementation.GeneratorTestPath!))
                yield return issue;
        }
        else if (implementation.HasTrackedGeneratorGap)
        {
            yield return $"{pattern.Name} has a tracked source-generated gap: {implementation.TrackingIssueUrl}";
        }
        else
        {
            yield return $"{pattern.Name} is missing a source-generated path and a tracking issue.";
        }
    }

    private static IEnumerable<string> ValidatePath(string repositoryRoot, string patternName, string surface, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            yield return $"{patternName} {surface} path is required.";
            yield break;
        }

        var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(fullPath))
            yield return $"{patternName} {surface} path does not exist: {relativePath}";
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PatternKit.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find PatternKit repository root.");
    }
}
