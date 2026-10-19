# CLAUDE.md

The Jellyfin **Continue Watching** plugin. It serves one server-ranked Continue
Watching row at `GET /ContinueWatching` for the Sashimi apps (Apple, Android, Roku),
which fall back to their own merge on a 404. The design is in
`~/Documents/git/plans/sashimi/2026-10-12-continue-watching-plugin.md`.

## Layout

- `src/Jellyfin.Plugin.ContinueWatching.Core`: the ranking (`ContinueWatchingRanker`) and
  the request rules (`RowRequest`: who may read whose row, the limit, list parsing).
  It doesn't reference Jellyfin, targets net9.0, and every rule here has a test.
- `src/Jellyfin.Plugin.ContinueWatching`: a thin shell (net10.0, Jellyfin.Controller
  12.1.0) that reads Resume, Next Up and history from the server, passes them to Core,
  and returns DTOs. Keep logic out of it. If something here needs a test, move it into
  Core.

## Commands

```bash
dotnet test                                                    # Core tests (.NET 9 or 10)
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"  # .NET 10 SDK lives here
dotnet build src/Jellyfin.Plugin.ContinueWatching -c Release
```

## Deploy (Docker Jellyfin)

1. Check `/Sessions` for active playback.
2. Run `docker stop Jellyfin`.
3. Copy both DLLs into
   `<jellyfin-config>/plugins/ContinueWatching_<version>/`, and update
   `version` and `changelog` in that folder's `meta.json`.
4. Run `docker start Jellyfin`.

Copying a DLL over a running server makes it look like Jellyfin crashed. Bump
`AssemblyVersion`/`FileVersion` in both csproj files. After deploying, check the live
row and the 403 for a non-admin asking for another user's row.

## Fix what you find

If you come across a problem while working (a missing test, a red CI job, a bug, dead
code), fix it as part of the work. Don't just list it. Only hand something back when it
truly needs the user: credentials, a paid account, a product decision, or hardware.
