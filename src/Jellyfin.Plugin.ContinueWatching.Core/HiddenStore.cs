using System.Text.Json;

namespace Jellyfin.Plugin.ContinueWatching.Core;

/// <summary>
/// Each user's hidden series, kept in one JSON file. Thread-safe; every change is
/// written straight away (to a temp file, then moved over), so it survives a restart.
/// </summary>
/// <remarks>One entry per series; a user keeps at most <see cref="MaxPerUser"/>, oldest
/// dropped first.</remarks>
public sealed class HiddenStore
{
    public const int MaxPerUser = 500;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<Guid, List<HiddenEntry>>? _byUser;

    public HiddenStore(string path)
    {
        _path = path;
    }

    public IReadOnlyList<HiddenEntry> List(Guid userId)
    {
        lock (_gate)
        {
            return Load().TryGetValue(userId, out var entries) ? entries.ToList() : [];
        }
    }

    /// <summary>Hides a series at the given episode, replacing any earlier hide of it.</summary>
    public void Hide(Guid userId, Guid seriesId, Guid episodeId, DateTime hiddenAtUtc)
    {
        lock (_gate)
        {
            var all = Load();
            if (!all.TryGetValue(userId, out var entries))
            {
                entries = [];
                all[userId] = entries;
            }

            entries.RemoveAll(e => e.SeriesId == seriesId);
            entries.Add(new HiddenEntry(seriesId, episodeId, hiddenAtUtc));
            if (entries.Count > MaxPerUser)
            {
                var keep = entries.OrderByDescending(e => e.HiddenAtUtc).Take(MaxPerUser).ToHashSet();
                entries.RemoveAll(e => !keep.Contains(e));
            }

            Save(all);
        }
    }

    /// <summary>
    /// Unhides whatever the id names: a stored episode, or a series. Unknown ids are a
    /// no-op, so undo can be sent twice.
    /// </summary>
    public void Unhide(Guid userId, Guid itemId, Guid? seriesId = null)
    {
        lock (_gate)
        {
            var all = Load();
            if (all.TryGetValue(userId, out var entries)
                && entries.RemoveAll(e => e.EpisodeId == itemId || e.SeriesId == itemId || e.SeriesId == seriesId) > 0)
            {
                Save(all);
            }
        }
    }

    /// <summary>Drops the given entries (the stale ones <see cref="HideRule"/> found).</summary>
    public void Remove(Guid userId, IReadOnlyCollection<HiddenEntry> stale)
    {
        if (stale.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            var all = Load();
            if (all.TryGetValue(userId, out var entries) && entries.RemoveAll(stale.Contains) > 0)
            {
                Save(all);
            }
        }
    }

    private Dictionary<Guid, List<HiddenEntry>> Load()
    {
        if (_byUser is not null)
        {
            return _byUser;
        }

        _byUser = [];
        if (File.Exists(_path))
        {
            try
            {
                using var stream = File.OpenRead(_path);
                var read = JsonSerializer.Deserialize<Dictionary<Guid, List<HiddenEntry>>>(stream, JsonOptions);
                if (read is not null)
                {
                    _byUser = read;
                }
            }
            catch (JsonException)
            {
                // A damaged file costs the hides, not the row. Keep it aside for a look.
                File.Copy(_path, _path + ".corrupt", overwrite: true);
            }
        }

        return _byUser;
    }

    private void Save(Dictionary<Guid, List<HiddenEntry>> all)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = _path + ".tmp";
        var live = all.Where(kv => kv.Value.Count > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        File.WriteAllText(temp, JsonSerializer.Serialize(live, JsonOptions));
        File.Move(temp, _path, overwrite: true);
    }
}
