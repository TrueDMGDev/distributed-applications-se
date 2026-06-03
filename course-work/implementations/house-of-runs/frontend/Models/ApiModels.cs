using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Frontend.Models;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

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

public sealed record WeaponResponse(
    Guid Id,
    string Name,
    string AspectName,
    string WeaponType,
    int TitanBloodLevel,
    int UnlockCost,
    decimal BaseDamage,
    bool IsUnlocked,
    string? Description,
    DateTime CreatedAt);

public sealed class WeaponRequest
{
    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string AspectName { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string WeaponType { get; set; } = string.Empty;

    [Range(0, 5)]
    public int TitanBloodLevel { get; set; }

    [Range(0, 25)]
    public int UnlockCost { get; set; }

    [Range(0, 9999)]
    public decimal BaseDamage { get; set; }

    public bool IsUnlocked { get; set; } = true;

    [MaxLength(500)]
    public string? Description { get; set; }
}

public sealed record BoonResponse(
    Guid Id,
    string Name,
    string God,
    string EffectType,
    int Level,
    decimal PowerScale,
    bool IsDuo,
    bool IsLegendary,
    string? Description,
    DateTime CreatedAt);

public sealed class BoonRequest
{
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string God { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string EffectType { get; set; } = string.Empty;

    [Range(1, 20)]
    public int Level { get; set; } = 1;

    [Range(0, 999)]
    public decimal PowerScale { get; set; } = 1;

    public bool IsDuo { get; set; }

    public bool IsLegendary { get; set; }

    [MaxLength(600)]
    public string? Description { get; set; }
}

public sealed record RunBoonResponse(
    Guid Id,
    Guid RunId,
    Guid BoonId,
    string BoonName,
    string God,
    string SlotType,
    int LevelUsed,
    bool IsCoreBoon,
    int PomLevel,
    string? Notes,
    DateTime AddedAt);

public sealed class RunBoonRequest
{
    public Guid RunId { get; set; }

    public Guid BoonId { get; set; }

    [Required, MaxLength(50)]
    public string SlotType { get; set; } = "Build";

    [Range(1, 20)]
    public int LevelUsed { get; set; } = 1;

    public bool IsCoreBoon { get; set; }

    [Range(0, 20)]
    public int PomLevel { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }
}

public sealed record RunResponse(
    Guid Id,
    Guid UserId,
    string UserName,
    string DisplayName,
    Guid WeaponId,
    string WeaponName,
    string AspectName,
    string Title,
    int HeatLevel,
    int DurationSeconds,
    string Result,
    string FinalBiome,
    string? DefeatedBoss,
    DateTime PlayedAt,
    bool IsPublic,
    string Source,
    string? Notes,
    string? ScreenshotUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int LikeCount,
    int CommentCount,
    bool HasLiked,
    IReadOnlyList<RunBoonResponse> Boons,
    IReadOnlyList<RunCommentResponse> RecentComments);

public sealed class RunRequest
{
    public Guid WeaponId { get; set; }

    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [Range(0, 64)]
    public int HeatLevel { get; set; }

    [Range(0, 999999)]
    public int DurationSeconds { get; set; }

    [Required, MaxLength(20)]
    public string Result { get; set; } = "Died";

    [Required, MaxLength(40)]
    public string FinalBiome { get; set; } = "Tartarus";

    [MaxLength(80)]
    public string? DefeatedBoss { get; set; }

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    public bool IsPublic { get; set; }

    [Required, MaxLength(30)]
    public string Source { get; set; } = "Manual";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(300), Url]
    public string? ScreenshotUrl { get; set; }

    public List<RunBoonRequest> Boons { get; set; } = [];
}

public sealed record ImportRunDraftResponse(
    int ImportIndex,
    string Title,
    Guid? WeaponId,
    string? WeaponName,
    int HeatLevel,
    int DurationSeconds,
    string Result,
    string FinalBiome,
    string? DefeatedBoss,
    DateTime PlayedAt,
    string Source,
    string? Notes,
    IReadOnlyList<ImportBoonDraftResponse> Boons);

public sealed record ImportRunsDraftResponse(IReadOnlyList<ImportRunDraftResponse> Runs);

public sealed record ImportBoonDraftResponse(
    Guid? BoonId,
    string Name,
    string? God,
    string SlotType,
    int LevelUsed,
    bool IsCoreBoon);

public sealed record RunCommentResponse(
    Guid Id,
    Guid RunId,
    Guid UserId,
    string UserName,
    string DisplayName,
    string Body,
    bool IsEdited,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class RunCommentRequest
{
    [Required, MaxLength(500)]
    public string Body { get; set; } = string.Empty;
}

public sealed record RunSocialSummaryResponse(
    Guid RunId,
    int LikeCount,
    int CommentCount,
    bool HasLiked);
