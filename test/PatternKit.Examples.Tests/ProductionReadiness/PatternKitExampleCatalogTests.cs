using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ProductionReadiness;

[Feature("Production-ready example catalog integrations")]
public sealed class PatternKitExampleCatalogTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Catalog entries map documented examples to source, tests, and production checks")]
    [Fact]
    public Task Catalog_Entries_Map_Documented_Examples_To_Source_Tests_And_Production_Checks()
        => Given("an examples catalog and the repository root", () => new
        {
            Catalog = new PatternKitExampleCatalog(),
            RepositoryRoot = FindRepoRoot()
        })
            .When("validating the catalog against repository files", ctx => new
            {
                ctx.Catalog.Entries,
                Report = ctx.Catalog.Validate(ctx.RepositoryRoot)
            })
            .Then("every catalog entry is valid", result => result.Report.IsValid)
            .And("the catalog covers every docs example toc page", result =>
                ExampleTocHrefs()
                    .Where(href => !string.Equals(href, "index.md", StringComparison.OrdinalIgnoreCase))
                    .All(href => result.Entries.Any(entry => string.Equals(
                        entry.DocumentationPath,
                        $"docs/examples/{href}",
                        StringComparison.OrdinalIgnoreCase))))
            .And("all entries describe integration surfaces and production checks", result =>
                result.Entries.All(entry =>
                    entry.Integration != ExampleIntegrationSurface.None
                    && entry.Patterns.Count > 0
                    && entry.ProductionChecks.Count > 0))
            .AssertPassed();

    [Scenario("ServiceCollection exposes the examples catalog to consumers")]
    [Fact]
    public Task ServiceCollection_Exposes_Examples_Catalog_To_Consumers()
        => Given("a service collection configured with the catalog", () =>
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddPatternKitExampleCatalog(options => options.RepositoryRoot = FindRepoRoot());
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and validating the catalog", provider =>
            {
                var catalog = provider.GetRequiredService<IPatternKitExampleCatalog>();
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PatternKitExampleCatalogOptions>>();

                return new
                {
                    Catalog = catalog,
                    Report = catalog.Validate(options.Value.RepositoryRoot)
                };
            })
            .Then("the catalog resolves from DI", result => result.Catalog.Entries.Count >= ExampleTocHrefs().Count - 1)
            .And("the configured repository validation succeeds", result => result.Report.IsValid)
            .AssertPassed();

    [Scenario("Generic host validates example metadata on startup")]
    [Fact]
    public Task Generic_Host_Validates_Example_Metadata_On_Startup()
        => Given("a generic host with hosted catalog validation", () =>
            {
                var builder = Host.CreateApplicationBuilder();
                builder.AddPatternKitExampleHostedValidation(options => options.RepositoryRoot = FindRepoRoot());
                return builder.Build();
            })
            .When("starting the host", host =>
            {
                host.StartAsync().GetAwaiter().GetResult();
                host.StopAsync().GetAwaiter().GetResult();
                host.Dispose();
                return true;
            })
            .Then("startup validation succeeds", started => started)
            .AssertPassed();

    [Scenario("Generic host fails fast when example metadata is invalid")]
    [Fact]
    public Task Generic_Host_Fails_Fast_When_Example_Metadata_Is_Invalid()
        => Given("a generic host with hosted catalog validation and a missing repository root", () =>
            {
                var builder = Host.CreateApplicationBuilder();
                builder.AddPatternKitExampleHostedValidation(options =>
                {
                    options.RepositoryRoot = Path.Combine(Path.GetTempPath(), $"patternkit-missing-examples-root-{Guid.NewGuid():N}");
                    options.FailOnInvalid = true;
                });
                return builder.Build();
            })
            .When("starting the host", host =>
            {
                try
                {
                    host.StartAsync().GetAwaiter().GetResult();
                    return null;
                }
                catch (InvalidOperationException ex)
                {
                    return ex;
                }
                finally
                {
                    host.Dispose();
                }
            })
            .Then("startup fails with a catalog validation error", exception =>
                exception is not null
                && exception.Message.Contains("PatternKit example catalog validation failed", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("ASP.NET Core endpoint exposes the examples catalog")]
    [Fact]
    public Task AspNetCore_Endpoint_Exposes_Examples_Catalog()
        => Given("an ASP.NET Core app mapped with PatternKit example catalog endpoints", () =>
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    EnvironmentName = Environments.Development
                });

                builder.Services.AddPatternKitExampleCatalog(options => options.RepositoryRoot = FindRepoRoot());

                var app = builder.Build();
                app.MapPatternKitExampleCatalog();
                return app;
            })
            .When("inspecting mapped endpoints and resolving the catalog", app =>
            {
                var endpoints = ((IEndpointRouteBuilder)app).DataSources
                    .SelectMany(source => source.Endpoints)
                    .ToArray();
                var catalog = app.Services.GetRequiredService<IPatternKitExampleCatalog>();
                var report = catalog.Validate(FindRepoRoot());
                app.DisposeAsync().AsTask().GetAwaiter().GetResult();

                return new
                {
                    EndpointNames = endpoints
                        .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
                        .OfType<string>()
                        .ToArray(),
                    report.IsValid
                };
            })
            .Then("the catalog endpoint is mapped", result =>
                result.EndpointNames.Contains("PatternKitExampleCatalog", StringComparer.Ordinal))
            .And("the validation endpoint is mapped and the report is valid", result =>
                result.EndpointNames.Contains("PatternKitExampleCatalogValidation", StringComparer.Ordinal)
                && result.IsValid)
            .AssertPassed();

    private static IReadOnlyList<string> ExampleTocHrefs()
    {
        var tocPath = Path.Combine(FindRepoRoot(), "docs", "examples", "toc.yml");
        return File.ReadAllLines(tocPath)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("href: ", StringComparison.Ordinal))
            .Select(line => line["href: ".Length..])
            .Where(href => href.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
