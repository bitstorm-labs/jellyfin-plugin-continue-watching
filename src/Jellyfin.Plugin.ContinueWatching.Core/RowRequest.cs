namespace Jellyfin.Plugin.ContinueWatching.Core;

/// <summary>The request-side rules of <c>GET /ContinueWatching</c>.</summary>
public static class RowRequest
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    /// <summary>
    /// Whose row to build: the requested user, or the caller when none is given.
    /// Null means the caller may not see it: only an admin can read someone else's row.
    /// </summary>
    public static Guid? TargetUser(Guid caller, Guid? requested, bool callerIsAdmin)
    {
        var target = requested is { } id && id != Guid.Empty ? id : caller;
        if (target == Guid.Empty)
        {
            return null;
        }

        return target == caller || callerIsAdmin ? target : null;
    }

    public static int ClampLimit(int limit) => Math.Clamp(limit, 1, MaxLimit);

    /// <summary>
    /// A comma-separated list of enum names, as Jellyfin's item endpoints take
    /// <c>fields</c> and <c>enableImageTypes</c>. Case-insensitive; unknown names and
    /// numbers are ignored rather than failing the whole row.
    /// </summary>
    public static T[] ParseList<T>(string? csv)
        where T : struct, Enum =>
        (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !s.All(char.IsAsciiDigit))
            .Select(s => Enum.TryParse<T>(s, true, out var v) && Enum.IsDefined(v) ? (T?)v : null)
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .Distinct()
            .ToArray();
}
