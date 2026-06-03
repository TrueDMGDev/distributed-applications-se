using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Dtos;

public sealed class RegisterRequest
{
    [Required, MaxLength(32)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(120), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(80)]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, MaxLength(120)]
    public string EmailOrUserName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Password { get; set; } = string.Empty;
}

public sealed record AuthResponse(string Token, UserResponse User);
