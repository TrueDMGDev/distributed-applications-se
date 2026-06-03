using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Models;

public sealed class RunBoon
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }

    public Run? Run { get; set; }

    public Guid BoonId { get; set; }

    public Boon? Boon { get; set; }

    [Required, MaxLength(50)]
    public string SlotType { get; set; } = string.Empty;

    public int LevelUsed { get; set; } = 1;

    public bool IsCoreBoon { get; set; }

    public int PomLevel { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
