using Jellyfin.Plugin.ContinueWatching.Core;

namespace Jellyfin.Plugin.ContinueWatching.Core.Tests;

public sealed class HiddenStoreTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Ann = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cw-hidden-" + Guid.NewGuid());

    private string FilePath => Path.Combine(_dir, "nested", "hidden.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void HidesAreKeptPerUser()
    {
        var store = new HiddenStore(FilePath);
        var series = Guid.NewGuid();
        var episode = Guid.NewGuid();

        store.Hide(Ann, series, episode, Now);

        Assert.Equal([new HiddenEntry(series, episode, Now)], store.List(Ann));
        Assert.Empty(store.List(Bob));
    }

    [Fact]
    public void HidesSurviveARestart()
    {
        var series = Guid.NewGuid();
        var episode = Guid.NewGuid();
        new HiddenStore(FilePath).Hide(Ann, series, episode, Now);

        var reloaded = new HiddenStore(FilePath).List(Ann);

        Assert.Equal([new HiddenEntry(series, episode, Now)], reloaded);
    }

    [Fact]
    public void HidingASeriesAgainReplacesTheEarlierEpisode()
    {
        var store = new HiddenStore(FilePath);
        var series = Guid.NewGuid();
        var later = Guid.NewGuid();
        store.Hide(Ann, series, Guid.NewGuid(), Now);

        store.Hide(Ann, series, later, Now.AddMinutes(5));

        Assert.Equal(later, Assert.Single(store.List(Ann)).EpisodeId);
    }

    [Fact]
    public void UnhideByEpisodeOrSeriesIsIdempotent()
    {
        var store = new HiddenStore(FilePath);
        var seriesA = Guid.NewGuid();
        var episodeA = Guid.NewGuid();
        var seriesB = Guid.NewGuid();
        store.Hide(Ann, seriesA, episodeA, Now);
        store.Hide(Ann, seriesB, Guid.NewGuid(), Now);

        store.Unhide(Ann, episodeA);
        store.Unhide(Ann, episodeA);
        store.Unhide(Ann, Guid.NewGuid(), seriesB);
        store.Unhide(Bob, Guid.NewGuid());

        Assert.Empty(store.List(Ann));
        Assert.Empty(new HiddenStore(FilePath).List(Ann));
    }

    [Fact]
    public void EachUserKeepsOnlyTheNewestFiveHundred()
    {
        var store = new HiddenStore(FilePath);
        var first = Guid.NewGuid();
        store.Hide(Ann, first, Guid.NewGuid(), Now);
        for (var i = 1; i <= HiddenStore.MaxPerUser; i++)
        {
            store.Hide(Ann, Guid.NewGuid(), Guid.NewGuid(), Now.AddSeconds(i));
        }

        var entries = store.List(Ann);

        Assert.Equal(HiddenStore.MaxPerUser, entries.Count);
        Assert.DoesNotContain(entries, e => e.SeriesId == first);
    }

    [Fact]
    public void RemoveDropsOnlyTheStaleEntries()
    {
        var store = new HiddenStore(FilePath);
        store.Hide(Ann, Guid.NewGuid(), Guid.NewGuid(), Now);
        store.Hide(Ann, Guid.NewGuid(), Guid.NewGuid(), Now);
        var stale = store.List(Ann)[0];

        store.Remove(Ann, [stale]);

        Assert.DoesNotContain(stale, new HiddenStore(FilePath).List(Ann));
        Assert.Single(store.List(Ann));
    }

    [Fact]
    public void ADamagedFileStartsEmptyAndIsKeptAside()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ not json");

        var store = new HiddenStore(FilePath);

        Assert.Empty(store.List(Ann));
        Assert.True(File.Exists(FilePath + ".corrupt"));
    }

    [Fact]
    public void ConcurrentHidesAreAllKept()
    {
        var store = new HiddenStore(FilePath);

        Parallel.For(0, 50, i => store.Hide(Ann, Guid.NewGuid(), Guid.NewGuid(), Now.AddSeconds(i)));

        Assert.Equal(50, store.List(Ann).Count);
        Assert.Equal(50, new HiddenStore(FilePath).List(Ann).Count);
    }
}
