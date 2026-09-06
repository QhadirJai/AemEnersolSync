using AemEnersolSync.Data;
using AemEnersolSync.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AemEnersolSync.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlatformsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public PlatformsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Returns the synced platforms without their wells, to keep the list light.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Platform>>> Get(CancellationToken cancellationToken) =>
        Ok(await _dbContext.Platforms
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken));

    /// <summary>Returns one platform along with its wells.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Platform), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Platform>> GetById(int id, CancellationToken cancellationToken)
    {
        var platform = await _dbContext.Platforms
            .AsNoTracking()
            .Include(p => p.Wells)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return platform is null ? NotFound() : Ok(platform);
    }
}
