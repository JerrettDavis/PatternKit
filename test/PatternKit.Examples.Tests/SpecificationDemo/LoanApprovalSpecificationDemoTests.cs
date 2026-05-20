using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.SpecificationDemo;
using TinyBDD;
using static PatternKit.Examples.SpecificationDemo.LoanApprovalSpecificationDemo;

namespace PatternKit.Examples.Tests.SpecificationDemo;

public sealed class LoanApprovalSpecificationDemoTests
{
    [Scenario("Fluent and generated specifications approve the same prime application")]
    [Fact]
    public void Fluent_And_Generated_Specifications_Approve_The_Same_Prime_Application()
    {
        var application = CreatePrimeApplication();
        var fluent = CreateFluentRegistry();
        var generated = CreateGeneratedRegistry();

        var fluentDecision = Evaluate(application, fluent);
        var generatedDecision = Evaluate(application, generated);

        ScenarioExpect.True(fluentDecision.Approved);
        ScenarioExpect.Equal(fluentDecision, generatedDecision);
        ScenarioExpect.Equal(["affordable", "approval-ready", "clear-fraud", "prime-credit", "stable-income", "verified-identity"], generated.Names.OrderBy(static name => name).ToArray());
    }

    [Scenario("Generated specifications explain failed loan rules")]
    [Fact]
    public void Generated_Specifications_Explain_Failed_Loan_Rules()
    {
        var application = new LoanApplication("APP-300", 90_000m, 50_000m, 680, 4, false, false);
        var registry = CreateGeneratedRegistry();

        var decision = Evaluate(application, registry);

        ScenarioExpect.False(decision.Approved);
        ScenarioExpect.Equal(["affordable", "prime-credit", "stable-income", "verified-identity"], decision.FailedRules.OrderBy(static name => name).ToArray());
    }

    [Scenario("Loan approval specifications integrate with IServiceCollection")]
    [Fact]
    public void Loan_Approval_Specifications_Integrate_With_IServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLoanApprovalSpecifications();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var service = provider.GetRequiredService<LoanApprovalService>();
        var approved = service.Evaluate(CreatePrimeApplication());
        var held = service.Evaluate(CreateHeldApplication());

        ScenarioExpect.True(approved.Approved);
        ScenarioExpect.False(held.Approved);
        ScenarioExpect.Contains("clear-fraud", held.FailedRules);
    }
}
