using AemEnersolSync.Data;
using AemEnersolSync.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AemEnersolSync.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WellsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public WellsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Returns the synced wells, optionally narrowed to a single platform.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Well>>> Get(
        [FromQuery] int? platformId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Wells.AsNoTracking();

        if (platformId is not null)
        {
            query = query.Where(w => w.PlatformId == platformId);
        }

        return Ok(await query.OrderBy(w => w.Id).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Well), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Well>> GetById(int id, CancellationToken cancellationToken)
    {
        var well = await _dbContext.Wells
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        return well is null ? NotFound() : Ok(well);
    }
}
