using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Models;

public sealed class Weapon
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string AspectName { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string WeaponType { get; set; } = string.Empty;

    public int TitanBloodLevel { get; set; }

    public int UnlockCost { get; set; }

    public decimal BaseDamage { get; set; }

    public bool IsUnlocked { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Run> Runs { get; set; } = new List<Run>();
}
