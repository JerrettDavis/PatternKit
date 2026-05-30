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
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator reports invalid service layer declarations")]
    [Theory]
    [InlineData("public static class RegisterCustomerService { [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL001")]
    [InlineData("public static partial class RegisterCustomerService;", "PKSL002")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerHandler] private static ValueTask<CustomerReceipt> One(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Two(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL002")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerHandler] private static CustomerReceipt Handle(RegisterCustomer request) => new(request.Email); }", "PKSL003")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerHandler] private ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL003")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Handle(string request, CancellationToken cancellationToken) => new(new CustomerReceipt(request)); }", "PKSL003")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerHandler] private static ValueTask<string> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(request.Email); }", "PKSL003")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerRule(\"email\", \"Email is required.\", 1)] private static string HasEmail(RegisterCustomer request) => request.Email; [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL004")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerRule(\"email\", \"Email is required.\", 1)] private bool HasEmail(RegisterCustomer request) => true; [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL004")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerRule(\"email\", \"Email is required.\", 1)] private static bool HasEmail() => true; [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL004")]
    [InlineData("public static partial class RegisterCustomerService { [ServiceLayerRule(\"email\", \"Email is required.\", 1)] private static bool HasEmail(string request) => true; [ServiceLayerHandler] private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email)); }", "PKSL004")]
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

    [Scenario("Generator emits service layer defaults and host shapes")]
    [Fact]
    public Task Generator_Emits_Service_Layer_Defaults_And_Host_Shapes()
        => Given("valid service layer declarations with default names and host shapes", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.ServiceLayer;
            namespace Demo;
            public sealed record RegisterCustomer(string Email);
            public sealed record CustomerReceipt(string Email);

            [GenerateServiceLayerOperation(typeof(RegisterCustomer), typeof(CustomerReceipt))]
            internal abstract partial class AbstractRegisterCustomerService
            {
                [ServiceLayerHandler]
                private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email));
            }

            [GenerateServiceLayerOperation(typeof(RegisterCustomer), typeof(CustomerReceipt), OperationName = "tenant\\\"registration")]
            public sealed partial class SealedRegisterCustomerService
            {
                [ServiceLayerRule("email", "Email is required.", 1)]
                private static bool HasEmail(RegisterCustomer request) => !string.IsNullOrWhiteSpace(request.Email);
                [ServiceLayerHandler]
                private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email));
            }

            [GenerateServiceLayerOperation(typeof(RegisterCustomer), typeof(CustomerReceipt))]
            internal partial struct StructRegisterCustomerService
            {
                [ServiceLayerHandler]
                private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email));
            }
            """))
        .Then("generated sources preserve host shape and configured defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractRegisterCustomerService", combined);
            ScenarioExpect.Contains("public sealed partial class SealedRegisterCustomerService", combined);
            ScenarioExpect.Contains("internal partial struct StructRegisterCustomerService", combined);
            ScenarioExpect.Contains("Create(\"AbstractRegisterCustomerService\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"registration\")", combined);
            ScenarioExpect.Contains("Require(\"email\", \"Email is required.\", HasEmail)", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator emits nested service layer host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Service_Layer_Host_Wrappers()
        => Given("nested service layer declarations", () => Compile("""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.ServiceLayer;
            namespace Demo;
            public sealed record RegisterCustomer(string Email);
            public sealed record CustomerReceipt(string Email);

            public partial class ServiceLayerContainer
            {
                private partial class PrivateHost
                {
                    [GenerateServiceLayerOperation(typeof(RegisterCustomer), typeof(CustomerReceipt))]
                    protected partial class ProtectedServiceLayer
                    {
                        [ServiceLayerHandler]
                        private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email));
                    }

                    [GenerateServiceLayerOperation(typeof(RegisterCustomer), typeof(CustomerReceipt))]
                    private protected partial class PrivateProtectedServiceLayer
                    {
                        [ServiceLayerHandler]
                        private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email));
                    }

                    [GenerateServiceLayerOperation(typeof(RegisterCustomer), typeof(CustomerReceipt))]
                    protected internal partial class ProtectedInternalServiceLayer
                    {
                        [ServiceLayerHandler]
                        private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email));
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class ServiceLayerContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedServiceLayer", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedServiceLayer", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalServiceLayer", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator skips malformed service layer type arguments")]
    [Theory]
    [InlineData("null!", "typeof(CustomerReceipt)")]
    [InlineData("typeof(RegisterCustomer)", "null!")]
    public Task Generator_Skips_Malformed_Service_Layer_Type_Arguments(string requestType, string responseType)
        => Given("a service layer declaration with a null type argument", () => Compile($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Generators.ServiceLayer;
            public sealed record RegisterCustomer(string Email);
            public sealed record CustomerReceipt(string Email);
            [GenerateServiceLayerOperation({{requestType}}, {{responseType}})]
            public static partial class RegisterCustomerService
            {
                [ServiceLayerHandler]
                private static ValueTask<CustomerReceipt> Handle(RegisterCustomer request, CancellationToken cancellationToken) => new(new CustomerReceipt(request.Email));
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "ServiceLayerGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(ServiceLayerOperation<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new ServiceLayerOperationGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);
}
