using AemEnersolSync.Services;
using Microsoft.AspNetCore.Mvc;

namespace AemEnersolSync.Controllers;

/// <summary>
/// Triggers a sync run. One request drives the whole chain — login, fetch, upsert — with
/// no tokens to copy by hand in between.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ISyncService syncService, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>Syncs the live dataset from GetPlatformWellActual.</summary>
    [HttpPost("actual")]
    [ProducesResponseType(typeof(SyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<ActionResult<SyncResult>> Actual(CancellationToken cancellationToken) =>
        RunAsync(PlatformWellDataset.Actual, cancellationToken);

    /// <summary>
    /// Syncs GetPlatformWellDummy, whose payload drops createdAt/updatedAt and adds an
    /// unmapped key — the run that demonstrates the sync survives a changed shape.
    /// </summary>
    [HttpPost("dummy")]
    [ProducesResponseType(typeof(SyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<ActionResult<SyncResult>> Dummy(CancellationToken cancellationToken) =>
        RunAsync(PlatformWellDataset.Dummy, cancellationToken);

    private async Task<ActionResult<SyncResult>> RunAsync(
        PlatformWellDataset dataset,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _syncService.SyncAsync(dataset, cancellationToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            // An upstream or configuration failure is not the caller's fault, so it is
            // reported as a gateway error rather than surfacing as an unhandled 500.
            _logger.LogError(ex, "{Dataset} sync failed", dataset);
            return Problem(
                title: "The AEM Enersol API could not be synced.",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
