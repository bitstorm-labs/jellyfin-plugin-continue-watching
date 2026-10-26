# Continue Watching for Jellyfin

Jellyfin serves two lists: **Resume** (part-watched items) and **Next Up** (each
show's next episode). Plex and Emby merge these into one Continue Watching row on
the server. This plugin does the same, so every client shows the same row and none
has to rebuild it.

## Requirements and install

- **Jellyfin 12** (built against `Jellyfin.Controller` 12.1.0, net10.0 only).
- Current version: **0.2.0**.

### Install from the plugin repository

1. In Jellyfin, go to Dashboard, Plugins, Repositories, and add this repository URL:

   ```
   https://raw.githubusercontent.com/bitstorm-labs/jellyfin-plugin-continue-watching/main/manifest.json
   ```
2. Open Dashboard, Plugins, Catalog, install **Continue Watching**, and restart Jellyfin.

`raw.githubusercontent.com` caches for a few minutes, so a repository added right
after a release can return a stale manifest. Wait a minute and retry.

To install by hand instead, see [Build and install](#build-and-install).

## Works with Sashimi

With the plugin installed, Sashimi shows the server's row as Continue Watching.
Without it, the app asks for `/ContinueWatching`, gets a 404, and builds a similar
row itself from Resume and Next Up.

| Platform | Uses the plugin | Falls back to its own merge |
|---|---|---|
| Apple TV, iPhone, iPad | yes | yes |
| Android | yes | yes |
| Roku | yes | yes |

The point of the plugin is that every Sashimi device shows the same row in the same order.

## Endpoints

`GET /ContinueWatching?userId=&limit=20&fields=&enableImageTypes=&imageTypeLimit=`

The response has the same shape as `/Users/{userId}/Items/Resume`
(`{ Items, TotalRecordCount }`). The caller must be signed in. A non-admin can only
ask for their own row. A client that gets 404 knows the plugin isn't installed.
The plugin has no settings.

### Remove from Continue Watching

| Request | Result |
|---|---|
| `POST /ContinueWatching/Hidden/{episodeId}` | Hides that episode's show from the row. 204; 400 if the item isn't an episode; 404 if it's unknown. |
| `DELETE /ContinueWatching/Hidden/{episodeId}` | Undo. 204, even if nothing was hidden. |
| `GET /ContinueWatching/Hidden` | The caller's hidden shows: `[{ SeriesId, EpisodeId, HiddenAtUtc }]`. |

All three take `userId` with the same rule as the row. A show stays hidden while its
Next Up episode is still the one that was hidden. It comes back on its own when Next
Up moves to another episode, when the hidden episode is marked played or deleted, or
when any episode of the show is played after the hide. The row drops those stale
entries as it builds. Hides are kept per user in
`<jellyfin-config>/plugins/configurations/ContinueWatching/hidden.json` (at most 500
per user, oldest dropped first), so they survive restarts and upgrades. Clearing a
movie's resume position is the client's job (`POST /UserItems/{id}/UserData`); the
plugin only hides shows.

## Ranking

1. Items already marked played are dropped, even though Jellyfin keeps a resume
   position on some finished episodes.
2. A movie or standalone video ranks by when it was last played.
3. Each show gets one entry. That's its part-watched episode if it was touched at or
   after the show's last finished episode, otherwise its Next Up episode.
4. The entry ranks by the show's **last activity**: the newest play of any of its
   episodes. The chosen episode's own date doesn't count, because a next episode
   opened weeks ago keeps that stale date while you binge the season before it.
5. Newest first. On a tie the part-watched item leads. Shows with no watch history
   go last, in Next Up's order.

6. Shows hidden with `POST /ContinueWatching/Hidden/{episodeId}` are left out.

Air date plays no part.

## Build and install

The plugin needs the .NET 10 SDK (Jellyfin.Controller 12.1.0 is net10.0 only). Core
and its tests build on .NET 9.

```bash
dotnet test
dotnet build src/Jellyfin.Plugin.ContinueWatching -c Release
```

To install, stop Jellyfin, then copy `Jellyfin.Plugin.ContinueWatching.dll`,
`Jellyfin.Plugin.ContinueWatching.Core.dll` (both from
`src/Jellyfin.Plugin.ContinueWatching/bin/Release/net10.0/`) and a `meta.json` into
`<jellyfin-config>/plugins/ContinueWatching_<version>/`, and start Jellyfin again. Copying over a
running server's DLL makes it look like Jellyfin crashed.

## Credits

Built on [Jellyfin](https://jellyfin.org/) (the Jellyfin project, GPL-2.0) and its
`Jellyfin.Controller` plugin API. Sashimi is a separate Bitstorm Labs client for
Jellyfin. Jellyfin is a separate project under its own license, and this plugin is
not affiliated with or endorsed by it.
