using Jellyfin.Plugin.ContinueWatching.Core;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ContinueWatching;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ContinueWatchingService>();

        // Under plugins/configurations, not the versioned plugin folder, so hides
        // survive an upgrade.
        serviceCollection.AddSingleton(provider => new HiddenStore(Path.Combine(
            provider.GetRequiredService<IApplicationPaths>().PluginConfigurationsPath,
            "ContinueWatching",
            "hidden.json")));
    }
}
