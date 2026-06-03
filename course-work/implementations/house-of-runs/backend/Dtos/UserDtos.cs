using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Dtos;

public sealed record UserResponse(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string Role,
    int Reputation,
    bool IsActive,
    DateTime CreatedAt);

public sealed class CreateUserRequest
{
    [Required, MaxLength(32)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(120), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(80)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Bio { get; set; }

    [MaxLength(300), Url]
    public string? AvatarUrl { get; set; }

    [Required, MaxLength(20)]
    public string Role { get; set; } = "User";

    [Range(0, 999999)]
    public int Reputation { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class UpdateUserRequest
{
    [Required, MaxLength(32)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(120), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Bio { get; set; }

    [MaxLength(300), Url]
    public string? AvatarUrl { get; set; }

    [Required, MaxLength(20)]
    public string Role { get; set; } = "User";

    [Range(0, 999999)]
    public int Reputation { get; set; }

    public bool IsActive { get; set; }
}
