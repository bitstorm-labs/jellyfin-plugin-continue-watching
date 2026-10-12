using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ContinueWatching;

public class ContinueWatchingPlugin : BasePlugin<PluginConfiguration>
{
    public ContinueWatchingPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    public override string Name => "Continue Watching";

    public override Guid Id => Guid.Parse("a286cbd2-ce22-46ad-ad0f-d49491cce52a");

    public override string Description =>
        "One Continue Watching row, ranked on the server: part-watched items and each "
        + "show's next episode, ordered by when you last watched them.";
}
