using Microsoft.Extensions.Hosting;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;


public sealed class CorporateApp(
    IHost host, 
    IReadOnlyList<Func<IServiceProvider, ValueTask>> startupTasks, 
    IReadOnlyList<string> log)
{
    public IReadOnlyList<string> Log => log;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var task in startupTasks)
        {
            await task(host.Services).ConfigureAwait(false);
        }
    }

    public IHost Host => host;
}
