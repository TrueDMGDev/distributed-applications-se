using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Dtos;

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
    [Required]
    public Guid RunId { get; set; }

    [Required]
    public Guid BoonId { get; set; }

    [Required, MaxLength(50)]
    public string SlotType { get; set; } = string.Empty;

    [Range(1, 20)]
    public int LevelUsed { get; set; } = 1;

    public bool IsCoreBoon { get; set; }

    [Range(0, 20)]
    public int PomLevel { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }
}
