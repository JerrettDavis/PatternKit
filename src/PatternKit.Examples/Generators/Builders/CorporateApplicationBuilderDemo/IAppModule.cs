using Microsoft.Extensions.Hosting;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public interface IAppModule
{
    void Configure(IHostApplicationBuilder builder, IList<string> log);
}