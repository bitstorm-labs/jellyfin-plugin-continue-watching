using System.Net.Mime;
using System.Security.Claims;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ContinueWatching;

/// <summary>
/// The Continue Watching row. Same response shape as Jellyfin's
/// <c>/Users/{id}/Items/Resume</c>, so a client can decode it the same way.
/// </summary>
[ApiController]
[Authorize]
[Route("ContinueWatching")]
[Produces(MediaTypeNames.Application.Json)]
public class ContinueWatchingController : ControllerBase
{
    private const string UserIdClaim = "Jellyfin-UserId";

    private readonly ContinueWatchingService _service;
    private readonly IUserManager _userManager;

    public ContinueWatchingController(ContinueWatchingService service, IUserManager userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    /// <param name="userId">Whose row; defaults to the caller. Only an admin may ask for someone else's.</param>
    /// <param name="limit">How many items (1–100, default 20).</param>
    /// <param name="fields">Extra item fields, comma-separated, as on Jellyfin's item endpoints.</param>
    /// <param name="enableImageTypes">Image types to include, comma-separated.</param>
    /// <param name="imageTypeLimit">Images per type.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QueryResult<BaseItemDto>>> Get(
        [FromQuery] Guid? userId,
        [FromQuery] int limit = 20,
        [FromQuery] string? fields = null,
        [FromQuery] string? enableImageTypes = null,
        [FromQuery] int? imageTypeLimit = null)
    {
        if (!Guid.TryParse(User.FindFirstValue(UserIdClaim), out var caller))
        {
            return Forbid();
        }

        var target = userId is { } requested && requested != Guid.Empty ? requested : caller;
        if (target != caller && !User.IsInRole("Administrator"))
        {
            return Forbid();
        }

        var user = _userManager.GetUserById(target);
        if (user is null)
        {
            return NotFound();
        }

        var dtoOptions = new DtoOptions
        {
            Fields = ParseList<ItemFields>(fields),
            EnableImages = true,
            EnableUserData = true
        };
        var imageTypes = ParseList<ImageType>(enableImageTypes);
        if (imageTypes.Length > 0)
        {
            dtoOptions.ImageTypes = imageTypes;
        }

        if (imageTypeLimit is { } perType)
        {
            dtoOptions.ImageTypeLimit = perType;
        }

        var items = await _service.GetRowAsync(user, Math.Clamp(limit, 1, 100), dtoOptions);
        return new QueryResult<BaseItemDto>(items);
    }

    private static T[] ParseList<T>(string? csv)
        where T : struct, Enum =>
        (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<T>(s, true, out var v) ? (T?)v : null)
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .ToArray();
}
