namespace Jellyfin.Plugin.ContinueWatching.Core;

/// <summary>A series a user removed from Continue Watching, at the episode it was showing.</summary>
/// <param name="SeriesId">The series kept out of the row.</param>
/// <param name="EpisodeId">The episode the row showed when it was removed.</param>
/// <param name="HiddenAtUtc">When it was removed.</param>
public sealed record HiddenEntry(Guid SeriesId, Guid EpisodeId, DateTime HiddenAtUtc);

/// <summary>Which hidden entries still hold, and which series they keep out of the row.</summary>
/// <param name="HiddenSeries">Series to leave out of the row.</param>
/// <param name="Kept">Entries that still hold.</param>
/// <param name="Stale">Entries that no longer hold and should be dropped from the store.</param>
public sealed record HideOutcome(
    IReadOnlySet<Guid> HiddenSeries,
    IReadOnlyList<HiddenEntry> Kept,
    IReadOnlyList<HiddenEntry> Stale);

/// <summary>
/// The "Remove from Continue Watching" rule, shared with the clients' local fallback.
/// </summary>
/// <remarks>
/// A series stays hidden while its Next Up episode is still the stored episode. The entry
/// goes stale, and the series shows again, as soon as any of these is true:
/// <list type="number">
/// <item>Next Up for the series is a different episode (watched elsewhere, or a resume
/// moved the user on).</item>
/// <item>The stored episode is finished, or no longer exists. This covers a series that
/// has dropped out of Next Up altogether.</item>
/// <item>An episode of the series was played after the hide (allowing
/// <see cref="PlaybackGrace"/> for the client's own resume-clear landing just after it):
/// the user went back to it, so it belongs in the row again.</item>
/// </list>
/// A series missing from Next Up for any other reason (past the candidate depth, say)
/// stays hidden.
/// </remarks>
public static class HideRule
{
    public static readonly TimeSpan PlaybackGrace = TimeSpan.FromMinutes(2);

    public static HideOutcome Apply(
        IReadOnlyList<HiddenEntry> hidden,
        IReadOnlyList<RowCandidate> nextUp,
        IReadOnlyDictionary<Guid, SeriesHistory> series,
        IReadOnlySet<Guid> finishedEpisodes)
    {
        var nextBySeries = new Dictionary<Guid, Guid>();
        foreach (var candidate in nextUp)
        {
            if (candidate.SeriesId is { } seriesId && !candidate.Played)
            {
                nextBySeries.TryAdd(seriesId, candidate.Id);
            }
        }

        var kept = new List<HiddenEntry>();
        var stale = new List<HiddenEntry>();
        foreach (var entry in hidden)
        {
            var movedOn = nextBySeries.TryGetValue(entry.SeriesId, out var next) && next != entry.EpisodeId;
            var finished = finishedEpisodes.Contains(entry.EpisodeId);
            var playedSince = series.TryGetValue(entry.SeriesId, out var history)
                && history.LastActivity is { } last
                && last > entry.HiddenAtUtc + PlaybackGrace;

            (movedOn || finished || playedSince ? stale : kept).Add(entry);
        }

        return new HideOutcome(kept.Select(e => e.SeriesId).ToHashSet(), kept, stale);
    }

    /// <summary>The candidates left once hidden series are taken out.</summary>
    public static IReadOnlyList<RowCandidate> Without(
        IReadOnlyList<RowCandidate> candidates, IReadOnlySet<Guid> hiddenSeries) =>
        candidates.Where(c => c.SeriesId is not { } id || !hiddenSeries.Contains(id)).ToList();
}
