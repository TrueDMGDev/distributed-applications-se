using HouseOfRuns.Api.Data;
using HouseOfRuns.Api.Dtos;
using HouseOfRuns.Api.Models;
using HouseOfRuns.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/run-boons")]
public sealed class RunBoonsController(HouseOfRunsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<RunBoonResponse>>> GetAll(
        [FromQuery] Guid? runId,
        [FromQuery] Guid? boonId,
        [FromQuery] string? slotType,
        [FromQuery] bool? isCore,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "addedAt",
        [FromQuery] string? sortDir = "desc")
    {
        page = Paging.NormalizePage(page);
        pageSize = Paging.NormalizePageSize(pageSize);
        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();

        var query = IncludeDetails(db.RunBoons.AsNoTracking())
            .Where(runBoon => runBoon.Run != null);

        if (!isAdmin)
        {
            query = query.Where(runBoon => runBoon.Run != null && runBoon.Run.UserId == currentUserId);
        }

        if (runId.HasValue)
        {
            query = query.Where(runBoon => runBoon.RunId == runId.Value);
        }

        if (boonId.HasValue)
        {
            query = query.Where(runBoon => runBoon.BoonId == boonId.Value);
        }

        if (!string.IsNullOrWhiteSpace(slotType))
        {
            var term = slotType.Trim().ToLower();
            query = query.Where(runBoon => runBoon.SlotType.ToLower().Contains(term));
        }

        if (isCore.HasValue)
        {
            query = query.Where(runBoon => runBoon.IsCoreBoon == isCore.Value);
        }

        var total = await query.CountAsync();
        query = Sort(query, sortBy, sortDir);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResponse<RunBoonResponse>(items.Select(ToResponse).ToList(), page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RunBoonResponse>> Get(Guid id)
    {
        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();
        var runBoon = await IncludeDetails(db.RunBoons.AsNoTracking())
            .FirstOrDefaultAsync(candidate => candidate.Id == id);

        if (runBoon is null)
        {
            return NotFound();
        }

        return runBoon.Run is not null && (isAdmin || runBoon.Run.UserId == currentUserId)
            ? Ok(ToResponse(runBoon))
            : Forbid();
    }

    [HttpPost]
    public async Task<ActionResult<RunBoonResponse>> Create(RunBoonRequest request)
    {
        if (request.RunId == Guid.Empty || request.BoonId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid run boon",
                Detail = "Run and boon are required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();
        var run = await db.Runs.FirstOrDefaultAsync(candidate => candidate.Id == request.RunId);
        if (run is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid run",
                Detail = "The selected run does not exist.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!isAdmin && run.UserId != currentUserId)
        {
            return Forbid();
        }

        var boonExists = await db.Boons.AnyAsync(boon => boon.Id == request.BoonId);
        if (!boonExists)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid boon",
                Detail = "The selected boon does not exist.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var runBoon = new RunBoon();
        Apply(request, runBoon);
        db.RunBoons.Add(runBoon);
        await db.SaveChangesAsync();

        var created = await IncludeDetails(db.RunBoons.AsNoTracking())
            .FirstAsync(candidate => candidate.Id == runBoon.Id);

        return CreatedAtAction(nameof(Get), new { id = runBoon.Id }, ToResponse(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RunBoonResponse>> Update(Guid id, RunBoonRequest request)
    {
        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();
        var runBoon = await db.RunBoons
            .Include(candidate => candidate.Run)
            .FirstOrDefaultAsync(candidate => candidate.Id == id);

        if (runBoon is null)
        {
            return NotFound();
        }

        if (runBoon.Run is null || (!isAdmin && runBoon.Run.UserId != currentUserId))
        {
            return Forbid();
        }

        var targetRun = await db.Runs.FirstOrDefaultAsync(run => run.Id == request.RunId);
        if (targetRun is null || (!isAdmin && targetRun.UserId != currentUserId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid run",
                Detail = "The selected run does not exist or is not yours.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var boonExists = await db.Boons.AnyAsync(boon => boon.Id == request.BoonId);
        if (!boonExists)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid boon",
                Detail = "The selected boon does not exist.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        Apply(request, runBoon);
        await db.SaveChangesAsync();

        var updated = await IncludeDetails(db.RunBoons.AsNoTracking())
            .FirstAsync(candidate => candidate.Id == runBoon.Id);

        return Ok(ToResponse(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();
        var runBoon = await db.RunBoons
            .Include(candidate => candidate.Run)
            .FirstOrDefaultAsync(candidate => candidate.Id == id);

        if (runBoon is null)
        {
            return NotFound();
        }

        if (runBoon.Run is null || (!isAdmin && runBoon.Run.UserId != currentUserId))
        {
            return Forbid();
        }

        db.RunBoons.Remove(runBoon);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static IQueryable<RunBoon> IncludeDetails(IQueryable<RunBoon> query) =>
        query.Include(runBoon => runBoon.Run)
            .Include(runBoon => runBoon.Boon);

    private static IQueryable<RunBoon> Sort(IQueryable<RunBoon> query, string? sortBy, string? sortDir)
    {
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "addedAt").ToLowerInvariant() switch
        {
            "slot" => desc ? query.OrderByDescending(runBoon => runBoon.SlotType) : query.OrderBy(runBoon => runBoon.SlotType),
            "level" => desc ? query.OrderByDescending(runBoon => runBoon.LevelUsed) : query.OrderBy(runBoon => runBoon.LevelUsed),
            _ => desc ? query.OrderByDescending(runBoon => runBoon.AddedAt) : query.OrderBy(runBoon => runBoon.AddedAt)
        };
    }

    private static void Apply(RunBoonRequest request, RunBoon runBoon)
    {
        runBoon.RunId = request.RunId;
        runBoon.BoonId = request.BoonId;
        runBoon.SlotType = request.SlotType.Trim();
        runBoon.LevelUsed = request.LevelUsed;
        runBoon.IsCoreBoon = request.IsCoreBoon;
        runBoon.PomLevel = request.PomLevel;
        runBoon.Notes = request.Notes;
    }

    private static RunBoonResponse ToResponse(RunBoon runBoon) => new(
        runBoon.Id,
        runBoon.RunId,
        runBoon.BoonId,
        runBoon.Boon?.Name ?? string.Empty,
        runBoon.Boon?.God ?? string.Empty,
        runBoon.SlotType,
        runBoon.LevelUsed,
        runBoon.IsCoreBoon,
        runBoon.PomLevel,
        runBoon.Notes,
        runBoon.AddedAt);
}
