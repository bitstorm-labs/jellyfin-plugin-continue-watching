namespace Jellyfin.Plugin.ContinueWatching.Core;

/// <summary>
/// One thing that could go in the row: a part-watched item from Resume, or a
/// series' next episode from Next Up.
/// </summary>
/// <param name="Id">The item id.</param>
/// <param name="SeriesId">The episode's series; null for a movie or a standalone video.</param>
/// <param name="LastPlayed">When this item itself was last played, if ever.</param>
/// <param name="Played">Whether the server counts this item as finished.</param>
public sealed record RowCandidate(Guid Id, Guid? SeriesId, DateTime? LastPlayed, bool Played = false);

/// <summary>What the server knows about a series' watch history for one user.</summary>
/// <param name="LastActivity">Newest LastPlayedDate of any of its episodes.</param>
/// <param name="LastFinished">Newest LastPlayedDate of an episode marked played.</param>
public sealed record SeriesHistory(DateTime? LastActivity, DateTime? LastFinished);
