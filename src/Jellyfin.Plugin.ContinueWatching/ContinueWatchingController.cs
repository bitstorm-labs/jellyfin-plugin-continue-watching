using System.Net.Mime;
using System.Security.Claims;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.ContinueWatching.Core;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
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
    private readonly ILibraryManager _libraryManager;
    private readonly HiddenStore _hidden;

    public ContinueWatchingController(
        ContinueWatchingService service,
        IUserManager userManager,
        ILibraryManager libraryManager,
        HiddenStore hidden)
    {
        _service = service;
        _userManager = userManager;
        _libraryManager = libraryManager;
        _hidden = hidden;
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
        [FromQuery] int limit = RowRequest.DefaultLimit,
        [FromQuery] string? fields = null,
        [FromQuery] string? enableImageTypes = null,
        [FromQuery] int? imageTypeLimit = null)
    {
        if (ResolveUser(userId, out var user) is { } refused)
        {
            return refused;
        }

        var dtoOptions = new DtoOptions
        {
            Fields = RowRequest.ParseList<ItemFields>(fields),
            EnableImages = true,
            EnableUserData = true
        };
        var imageTypes = RowRequest.ParseList<ImageType>(enableImageTypes);
        if (imageTypes.Length > 0)
        {
            dtoOptions.ImageTypes = imageTypes;
        }

        if (imageTypeLimit is { } perType)
        {
            dtoOptions.ImageTypeLimit = perType;
        }

        var items = await _service.GetRowAsync(user!, RowRequest.ClampLimit(limit), dtoOptions);
        return new QueryResult<BaseItemDto>(items);
    }

    /// <summary>
    /// Removes an episode's series from the row until the user's Next Up for it moves
    /// to another episode (or they play the series again).
    /// </summary>
    /// <param name="itemId">The episode the row is showing.</param>
    /// <param name="userId">Whose row; defaults to the caller. Only an admin may change someone else's.</param>
    [HttpPost("Hidden/{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Hide([FromRoute] Guid itemId, [FromQuery] Guid? userId)
    {
        if (ResolveUser(userId, out var user) is { } refused)
        {
            return refused;
        }

        var item = _libraryManager.GetItemById<BaseItem>(itemId, user);
        if (item is null)
        {
            return NotFound();
        }

        if (item is not Episode { SeriesId: var seriesId } || seriesId == Guid.Empty)
        {
            return BadRequest("Only an episode can be hidden from Continue Watching.");
        }

        _hidden.Hide(user!.Id, seriesId, itemId, DateTime.UtcNow);
        return NoContent();
    }

    /// <summary>Undoes a hide. Succeeds whether or not anything was hidden.</summary>
    /// <param name="itemId">The hidden episode (or its series).</param>
    /// <param name="userId">Whose row; defaults to the caller. Only an admin may change someone else's.</param>
    [HttpDelete("Hidden/{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Unhide([FromRoute] Guid itemId, [FromQuery] Guid? userId)
    {
        if (ResolveUser(userId, out var user) is { } refused)
        {
            return refused;
        }

        var seriesId = _libraryManager.GetItemById(itemId) is Episode { SeriesId: var id } && id != Guid.Empty
            ? id
            : (Guid?)null;
        _hidden.Unhide(user!.Id, itemId, seriesId);
        return NoContent();
    }

    /// <summary>The series currently hidden from the row.</summary>
    /// <param name="userId">Whose list; defaults to the caller. Only an admin may read someone else's.</param>
    [HttpGet("Hidden")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<HiddenEntry>> ListHidden([FromQuery] Guid? userId)
    {
        if (ResolveUser(userId, out var user) is { } refused)
        {
            return refused;
        }

        return Ok(_hidden.List(user!.Id));
    }

    /// <summary>The same who-may-see-whose rule for every endpoint; null when allowed.</summary>
    private ActionResult? ResolveUser(Guid? requested, out User? user)
    {
        user = null;
        _ = Guid.TryParse(User.FindFirstValue(UserIdClaim), out var caller);
        if (RowRequest.TargetUser(caller, requested, User.IsInRole("Administrator")) is not { } target)
        {
            return Forbid();
        }

        user = _userManager.GetUserById(target);
        return user is null ? NotFound() : null;
    }
}
