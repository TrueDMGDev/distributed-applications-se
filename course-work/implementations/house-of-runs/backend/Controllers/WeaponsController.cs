using HouseOfRuns.Api.Data;
using HouseOfRuns.Api.Dtos;
using HouseOfRuns.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/weapons")]
public sealed class WeaponsController(HouseOfRunsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<WeaponResponse>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] string? weaponType,
        [FromQuery] bool? isUnlocked,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "name",
        [FromQuery] string? sortDir = "asc")
    {
        page = Paging.NormalizePage(page);
        pageSize = Paging.NormalizePageSize(pageSize);

        var query = db.Weapons.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(weapon =>
                weapon.Name.ToLower().Contains(term) ||
                weapon.AspectName.ToLower().Contains(term) ||
                (weapon.Description != null && weapon.Description.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(weaponType))
        {
            var type = weaponType.Trim().ToLower();
            query = query.Where(weapon => weapon.WeaponType.ToLower().Contains(type));
        }

        if (isUnlocked.HasValue)
        {
            query = query.Where(weapon => weapon.IsUnlocked == isUnlocked.Value);
        }

        var total = await query.CountAsync();
        query = Sort(query, sortBy, sortDir);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResponse<WeaponResponse>(items.Select(ToResponse).ToList(), page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WeaponResponse>> Get(Guid id)
    {
        var weapon = await db.Weapons.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id);
        return weapon is null ? NotFound() : Ok(ToResponse(weapon));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<ActionResult<WeaponResponse>> Create(WeaponRequest request)
    {
        var weapon = new Weapon();
        Apply(request, weapon);
        db.Weapons.Add(weapon);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = weapon.Id }, ToResponse(weapon));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WeaponResponse>> Update(Guid id, WeaponRequest request)
    {
        var weapon = await db.Weapons.FirstOrDefaultAsync(candidate => candidate.Id == id);
        if (weapon is null)
        {
            return NotFound();
        }

        Apply(request, weapon);
        await db.SaveChangesAsync();
        return Ok(ToResponse(weapon));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var weapon = await db.Weapons.FirstOrDefaultAsync(candidate => candidate.Id == id);
        if (weapon is null)
        {
            return NotFound();
        }

        db.Weapons.Remove(weapon);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static IQueryable<Weapon> Sort(IQueryable<Weapon> query, string? sortBy, string? sortDir)
    {
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "name").ToLowerInvariant() switch
        {
            "aspect" => desc ? query.OrderByDescending(weapon => weapon.AspectName) : query.OrderBy(weapon => weapon.AspectName),
            "type" => desc ? query.OrderByDescending(weapon => weapon.WeaponType) : query.OrderBy(weapon => weapon.WeaponType),
            "damage" => desc ? query.OrderByDescending(weapon => weapon.BaseDamage) : query.OrderBy(weapon => weapon.BaseDamage),
            "titanbloodlevel" => desc ? query.OrderByDescending(weapon => weapon.TitanBloodLevel) : query.OrderBy(weapon => weapon.TitanBloodLevel),
            _ => desc ? query.OrderByDescending(weapon => weapon.Name) : query.OrderBy(weapon => weapon.Name)
        };
    }

    private static void Apply(WeaponRequest request, Weapon weapon)
    {
        weapon.Name = request.Name.Trim();
        weapon.AspectName = request.AspectName.Trim();
        weapon.WeaponType = request.WeaponType.Trim();
        weapon.TitanBloodLevel = request.TitanBloodLevel;
        weapon.UnlockCost = request.UnlockCost;
        weapon.BaseDamage = request.BaseDamage;
        weapon.IsUnlocked = request.IsUnlocked;
        weapon.Description = request.Description;
    }

    private static WeaponResponse ToResponse(Weapon weapon) => new(
        weapon.Id,
        weapon.Name,
        weapon.AspectName,
        weapon.WeaponType,
        weapon.TitanBloodLevel,
        weapon.UnlockCost,
        weapon.BaseDamage,
        weapon.IsUnlocked,
        weapon.Description,
        weapon.CreatedAt);
}
