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
[Route("api/runs")]
public sealed class RunsController(HouseOfRunsDbContext db) : ControllerBase
{
    [HttpGet("public")]
    public async Task<ActionResult<PagedResponse<RunResponse>>> GetPublic(
        [FromQuery] string? q,
        [FromQuery] Guid? weaponId,
        [FromQuery] string? result,
        [FromQuery] string? finalBiome,
        [FromQuery] int? minHeat,
        [FromQuery] int? maxHeat,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 9,
        [FromQuery] string? sortBy = "playedAt",
        [FromQuery] string? sortDir = "desc")
    {
        page = Paging.NormalizePage(page);
        pageSize = Paging.NormalizePageSize(pageSize);
        var currentUserId = User.GetRequiredUserId();

        var query = ApplyRunFilters(
            IncludeRunDetails(db.Runs.AsNoTracking()).Where(run => run.IsPublic),
            q,
            weaponId,
            result,
            finalBiome,
            minHeat,
            maxHeat);

        var total = await query.CountAsync();
        query = Sort(query, sortBy, sortDir);

        var runs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResponse<RunResponse>(runs.Select(run => ToResponse(run, currentUserId)).ToList(), page, pageSize, total));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<RunResponse>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] Guid? userId,
        [FromQuery] Guid? weaponId,
        [FromQuery] string? result,
        [FromQuery] string? finalBiome,
        [FromQuery] int? minHeat,
        [FromQuery] int? maxHeat,
        [FromQuery] bool mineOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "playedAt",
        [FromQuery] string? sortDir = "desc")
    {
        page = Paging.NormalizePage(page);
        pageSize = Paging.NormalizePageSize(pageSize);
        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();

        var query = IncludeRunDetails(db.Runs.AsNoTracking());

        if (!isAdmin || mineOnly)
        {
            query = query.Where(run => run.UserId == currentUserId);
        }

        if (isAdmin && userId.HasValue)
        {
            query = query.Where(run => run.UserId == userId.Value);
        }

        query = ApplyRunFilters(query, q, weaponId, result, finalBiome, minHeat, maxHeat);

        var total = await query.CountAsync();
        query = Sort(query, sortBy, sortDir);

        var runs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResponse<RunResponse>(runs.Select(run => ToResponse(run, currentUserId)).ToList(), page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RunResponse>> Get(Guid id)
    {
        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();
        var run = await IncludeRunDetails(db.Runs.AsNoTracking())
            .FirstOrDefaultAsync(candidate => candidate.Id == id);

        if (run is null)
        {
            return NotFound();
        }

        return isAdmin || run.IsPublic || run.UserId == currentUserId ? Ok(ToResponse(run, currentUserId)) : Forbid();
    }

    [HttpPost]
    public async Task<ActionResult<RunResponse>> Create(RunRequest request)
    {
        var currentUserId = User.GetRequiredUserId();
        var weaponExists = await db.Weapons.AnyAsync(weapon => weapon.Id == request.WeaponId);
        if (!weaponExists)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid weapon",
                Detail = "The selected weapon does not exist.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var boonIds = request.Boons
            .Select(boon => boon.BoonId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (boonIds.Count > 0)
        {
            var existingBoonIds = await db.Boons
                .Where(boon => boonIds.Contains(boon.Id))
                .Select(boon => boon.Id)
                .ToListAsync();

            if (existingBoonIds.Count != boonIds.Count)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid boon",
                    Detail = "One or more selected boons do not exist.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        var run = new Run { UserId = currentUserId };
        Apply(request, run);
        run.RunBoons = request.Boons
            .Where(boon => boon.BoonId != Guid.Empty)
            .Select(ToRunBoon)
            .ToList();

        db.Runs.Add(run);
        await db.SaveChangesAsync();

        var created = await IncludeRunDetails(db.Runs.AsNoTracking())
            .FirstAsync(candidate => candidate.Id == run.Id);

        return CreatedAtAction(nameof(Get), new { id = run.Id }, ToResponse(created, currentUserId));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RunResponse>> Update(Guid id, RunRequest request)
    {
        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();
        var run = await db.Runs.FirstOrDefaultAsync(candidate => candidate.Id == id);

        if (run is null)
        {
            return NotFound();
        }

        if (!isAdmin && run.UserId != currentUserId)
        {
            return Forbid();
        }

        var weaponExists = await db.Weapons.AnyAsync(weapon => weapon.Id == request.WeaponId);
        if (!weaponExists)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid weapon",
                Detail = "The selected weapon does not exist.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var boonIds = request.Boons
            .Select(boon => boon.BoonId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (boonIds.Count > 0)
        {
            var existingBoonIds = await db.Boons
                .Where(boon => boonIds.Contains(boon.Id))
                .Select(boon => boon.Id)
                .ToListAsync();

            if (existingBoonIds.Count != boonIds.Count)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid boon",
                    Detail = "One or more selected boons do not exist.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        Apply(request, run);
        run.UpdatedAt = DateTime.UtcNow;

        var runBoons = request.Boons
            .Where(boon => boon.BoonId != Guid.Empty)
            .Select(requestBoon =>
            {
                var runBoon = ToRunBoon(requestBoon);
                runBoon.RunId = id;
                return runBoon;
            })
            .ToList();

        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        await db.RunBoons.Where(runBoon => runBoon.RunId == id).ExecuteDeleteAsync();
        if (runBoons.Count > 0)
        {
            db.RunBoons.AddRange(runBoons);
            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        var updated = await IncludeRunDetails(db.Runs.AsNoTracking())
            .FirstAsync(candidate => candidate.Id == run.Id);

        return Ok(ToResponse(updated, currentUserId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();
        var run = await db.Runs.FirstOrDefaultAsync(candidate => candidate.Id == id);
        if (run is null)
        {
            return NotFound();
        }

        if (!isAdmin && run.UserId != currentUserId)
        {
            return Forbid();
        }

        db.Runs.Remove(run);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static IQueryable<Run> IncludeRunDetails(IQueryable<Run> query) =>
        query.Include(run => run.User)
            .Include(run => run.Weapon)
            .Include(run => run.Likes)
            .Include(run => run.Comments)
            .ThenInclude(comment => comment.User)
            .Include(run => run.RunBoons)
            .ThenInclude(runBoon => runBoon.Boon);

    private static IQueryable<Run> ApplyRunFilters(
        IQueryable<Run> query,
        string? q,
        Guid? weaponId,
        string? result,
        string? finalBiome,
        int? minHeat,
        int? maxHeat)
    {
        if (weaponId.HasValue)
        {
            query = query.Where(run => run.WeaponId == weaponId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(run =>
                run.Title.ToLower().Contains(term) ||
                (run.Notes != null && run.Notes.ToLower().Contains(term)) ||
                (run.User != null && run.User.DisplayName.ToLower().Contains(term)) ||
                (run.User != null && run.User.UserName.ToLower().Contains(term)) ||
                (run.Weapon != null && run.Weapon.Name.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(result))
        {
            var term = result.Trim().ToLower();
            query = query.Where(run => run.Result.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(finalBiome))
        {
            var term = finalBiome.Trim().ToLower();
            query = query.Where(run => run.FinalBiome.ToLower().Contains(term));
        }

        if (minHeat.HasValue)
        {
            query = query.Where(run => run.HeatLevel >= minHeat.Value);
        }

        if (maxHeat.HasValue)
        {
            query = query.Where(run => run.HeatLevel <= maxHeat.Value);
        }

        return query;
    }

    private static IQueryable<Run> Sort(IQueryable<Run> query, string? sortBy, string? sortDir)
    {
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "playedAt").ToLowerInvariant() switch
        {
            "title" => desc ? query.OrderByDescending(run => run.Title) : query.OrderBy(run => run.Title),
            "heat" => desc ? query.OrderByDescending(run => run.HeatLevel) : query.OrderBy(run => run.HeatLevel),
            "duration" => desc ? query.OrderByDescending(run => run.DurationSeconds) : query.OrderBy(run => run.DurationSeconds),
            "likes" => desc ? query.OrderByDescending(run => run.Likes.Count(like => like.IsActive)) : query.OrderBy(run => run.Likes.Count(like => like.IsActive)),
            "result" => desc ? query.OrderByDescending(run => run.Result) : query.OrderBy(run => run.Result),
            "createdat" => desc ? query.OrderByDescending(run => run.CreatedAt) : query.OrderBy(run => run.CreatedAt),
            _ => desc ? query.OrderByDescending(run => run.PlayedAt) : query.OrderBy(run => run.PlayedAt)
        };
    }

    private static void Apply(RunRequest request, Run run)
    {
        run.WeaponId = request.WeaponId;
        run.Title = request.Title.Trim();
        run.HeatLevel = request.HeatLevel;
        run.DurationSeconds = request.DurationSeconds;
        run.Result = request.Result.Trim();
        run.FinalBiome = request.FinalBiome.Trim();
        run.DefeatedBoss = request.DefeatedBoss;
        run.PlayedAt = request.PlayedAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.PlayedAt, DateTimeKind.Utc)
            : request.PlayedAt.ToUniversalTime();
        run.IsPublic = request.IsPublic;
        run.Source = NormalizeSource(request.Source);
        run.Notes = request.Notes;
        run.ScreenshotUrl = request.ScreenshotUrl;
    }

    private static RunBoon ToRunBoon(RunBoonRequest request) => new()
    {
        BoonId = request.BoonId,
        SlotType = request.SlotType.Trim(),
        LevelUsed = request.LevelUsed,
        IsCoreBoon = request.IsCoreBoon,
        PomLevel = request.PomLevel,
        Notes = request.Notes
    };

    private static RunResponse ToResponse(Run run, Guid currentUserId) => new(
        run.Id,
        run.UserId,
        run.User?.UserName ?? string.Empty,
        run.User?.DisplayName ?? string.Empty,
        run.WeaponId,
        run.Weapon?.Name ?? string.Empty,
        run.Weapon?.AspectName ?? string.Empty,
        run.Title,
        run.HeatLevel,
        run.DurationSeconds,
        run.Result,
        run.FinalBiome,
        run.DefeatedBoss,
        run.PlayedAt,
        run.IsPublic,
        run.Source,
        run.Notes,
        run.ScreenshotUrl,
        run.CreatedAt,
        run.UpdatedAt,
        run.Likes.Count(like => like.IsActive),
        run.Comments.Count(comment => !comment.IsDeleted),
        run.Likes.Any(like => like.IsActive && like.UserId == currentUserId),
        run.RunBoons
            .OrderByDescending(runBoon => runBoon.IsCoreBoon)
            .ThenBy(runBoon => runBoon.SlotType)
            .Select(ToRunBoonResponse)
            .ToList(),
        run.Comments
            .Where(comment => !comment.IsDeleted)
            .OrderByDescending(comment => comment.CreatedAt)
            .Take(3)
            .Select(ToCommentResponse)
            .ToList());

    private static RunBoonResponse ToRunBoonResponse(RunBoon runBoon) => new(
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

    private static string NormalizeSource(string? source) =>
        source != null &&
        (source.Contains("import", StringComparison.OrdinalIgnoreCase) ||
         source.Contains("export", StringComparison.OrdinalIgnoreCase))
            ? "Imported"
            : "Manual";

    private static RunCommentResponse ToCommentResponse(RunComment comment) => new(
        comment.Id,
        comment.RunId,
        comment.UserId,
        comment.User?.UserName ?? string.Empty,
        comment.User?.DisplayName ?? string.Empty,
        comment.Body,
        comment.IsEdited,
        comment.CreatedAt,
        comment.UpdatedAt);
}
