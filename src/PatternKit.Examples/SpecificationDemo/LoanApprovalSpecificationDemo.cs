using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.Specification;
using PatternKit.Generators.Specification;

namespace PatternKit.Examples.SpecificationDemo;

/// <summary>
/// Production-style loan approval rules implemented with fluent and source-generated specifications.
/// </summary>
public static class LoanApprovalSpecificationDemo
{
    public sealed record LoanApplication(
        string ApplicationId,
        decimal AnnualIncome,
        decimal RequestedAmount,
        int CreditScore,
        int MonthsEmployed,
        bool HasFraudHold,
        bool HasVerifiedIdentity);

    public sealed record LoanDecision(string ApplicationId, bool Approved, IReadOnlyList<string> FailedRules);

    public static SpecificationRegistry<LoanApplication> CreateFluentRegistry()
    {
        var verified = Specification<LoanApplication>.Where("verified-identity", static application => application.HasVerifiedIdentity);
        var clearFraud = Specification<LoanApplication>.Where("clear-fraud", static application => !application.HasFraudHold);
        var primeCredit = Specification<LoanApplication>.Where("prime-credit", static application => application.CreditScore >= 700);
        var stableIncome = Specification<LoanApplication>.Where("stable-income", static application => application.MonthsEmployed >= 12);
        var affordable = Specification<LoanApplication>.Where("affordable", static application =>
            application.AnnualIncome > 0m && application.RequestedAmount <= application.AnnualIncome * 0.35m);
        var approval = verified.And(clearFraud).And(primeCredit).And(stableIncome).And(affordable, "approval-ready");

        return SpecificationRegistry<LoanApplication>.Create()
            .Add(verified.Name, verified)
            .Add(clearFraud.Name, clearFraud)
            .Add(primeCredit.Name, primeCredit)
            .Add(stableIncome.Name, stableIncome)
            .Add(affordable.Name, affordable)
            .Add(approval.Name, approval)
            .Build();
    }

    public static SpecificationRegistry<LoanApplication> CreateGeneratedRegistry()
        => GeneratedLoanApprovalSpecifications.Create();

    public static LoanDecision Evaluate(LoanApplication application, SpecificationRegistry<LoanApplication> registry)
    {
        var failed = registry.Names
            .Where(name => name != "approval-ready" && !registry.IsSatisfiedBy(name, application))
            .OrderBy(static name => name)
            .ToArray();

        return new LoanDecision(
            application.ApplicationId,
            registry.IsSatisfiedBy("approval-ready", application),
            failed);
    }

    public static IServiceCollection AddLoanApprovalSpecifications(this IServiceCollection services)
    {
        services.AddSingleton(static _ => CreateGeneratedRegistry());
        services.AddSingleton<LoanApprovalService>();
        return services;
    }

    public static LoanApplication CreatePrimeApplication()
        => new("APP-100", 180_000m, 50_000m, 742, 36, false, true);

    public static LoanApplication CreateHeldApplication()
        => new("APP-200", 180_000m, 50_000m, 742, 36, true, true);
}

public sealed class LoanApprovalService(SpecificationRegistry<LoanApprovalSpecificationDemo.LoanApplication> registry)
{
    public LoanApprovalSpecificationDemo.LoanDecision Evaluate(LoanApprovalSpecificationDemo.LoanApplication application)
        => LoanApprovalSpecificationDemo.Evaluate(application, registry);
}

[GenerateSpecificationRegistry(typeof(LoanApprovalSpecificationDemo.LoanApplication))]
public static partial class GeneratedLoanApprovalSpecifications
{
    [SpecificationRule("affordable")]
    private static bool Affordable(LoanApprovalSpecificationDemo.LoanApplication application)
        => application.AnnualIncome > 0m && application.RequestedAmount <= application.AnnualIncome * 0.35m;

    [SpecificationRule("approval-ready")]
    private static bool ApprovalReady(LoanApprovalSpecificationDemo.LoanApplication application)
        => VerifiedIdentity(application)
            && ClearFraud(application)
            && PrimeCredit(application)
            && StableIncome(application)
            && Affordable(application);

    [SpecificationRule("clear-fraud")]
    private static bool ClearFraud(LoanApprovalSpecificationDemo.LoanApplication application)
        => !application.HasFraudHold;

    [SpecificationRule("prime-credit")]
    private static bool PrimeCredit(LoanApprovalSpecificationDemo.LoanApplication application)
        => application.CreditScore >= 700;

    [SpecificationRule("stable-income")]
    private static bool StableIncome(LoanApprovalSpecificationDemo.LoanApplication application)
        => application.MonthsEmployed >= 12;

    [SpecificationRule("verified-identity")]
    private static bool VerifiedIdentity(LoanApprovalSpecificationDemo.LoanApplication application)
        => application.HasVerifiedIdentity;
}
