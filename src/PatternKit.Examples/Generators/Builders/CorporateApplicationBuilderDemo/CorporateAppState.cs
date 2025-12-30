using Microsoft.Extensions.Hosting;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public readonly record struct CorporateAppState(
    HostApplicationBuilder Builder,
    List<IAppModule> Modules,
    List<Action<IHostApplicationBuilder>> Customizations,
    List<Func<IServiceProvider, ValueTask>> StartupTasks,
    List<string> Log);