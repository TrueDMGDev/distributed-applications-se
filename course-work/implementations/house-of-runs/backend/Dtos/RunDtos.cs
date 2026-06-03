using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Dtos;

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
    [Required]
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
