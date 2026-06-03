using HouseOfRuns.Api.Data;
using HouseOfRuns.Api.Dtos;
using HouseOfRuns.Api.Models;
using HouseOfRuns.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/users")]
public sealed class UsersController(HouseOfRunsDbContext db, PasswordHasher passwordHasher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<UserResponse>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] string? email,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "createdAt",
        [FromQuery] string? sortDir = "desc")
    {
        page = Paging.NormalizePage(page);
        pageSize = Paging.NormalizePageSize(pageSize);

        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(user =>
                user.UserName.ToLower().Contains(term) ||
                user.DisplayName.ToLower().Contains(term) ||
                (user.Bio != null && user.Bio.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var term = email.Trim().ToLower();
            query = query.Where(user => user.Email.ToLower().Contains(term));
        }

        if (isActive.HasValue)
        {
            query = query.Where(user => user.IsActive == isActive.Value);
        }

        var total = await query.CountAsync();
        query = Sort(query, sortBy, sortDir);

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResponse<UserResponse>(users.Select(ToResponse).ToList(), page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Get(Guid id)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id);
        return user is null ? NotFound() : Ok(ToResponse(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUserName = request.UserName.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(user =>
            user.Email.ToLower() == normalizedEmail || user.UserName.ToLower() == normalizedUserName);

        if (exists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "User already exists",
                Detail = "Email and username must be unique.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var user = new AppUser
        {
            UserName = request.UserName.Trim(),
            Email = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            Bio = request.Bio,
            AvatarUrl = request.AvatarUrl,
            Role = NormalizeRole(request.Role),
            Reputation = request.Reputation,
            IsActive = request.IsActive
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = user.Id }, ToResponse(user));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, UpdateUserRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUserName = request.UserName.Trim().ToLowerInvariant();
        var duplicate = await db.Users.AnyAsync(candidate =>
            candidate.Id != id &&
            (candidate.Email.ToLower() == normalizedEmail || candidate.UserName.ToLower() == normalizedUserName));

        if (duplicate)
        {
            return Conflict(new ProblemDetails
            {
                Title = "User already exists",
                Detail = "Email and username must be unique.",
                Status = StatusCodes.Status409Conflict
            });
        }

        user.UserName = request.UserName.Trim();
        user.Email = normalizedEmail;
        user.DisplayName = request.DisplayName.Trim();
        user.Bio = request.Bio;
        user.AvatarUrl = request.AvatarUrl;
        user.Role = NormalizeRole(request.Role);
        user.Reputation = request.Reputation;
        user.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        return Ok(ToResponse(user));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static IQueryable<AppUser> Sort(IQueryable<AppUser> query, string? sortBy, string? sortDir)
    {
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "createdAt").ToLowerInvariant() switch
        {
            "username" => desc ? query.OrderByDescending(user => user.UserName) : query.OrderBy(user => user.UserName),
            "email" => desc ? query.OrderByDescending(user => user.Email) : query.OrderBy(user => user.Email),
            "displayname" => desc ? query.OrderByDescending(user => user.DisplayName) : query.OrderBy(user => user.DisplayName),
            "reputation" => desc ? query.OrderByDescending(user => user.Reputation) : query.OrderBy(user => user.Reputation),
            _ => desc ? query.OrderByDescending(user => user.CreatedAt) : query.OrderBy(user => user.CreatedAt)
        };
    }

    private static UserResponse ToResponse(AppUser user) => new(
        user.Id,
        user.UserName,
        user.Email,
        user.DisplayName,
        user.Bio,
        user.AvatarUrl,
        user.Role,
        user.Reputation,
        user.IsActive,
        user.CreatedAt);

    private static string NormalizeRole(string role) =>
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "User";
}
