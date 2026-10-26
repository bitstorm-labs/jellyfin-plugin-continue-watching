using Jellyfin.Plugin.ContinueWatching.Core;

namespace Jellyfin.Plugin.ContinueWatching.Core.Tests;

public class HideRuleTests
{
    private static readonly DateTime HiddenAt = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Show = Guid.NewGuid();
    private static readonly Guid E5 = Guid.NewGuid();
    private static readonly Guid E6 = Guid.NewGuid();
    private static readonly HashSet<Guid> NoneFinished = [];
    private static readonly Dictionary<Guid, SeriesHistory> NoHistory = [];

    private static HiddenEntry Hidden(Guid episode) => new(Show, episode, HiddenAt);

    private static RowCandidate Next(Guid episode, Guid? series = null) =>
        new(episode, series ?? Show, null);

    [Fact]
    public void ASeriesStaysHiddenWhileNextUpIsStillTheStoredEpisode()
    {
        var outcome = HideRule.Apply([Hidden(E5)], [Next(E5)], NoHistory, NoneFinished);

        Assert.Contains(Show, outcome.HiddenSeries);
        Assert.Empty(outcome.Stale);
    }

    [Fact]
    public void OnceNextUpMovesToAnotherEpisodeTheHideIsStale()
    {
        var outcome = HideRule.Apply([Hidden(E5)], [Next(E6)], NoHistory, NoneFinished);

        Assert.Empty(outcome.HiddenSeries);
        Assert.Equal([Hidden(E5)], outcome.Stale);
    }

    [Fact]
    public void AFinishedOrDeletedStoredEpisodeMakesTheHideStaleEvenWithNoNextUp()
    {
        var outcome = HideRule.Apply([Hidden(E5)], [], NoHistory, new HashSet<Guid> { E5 });

        Assert.Empty(outcome.HiddenSeries);
        Assert.Single(outcome.Stale);
    }

    [Fact]
    public void ASeriesMissingFromNextUpForNoKnownReasonStaysHidden()
    {
        var outcome = HideRule.Apply([Hidden(E5)], [], NoHistory, NoneFinished);

        Assert.Contains(Show, outcome.HiddenSeries);
    }

    [Fact]
    public void PlayingTheSeriesAfterTheHideBringsItBack()
    {
        var history = new Dictionary<Guid, SeriesHistory>
        {
            [Show] = new(HiddenAt.AddHours(1), null)
        };

        var outcome = HideRule.Apply([Hidden(E5)], [Next(E5)], history, NoneFinished);

        Assert.Empty(outcome.HiddenSeries);
        Assert.Single(outcome.Stale);
    }

    [Fact]
    public void ActivityFromBeforeTheHideOrWithinTheGraceKeepsItHidden()
    {
        var history = new Dictionary<Guid, SeriesHistory>
        {
            [Show] = new(HiddenAt + HideRule.PlaybackGrace, null)
        };

        var outcome = HideRule.Apply([Hidden(E5)], [Next(E5)], history, NoneFinished);

        Assert.Contains(Show, outcome.HiddenSeries);
    }

    [Fact]
    public void APlayedNextUpEntryDoesNotCountAsTheSeriesMovingOn()
    {
        var outcome = HideRule.Apply(
            [Hidden(E5)], [new RowCandidate(E6, Show, null, Played: true), Next(E5)], NoHistory, NoneFinished);

        Assert.Contains(Show, outcome.HiddenSeries);
    }

    [Fact]
    public void HiddenSeriesLeaveResumeAndNextUpButOtherItemsStay()
    {
        var other = Guid.NewGuid();
        var movie = new RowCandidate(Guid.NewGuid(), null, HiddenAt);
        var candidates = new[] { Next(E5), Next(Guid.NewGuid(), other), movie };

        var left = HideRule.Without(candidates, new HashSet<Guid> { Show });

        Assert.Equal(2, left.Count);
        Assert.DoesNotContain(left, c => c.SeriesId == Show);
        Assert.Contains(movie, left);
    }

    [Fact]
    public void TheRankerNeverShowsAHiddenSeries()
    {
        var other = Guid.NewGuid();
        var otherNext = Guid.NewGuid();
        var resume = new[] { new RowCandidate(E5, Show, HiddenAt.AddDays(-1)) };
        var nextUp = new[] { Next(E5), Next(otherNext, other) };
        var outcome = HideRule.Apply([Hidden(E5)], nextUp, NoHistory, NoneFinished);

        var row = ContinueWatchingRanker.Rank(
            HideRule.Without(resume, outcome.HiddenSeries),
            HideRule.Without(nextUp, outcome.HiddenSeries),
            NoHistory,
            20);

        Assert.Equal([otherNext], row);
    }
}
