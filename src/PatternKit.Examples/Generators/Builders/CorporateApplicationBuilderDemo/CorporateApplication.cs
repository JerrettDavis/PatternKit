using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatternKit.Generators.Builders;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

[GenerateBuilder(
    Model = BuilderModel.StateProjection,
    GenerateBuilderMethods = true)]
public static partial class CorporateApplication
{
    public static CorporateAppState Seed()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddOptions();

        return new CorporateAppState(builder, [], [], [], []);
    }

    [BuilderProjector]
    public static CorporateApp Build(CorporateAppState state)
    {
        foreach (var module in state.Modules)
        {
            module.Configure(state.Builder, state.Log);
        }

        foreach (var customize in state.Customizations)
        {
            customize(state.Builder);
        }

        var host = state.Builder.Build();
        return new CorporateApp(host, state.StartupTasks, state.Log);
    }
}
