using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.ContinueWatching.Core;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.ContinueWatching;

/// <summary>Gathers a user's Resume, Next Up and watch history and ranks the row.</summary>
public class ContinueWatchingService
{
    // How far back the watch history reaches. A series whose newest activity is
    // older than the newest this-many episodes still ranks, by its own items' dates.
    private const int HistoryDepth = 500;
    private const int CandidateDepth = 50;

    private readonly ILibraryManager _libraryManager;
    private readonly ITVSeriesManager _tvSeriesManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IDtoService _dtoService;

    public ContinueWatchingService(
        ILibraryManager libraryManager,
        ITVSeriesManager tvSeriesManager,
        IUserDataManager userDataManager,
        IDtoService dtoService)
    {
        _libraryManager = libraryManager;
        _tvSeriesManager = tvSeriesManager;
        _userDataManager = userDataManager;
        _dtoService = dtoService;
    }

    public async Task<IReadOnlyList<BaseItemDto>> GetRowAsync(User user, int limit, DtoOptions dtoOptions)
    {
        var lean = new DtoOptions(false);

        // Three independent reads; run together they cost the slowest one.
        var resumeTask = Task.Run(() => _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            OrderBy = [(ItemSortBy.DatePlayed, SortOrder.Descending)],
            IsResumable = true,
            Recursive = true,
            MediaTypes = [MediaType.Video],
            IsVirtualItem = false,
            CollapseBoxSetItems = false,
            Limit = CandidateDepth,
            DtoOptions = lean
        }));

        var nextUpTask = Task.Run(() => _tvSeriesManager.GetNextUp(
            new NextUpQuery
            {
                User = user,
                Limit = CandidateDepth,
                EnableRewatching = false,
                EnableResumable = true,
                NextUpDateCutoff = DateTime.MinValue
            },
            lean).Items);

        var historyTask = Task.Run(() => _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            OrderBy = [(ItemSortBy.DatePlayed, SortOrder.Descending)],
            IncludeItemTypes = [BaseItemKind.Episode],
            Recursive = true,
            IsVirtualItem = false,
            Limit = HistoryDepth,
            DtoOptions = lean
        }));

        await Task.WhenAll(resumeTask, nextUpTask, historyTask).ConfigureAwait(false);
        var resume = resumeTask.Result;
        var nextUp = nextUpTask.Result;
        var history = historyTask.Result;

        var all = resume.Concat(nextUp).Concat(history).DistinctBy(i => i.Id).ToList();
        var userData = _userDataManager.GetUserDataBatch(all, user);
        UserItemData? DataOf(BaseItem item) => userData.TryGetValue(item.Id, out var d) ? d : null;

        RowCandidate ToCandidate(BaseItem item)
        {
            var data = DataOf(item);
            return new RowCandidate(item.Id, SeriesIdOf(item), data?.LastPlayedDate, data?.Played ?? false);
        }

        var ranked = ContinueWatchingRanker.Rank(
            resume.Select(ToCandidate).ToList(),
            nextUp.Select(ToCandidate).ToList(),
            SeriesHistories(history, DataOf),
            limit);

        var byId = resume.Concat(nextUp).DistinctBy(i => i.Id).ToDictionary(i => i.Id);
        var items = ranked.Select(id => byId[id]).ToList();
        return _dtoService.GetBaseItemDtos(items, dtoOptions, user);
    }

    private static Dictionary<Guid, SeriesHistory> SeriesHistories(
        IReadOnlyList<BaseItem> episodes, Func<BaseItem, UserItemData?> dataOf)
    {
        var result = new Dictionary<Guid, SeriesHistory>();
        foreach (var episode in episodes)
        {
            var seriesId = SeriesIdOf(episode);
            var data = dataOf(episode);
            if (seriesId is null || data?.LastPlayedDate is not { } played)
            {
                continue;
            }

            result.TryGetValue(seriesId.Value, out var known);
            var activity = Max(known?.LastActivity, played);
            var finished = data.Played ? Max(known?.LastFinished, played) : known?.LastFinished;
            result[seriesId.Value] = new SeriesHistory(activity, finished);
        }

        return result;
    }

    private static Guid? SeriesIdOf(BaseItem item) =>
        item is MediaBrowser.Controller.Entities.TV.Episode { SeriesId: var id } && id != Guid.Empty ? id : null;

    private static DateTime Max(DateTime? a, DateTime b) => a is { } x && x > b ? x : b;
}
