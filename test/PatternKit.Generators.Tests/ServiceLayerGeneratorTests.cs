using Microsoft.CodeAnalysis;
using PatternKit.Application.ServiceLayer;
using PatternKit.Generators.ServiceLayer;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Service Layer generator")]
public sealed partial class ServiceLayerGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits service layer operation factory")]
    [Fact]
    public Task Generator_Emits_Service_Layer_Operation_Factory()
        => Given("a valid service layer declaration", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.ServiceLayer;
            namespace Demo;
            public sealed record RegisterCustomer(string Email);
            public sealed record CustomerReceipt(string Email);
            [GenerateServiceLayerOperation(typeof(RegisterCustomer), typeof(CustomerReceipt), FactoryName = "Build", OperationName = "register-customer")]
            public static partial class RegisterCustomerService
            {
                [ServiceLayerRule("tenant", "Tenant is required.", 1)]
                private static bool HasTenant(RegisterCustomer request) => true;
                [ServiceLayerRule("email", "Email is required.", 2)]
                private static bool HasEmail(RegisterCustomer request) => !string.IsNullOrWhiteSpace(request.Email);
                [ServiceLayerHandler]
                private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email));
            }
            """))
        .Then("generated source creates the operation with ordered rules and handler", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("Create(\"register-customer\")", source);
            ScenarioExpect.Contains("Require(\"tenant\", \"Tenant is required.\", HasTenant)", source);
            ScenarioExpect.Contains("Require(\"email\", \"Email is required.\", HasEmail)", source);
            ScenarioExpect.Contains(".Handle(Handle).Build()", source);
            ScenarioExpect.True(source.IndexOf("HasTenant", StringComparison.Ordinal) < source.IndexOf("HasEmail", StringComparison.Ordinal));
        })
        .AssertPassed();

    [Scenario("Generator reports invalid service layer declarations")]
    [Theory]
    [InlineData("public static class RegisterCustomerService { [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL001")]
    [InlineData("public static partial class RegisterCustomerService;", "PKSL002")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerHandler] private static ValueTask<CustomerReceipt> One(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Two(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL002")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerHandler] private static CustomerReceipt Handle(RegisterCustomer request) => new(request.Email); }", "PKSL003")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerRule(\"email\", \"Email is required.\", 1)] private static string HasEmail(RegisterCustomer request) => request.Email; [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL004")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerRule(\"email\", \"Email is required.\", 1)] private static bool HasEmail(RegisterCustomer request) => true; [ServiceLayerRule(\"tenant\", \"Tenant is required.\", 1)] private static bool HasTenant(RegisterCustomer request) => true; [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL005")]
    public Task Generator_Reports_Invalid_Service_Layer_Declarations(string declaration, string diagnosticId)
        => Given("an invalid service layer declaration", () => Compile($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.ServiceLayer;
            public sealed record RegisterCustomer(string Email);
            public sealed record CustomerReceipt(string Email);
            [GenerateServiceLayerOperation(typeof(RegisterCustomer), typeof(CustomerReceipt))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "ServiceLayerGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(ServiceLayerOperation<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new ServiceLayerOperationGenerator(), out var run, out _);
        var result = run.Results.Single();
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources);
}
