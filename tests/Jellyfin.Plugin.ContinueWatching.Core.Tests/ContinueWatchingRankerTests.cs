using Jellyfin.Plugin.ContinueWatching.Core;

namespace Jellyfin.Plugin.ContinueWatching.Core.Tests;

public class ContinueWatchingRankerTests
{
    private static readonly Dictionary<string, Guid> Ids = new();

    private static Guid Id(string name)
    {
        if (!Ids.TryGetValue(name, out var id))
        {
            id = Guid.NewGuid();
            Ids[name] = id;
        }

        return id;
    }

    private static DateTime At(string iso) => DateTime.Parse(iso, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

    private static RowCandidate Ep(string id, string series, string? played, bool finished = false) =>
        new(Id(id), Id(series), played is null ? null : At(played), finished);

    private static RowCandidate Movie(string id, string played) => new(Id(id), null, At(played));

    private static SeriesHistory History(string? activity, string? finished) =>
        new(activity is null ? null : At(activity), finished is null ? null : At(finished));

    private static List<string> Names(IEnumerable<Guid> ids) =>
        ids.Select(id => Ids.First(kv => kv.Value == id).Key).ToList();

    /// <summary>
    /// The live row on 2026-10-12. The 100's next episode (S5E1) had been opened on
    /// Sept 22, so it carried that date while S4 was binged; ranked by its own date
    /// it fell behind every show touched since. A series ranks by its last activity.
    /// </summary>
    [Fact]
    public void ASeriesRanksByItsLastActivityNotItsNextEpisodesStaleDate()
    {
        var resume = new[]
        {
            Ep("gcn-vo2", "gcn", "2026-10-12T01:12:39Z"),
            Ep("exploring-offgrid", "exploring", "2026-10-10T02:03:13Z"),
            Ep("sabrina-e2", "sabrina", "2026-10-01T17:35:26Z"),
            Ep("the100-s1e13", "the100", "2026-09-22T01:32:59Z", finished: true),
        };
        var nextUp = new[]
        {
            Ep("the100-s5e1", "the100", "2026-09-22T02:11:50Z"),
            Ep("gcn-vo2", "gcn", "2026-10-12T01:12:39Z"),
            Ep("darkmatter-s2e5", "darkmatter", "2026-10-02T18:13:24Z"),
            Ep("sabrina-e2", "sabrina", "2026-10-01T17:35:26Z"),
        };
        var series = new Dictionary<Guid, SeriesHistory>
        {
            [Id("the100")] = History("2026-10-12T05:41:44Z", "2026-10-12T05:41:44Z"),
            [Id("gcn")] = History("2026-10-12T01:12:39Z", "2026-10-12T01:05:36Z"),
            [Id("exploring")] = History("2026-10-10T02:03:13Z", "2026-09-30T10:00:00Z"),
            [Id("darkmatter")] = History("2026-10-02T18:13:24Z", "2026-10-02T17:00:00Z"),
            [Id("sabrina")] = History("2026-10-01T17:35:26Z", "2026-09-30T12:00:00Z"),
        };

        var row = ContinueWatchingRanker.Rank(resume, nextUp, series, 20);

        Assert.Equal(
            ["the100-s5e1", "gcn-vo2", "exploring-offgrid", "darkmatter-s2e5", "sabrina-e2"],
            Names(row));
    }

    [Fact]
    public void APartWatchedEpisodeTouchedAfterTheLastFinishIsOfferedForResuming()
    {
        var resume = new[] { Ep("show-e5", "show", "2026-10-05T20:30:00Z") };
        var nextUp = new[] { Ep("show-e6", "show", null) };
        var series = new Dictionary<Guid, SeriesHistory>
        {
            [Id("show")] = History("2026-10-05T20:30:00Z", "2026-10-05T20:00:00Z"),
        };

        Assert.Equal(["show-e5"], Names(ContinueWatchingRanker.Rank(resume, nextUp, series, 20)));
    }

    [Fact]
    public void APartWatchedEpisodeOlderThanTheLastFinishGivesWayToNextUp()
    {
        // Stopped part-way through E2 last month, since then watched E3–E7 elsewhere.
        var resume = new[] { Ep("show-e2", "show", "2026-09-01T20:00:00Z") };
        var nextUp = new[] { Ep("show-e8", "show", null) };
        var series = new Dictionary<Guid, SeriesHistory>
        {
            [Id("show")] = History("2026-10-05T21:00:00Z", "2026-10-05T21:00:00Z"),
        };

        Assert.Equal(["show-e8"], Names(ContinueWatchingRanker.Rank(resume, nextUp, series, 20)));
    }

    [Fact]
    public void FinishedItemsThatStillCarryAPositionNeverAppear()
    {
        var resume = new[]
        {
            Ep("mash-s3e15", "mash", "2026-09-15T04:12:33Z", finished: true),
            new RowCandidate(Id("old-movie"), null, At("2026-09-10T00:00:00Z"), Played: true),
        };

        Assert.Empty(ContinueWatchingRanker.Rank(resume, [], new Dictionary<Guid, SeriesHistory>(), 20));
    }

    [Fact]
    public void AMovieRanksByWhenItWasLastPlayedAmongTheShows()
    {
        var resume = new[] { Movie("movie", "2026-10-08T12:00:00Z") };
        var nextUp = new[] { Ep("newer-e2", "newer", null), Ep("older-e2", "older", null) };
        var series = new Dictionary<Guid, SeriesHistory>
        {
            [Id("newer")] = History("2026-10-09T12:00:00Z", "2026-10-09T12:00:00Z"),
            [Id("older")] = History("2026-10-01T12:00:00Z", "2026-10-01T12:00:00Z"),
        };

        Assert.Equal(
            ["newer-e2", "movie", "older-e2"],
            Names(ContinueWatchingRanker.Rank(resume, nextUp, series, 20)));
    }

    [Fact]
    public void ASeriesWithNoHistoryAtAllGoesLastInNextUpOrder()
    {
        var nextUp = new[] { Ep("fresh-a", "fresh-a-show", null), Ep("dated", "dated-show", null), Ep("fresh-b", "fresh-b-show", null) };
        var series = new Dictionary<Guid, SeriesHistory>
        {
            [Id("dated-show")] = History("2026-10-01T12:00:00Z", "2026-10-01T12:00:00Z"),
        };

        Assert.Equal(
            ["dated", "fresh-a", "fresh-b"],
            Names(ContinueWatchingRanker.Rank([], nextUp, series, 20)));
    }

    [Fact]
    public void EachSeriesAppearsOnceAndTheLimitHolds()
    {
        var resume = new[] { Ep("a-e1", "a", "2026-10-03T00:00:00Z"), Ep("a-e4", "a", "2026-10-04T00:00:00Z") };
        var nextUp = new[] { Ep("a-e5", "a", null), Ep("b-e1", "b", null), Ep("c-e1", "c", null) };
        var series = new Dictionary<Guid, SeriesHistory>
        {
            [Id("a")] = History("2026-10-04T00:00:00Z", "2026-10-02T00:00:00Z"),
            [Id("b")] = History("2026-10-02T00:00:00Z", "2026-10-02T00:00:00Z"),
            [Id("c")] = History("2026-10-01T00:00:00Z", "2026-10-01T00:00:00Z"),
        };

        Assert.Equal(["a-e4", "b-e1"], Names(ContinueWatchingRanker.Rank(resume, nextUp, series, 2)));
    }
}
