using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.Repository;
using PatternKit.Application.ServiceLayer;
using PatternKit.Generators.ServiceLayer;

namespace PatternKit.Examples.ServiceLayerDemo;

public static class CustomerServiceLayerDemo
{
    public static async ValueTask<CustomerServiceLayerSummary> RunFluentAsync()
    {
        var repository = InMemoryRepository<RegisteredCustomer, string>.Create(static customer => customer.CustomerId).Build();
        var operation = CustomerServiceLayerPolicies.CreateFluentOperation(repository);
        var result = await operation.ExecuteAsync(new RegisterCustomerRequest("customer-100", "buyer@example.com", "retail"));
        return new(result.Succeeded, result.Response?.CustomerId ?? "", (await repository.ListAsync()).Count);
    }

    public static async ValueTask<CustomerServiceLayerSummary> RunGeneratedAsync()
    {
        GeneratedCustomerServiceLayer.Repository = InMemoryRepository<RegisteredCustomer, string>.Create(static customer => customer.CustomerId).Build();
        var result = await GeneratedCustomerServiceLayer.CreateOperation().ExecuteAsync(new RegisterCustomerRequest("customer-200", "buyer2@example.com", "enterprise"));
        return new(result.Succeeded, result.Response?.CustomerId ?? "", (await GeneratedCustomerServiceLayer.Repository.ListAsync()).Count);
    }
}

public sealed record RegisterCustomerRequest(string CustomerId, string Email, string Segment);

public sealed record CustomerRegistrationReceipt(string CustomerId, string Email);

public sealed record RegisteredCustomer(string CustomerId, string Email, string Segment);

public sealed record CustomerServiceLayerSummary(bool Registered, string CustomerId, int RepositoryCount);

public static class CustomerServiceLayerPolicies
{
    public static ServiceLayerOperation<RegisterCustomerRequest, CustomerRegistrationReceipt> CreateFluentOperation(IRepository<RegisteredCustomer, string> repository)
    {
        if (repository is null)
            throw new ArgumentNullException(nameof(repository));

        return ServiceLayerOperation<RegisterCustomerRequest, CustomerRegistrationReceipt>.Create("register-customer")
            .Require("customer-id", "Customer id is required.", static request => !string.IsNullOrWhiteSpace(request.CustomerId))
            .Require("email", "Email is required.", static request => !string.IsNullOrWhiteSpace(request.Email))
            .Require("segment", "Customer segment is required.", static request => !string.IsNullOrWhiteSpace(request.Segment))
            .Handle(async (request, cancellationToken) =>
            {
                var result = await repository.AddAsync(new RegisteredCustomer(request.CustomerId, request.Email, request.Segment), cancellationToken).ConfigureAwait(false);
                if (!result.Succeeded)
                    throw new InvalidOperationException(result.Reason);

                return new CustomerRegistrationReceipt(request.CustomerId, request.Email);
            })
            .Build();
    }
}

public sealed class CustomerServiceLayerWorkflow
{
    private readonly IServiceOperation<RegisterCustomerRequest, CustomerRegistrationReceipt> _operation;

    public CustomerServiceLayerWorkflow(IServiceOperation<RegisterCustomerRequest, CustomerRegistrationReceipt> operation)
    {
        _operation = operation;
    }

    public async ValueTask<CustomerServiceLayerSummary> RegisterAsync(RegisterCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _operation.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return new(result.Succeeded, result.Response?.CustomerId ?? "", result.Succeeded ? 1 : 0);
    }
}

public sealed record CustomerServiceLayerDemoRunner(
    Func<ValueTask<CustomerServiceLayerSummary>> RunFluentAsync,
    Func<ValueTask<CustomerServiceLayerSummary>> RunGeneratedAsync);

public static class CustomerServiceLayerServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerServiceLayerDemo(this IServiceCollection services)
    {
        services.AddScoped<IRepository<RegisteredCustomer, string>>(_ => InMemoryRepository<RegisteredCustomer, string>.Create(static customer => customer.CustomerId).Build());
        services.AddScoped<IServiceOperation<RegisterCustomerRequest, CustomerRegistrationReceipt>>(sp =>
            CustomerServiceLayerPolicies.CreateFluentOperation(sp.GetRequiredService<IRepository<RegisteredCustomer, string>>()));
        services.AddScoped<CustomerServiceLayerWorkflow>();
        services.AddSingleton(new CustomerServiceLayerDemoRunner(
            CustomerServiceLayerDemo.RunFluentAsync,
            CustomerServiceLayerDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateServiceLayerOperation(typeof(RegisterCustomerRequest), typeof(CustomerRegistrationReceipt), FactoryName = "CreateOperation", OperationName = "register-customer")]
public static partial class GeneratedCustomerServiceLayer
{
    public static IRepository<RegisteredCustomer, string> Repository { get; set; } =
        InMemoryRepository<RegisteredCustomer, string>.Create(static customer => customer.CustomerId).Build();

    [ServiceLayerRule("customer-id", "Customer id is required.", 10)]
    private static bool HasCustomerId(RegisterCustomerRequest request) => !string.IsNullOrWhiteSpace(request.CustomerId);

    [ServiceLayerRule("email", "Email is required.", 20)]
    private static bool HasEmail(RegisterCustomerRequest request) => !string.IsNullOrWhiteSpace(request.Email);

    [ServiceLayerRule("segment", "Customer segment is required.", 30)]
    private static bool HasSegment(RegisterCustomerRequest request) => !string.IsNullOrWhiteSpace(request.Segment);

    [ServiceLayerHandler]
    private static async ValueTask<CustomerRegistrationReceipt> Handle(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await Repository.AddAsync(new RegisteredCustomer(request.CustomerId, request.Email, request.Segment), cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Reason);

        return new CustomerRegistrationReceipt(request.CustomerId, request.Email);
    }
}
