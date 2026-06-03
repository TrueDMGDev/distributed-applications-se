using HouseOfRuns.Api.Data;
using HouseOfRuns.Api.Dtos;
using HouseOfRuns.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/boons")]
public sealed class BoonsController(HouseOfRunsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<BoonResponse>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] string? god,
        [FromQuery] string? effectType,
        [FromQuery] bool? isDuo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "name",
        [FromQuery] string? sortDir = "asc")
    {
        page = Paging.NormalizePage(page);
        pageSize = Paging.NormalizePageSize(pageSize);

        var query = db.Boons.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(boon =>
                boon.Name.ToLower().Contains(term) ||
                boon.God.ToLower().Contains(term) ||
                (boon.Description != null && boon.Description.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(god))
        {
            var term = god.Trim().ToLower();
            query = query.Where(boon => boon.God.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(effectType))
        {
            var term = effectType.Trim().ToLower();
            query = query.Where(boon => boon.EffectType.ToLower().Contains(term));
        }

        if (isDuo.HasValue)
        {
            query = query.Where(boon => boon.IsDuo == isDuo.Value);
        }

        var total = await query.CountAsync();
        query = Sort(query, sortBy, sortDir);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResponse<BoonResponse>(items.Select(ToResponse).ToList(), page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BoonResponse>> Get(Guid id)
    {
        var boon = await db.Boons.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id);
        return boon is null ? NotFound() : Ok(ToResponse(boon));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<ActionResult<BoonResponse>> Create(BoonRequest request)
    {
        var boon = new Boon();
        Apply(request, boon);
        db.Boons.Add(boon);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = boon.Id }, ToResponse(boon));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BoonResponse>> Update(Guid id, BoonRequest request)
    {
        var boon = await db.Boons.FirstOrDefaultAsync(candidate => candidate.Id == id);
        if (boon is null)
        {
            return NotFound();
        }

        Apply(request, boon);
        await db.SaveChangesAsync();
        return Ok(ToResponse(boon));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var boon = await db.Boons.FirstOrDefaultAsync(candidate => candidate.Id == id);
        if (boon is null)
        {
            return NotFound();
        }

        db.Boons.Remove(boon);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static IQueryable<Boon> Sort(IQueryable<Boon> query, string? sortBy, string? sortDir)
    {
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "name").ToLowerInvariant() switch
        {
            "god" => desc ? query.OrderByDescending(boon => boon.God) : query.OrderBy(boon => boon.God),
            "effecttype" => desc ? query.OrderByDescending(boon => boon.EffectType) : query.OrderBy(boon => boon.EffectType),
            "level" => desc ? query.OrderByDescending(boon => boon.Level) : query.OrderBy(boon => boon.Level),
            _ => desc ? query.OrderByDescending(boon => boon.Name) : query.OrderBy(boon => boon.Name)
        };
    }

    private static void Apply(BoonRequest request, Boon boon)
    {
        boon.Name = request.Name.Trim();
        boon.God = request.God.Trim();
        boon.EffectType = request.EffectType.Trim();
        boon.Level = request.Level;
        boon.PowerScale = request.PowerScale;
        boon.IsDuo = request.IsDuo;
        boon.IsLegendary = request.IsLegendary;
        boon.Description = request.Description;
    }

    private static BoonResponse ToResponse(Boon boon) => new(
        boon.Id,
        boon.Name,
        boon.God,
        boon.EffectType,
        boon.Level,
        boon.PowerScale,
        boon.IsDuo,
        boon.IsLegendary,
        boon.Description,
        boon.CreatedAt);
}
