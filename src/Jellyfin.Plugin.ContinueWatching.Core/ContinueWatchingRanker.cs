namespace Jellyfin.Plugin.ContinueWatching.Core;

/// <summary>
/// Builds the Continue Watching row from Jellyfin's two lists — Resume (part-watched
/// items) and Next Up (each series' next episode) — the way Plex and Emby do on their
/// servers, so every client shows the same row.
/// </summary>
/// <remarks>
/// Rules:
/// <list type="number">
/// <item>Anything already marked played is dropped: Jellyfin keeps a position on some
/// finished episodes, and they would otherwise resurface as "resume".</item>
/// <item>A movie or standalone video ranks by when it was last played.</item>
/// <item>A series gets one entry: its part-watched episode if that was touched at or
/// after the series' last finished episode, otherwise its Next Up episode.</item>
/// <item>That entry ranks by the series' last activity, never by the chosen
/// episode's own date — a next episode opened weeks ago keeps that stale date while
/// the season before it is binged.</item>
/// <item>Newest first; on a tie part-watched leads. A series with no dated activity
/// at all goes last, in Next Up's order.</item>
/// </list>
/// Air date plays no part.
/// </remarks>
public static class ContinueWatchingRanker
{
    public static IReadOnlyList<Guid> Rank(
        IReadOnlyList<RowCandidate> resume,
        IReadOnlyList<RowCandidate> nextUp,
        IReadOnlyDictionary<Guid, SeriesHistory> series,
        int limit)
    {
        var entries = new List<Entry>();
        var resumable = resume.Where(c => !c.Played).ToList();

        // Next Up's order first, so undated series keep the server's order; then any
        // series that only has a part-watched episode.
        var seriesOrder = nextUp.Concat(resumable)
            .Where(c => c.SeriesId is not null)
            .Select(c => c.SeriesId!.Value)
            .Distinct()
            .ToList();

        for (var position = 0; position < seriesOrder.Count; position++)
        {
            var seriesId = seriesOrder[position];
            series.TryGetValue(seriesId, out var history);

            var partWatched = resumable
                .Where(c => c.SeriesId == seriesId)
                .OrderByDescending(c => c.LastPlayed ?? DateTime.MinValue)
                .FirstOrDefault();
            var next = nextUp.FirstOrDefault(c => c.SeriesId == seriesId && !c.Played);

            var resumeIt = partWatched is not null
                && (history?.LastFinished is null || partWatched.LastPlayed >= history.LastFinished);
            var chosen = resumeIt ? partWatched : next;
            if (chosen is null)
            {
                continue;
            }

            DateTime? activity = new[] { history?.LastActivity, chosen.LastPlayed, partWatched?.LastPlayed }
                .Where(d => d is not null)
                .Max();
            entries.Add(new Entry(chosen.Id, activity, resumeIt, position));
        }

        var standalone = resumable.Where(c => c.SeriesId is null).ToList();
        for (var i = 0; i < standalone.Count; i++)
        {
            entries.Add(new Entry(standalone[i].Id, standalone[i].LastPlayed, true, seriesOrder.Count + i));
        }

        return entries
            .OrderBy(e => e.Activity is null)
            .ThenByDescending(e => e.Activity)
            .ThenByDescending(e => e.IsResume)
            .ThenBy(e => e.Position)
            .Select(e => e.Id)
            .Distinct()
            .Take(Math.Max(0, limit))
            .ToList();
    }

    private sealed record Entry(Guid Id, DateTime? Activity, bool IsResume, int Position);
}
