using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.LazyLoading;
using PatternKit.Generators.LazyLoading;

namespace PatternKit.Examples.LazyLoadDemo;

public sealed record CustomerProfile(Guid CustomerId, string Name, string Tier);

public interface ICustomerProfileStore
{
    ValueTask<CustomerProfile> LoadAsync(Guid customerId, CancellationToken cancellationToken = default);
}

public sealed class InMemoryCustomerProfileStore(CustomerProfile profile) : ICustomerProfileStore
{
    public ValueTask<CustomerProfile> LoadAsync(Guid customerId, CancellationToken cancellationToken = default)
        => new(profile with { CustomerId = customerId });
}

public sealed class CustomerProfileLazyLoadService(LazyLoad<CustomerProfile> profile)
{
    public async ValueTask<string> GetTierAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await profile.GetAsync(cancellationToken).ConfigureAwait(false);
        return loaded.Value.Tier;
    }

    public void Refresh() => profile.Invalidate();
}

public static class CustomerProfileLazyLoadPolicies
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static LazyLoad<CustomerProfile> CreateFluent(ICustomerProfileStore store)
        => LazyLoad<CustomerProfile>.Create("customer-profile")
            .LoadWith(ct => store.LoadAsync(CustomerId, ct))
            .WithTimeToLive(TimeSpan.FromMinutes(5))
            .Build();
}

[GenerateLazyLoad(
    typeof(CustomerProfile),
    FactoryMethodName = nameof(CreateGenerated),
    LoaderMethodName = nameof(LoadGeneratedAsync),
    LazyLoadName = "customer-profile",
    TimeToLiveMilliseconds = 300000)]
public static partial class GeneratedCustomerProfileLazyLoad
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static ICustomerProfileStore? _store;

    public static void UseStore(ICustomerProfileStore store) => _store = store;

    public static ValueTask<CustomerProfile> LoadGeneratedAsync(CancellationToken cancellationToken)
        => (_store ?? throw new InvalidOperationException("Customer profile store is not configured."))
            .LoadAsync(CustomerId, cancellationToken);
}

public static class CustomerProfileLazyLoadServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerProfileLazyLoadDemo(this IServiceCollection services)
    {
        services.AddSingleton<ICustomerProfileStore>(_ => new InMemoryCustomerProfileStore(new CustomerProfile(Guid.Empty, "Ada Lovelace", "Gold")));
        services.AddSingleton(provider => CustomerProfileLazyLoadPolicies.CreateFluent(provider.GetRequiredService<ICustomerProfileStore>()));
        services.AddSingleton<CustomerProfileLazyLoadService>();
        return services;
    }
}
