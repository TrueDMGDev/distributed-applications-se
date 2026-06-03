using HouseOfRuns.Api.Data;
using HouseOfRuns.Api.Dtos;
using HouseOfRuns.Api.Models;
using HouseOfRuns.Api.Security;
using HouseOfRuns.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    HouseOfRunsDbContext db,
    PasswordHasher passwordHasher,
    TokenService tokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUserName = request.UserName.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(user =>
            user.Email.ToLower() == normalizedEmail || user.UserName.ToLower() == normalizedUserName);

        if (exists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Account already exists",
                Detail = "A user with this email or username already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var user = new AppUser
        {
            UserName = request.UserName.Trim(),
            Email = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = "User",
            Reputation = 0,
            IsActive = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(new AuthResponse(tokenService.CreateToken(user), ToResponse(user)));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var lookup = request.EmailOrUserName.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(candidate =>
            candidate.Email.ToLower() == lookup || candidate.UserName.ToLower() == lookup);

        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials",
                Detail = "The email/username and password combination is not valid.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        return Ok(new AuthResponse(tokenService.CreateToken(user), ToResponse(user)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var userId = User.GetRequiredUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == userId);
        return user is null ? Unauthorized() : Ok(ToResponse(user));
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
}
