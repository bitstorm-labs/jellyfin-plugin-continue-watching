# Continue Watching for Jellyfin

Jellyfin serves two lists: **Resume** (part-watched items) and **Next Up** (each
show's next episode). Plex and Emby merge these into one Continue Watching row on
the server. This plugin does the same, so every client shows the same row and none
has to rebuild it.

## Endpoint

`GET /ContinueWatching?userId=&limit=20&fields=&enableImageTypes=&imageTypeLimit=`

The response has the same shape as `/Users/{userId}/Items/Resume`
(`{ Items, TotalRecordCount }`). The caller must be signed in. A non-admin can only
ask for their own row. A client that gets 404 knows the plugin isn't installed.

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

Air date plays no part.

## Build and install

The plugin needs the .NET 10 SDK (Jellyfin.Controller 12.1.0 is net10.0 only). Core
and its tests build on .NET 9.

```bash
dotnet test
dotnet build src/Jellyfin.Plugin.ContinueWatching -c Release
```

To install, stop Jellyfin, then copy `Jellyfin.Plugin.ContinueWatching.dll`,
`Jellyfin.Plugin.ContinueWatching.Core.dll` and a `meta.json` into
`plugins/ContinueWatching_<version>/`, and start Jellyfin again. Copying over a
running server's DLL makes it look like Jellyfin crashed.
