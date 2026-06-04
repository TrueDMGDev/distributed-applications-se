using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Models;

public sealed class Run
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public AppUser? User { get; set; }

    public Guid WeaponId { get; set; }

    public Weapon? Weapon { get; set; }

    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    public int HeatLevel { get; set; }

    public int DurationSeconds { get; set; }

    [Required, MaxLength(20)]
    public string Result { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string FinalBiome { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? DefeatedBoss { get; set; }

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    public bool IsPublic { get; set; }

    [Required, MaxLength(30)]
    public string Source { get; set; } = "Manual";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Required, MinLength(11), MaxLength(999)]
    public string FullDescription { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? ScreenshotUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<RunBoon> RunBoons { get; set; } = new List<RunBoon>();

    public ICollection<RunComment> Comments { get; set; } = new List<RunComment>();

    public ICollection<RunLike> Likes { get; set; } = new List<RunLike>();
}
