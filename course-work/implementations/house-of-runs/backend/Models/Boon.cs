using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Models;

public sealed class Boon
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string God { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string EffectType { get; set; } = string.Empty;

    public int Level { get; set; }

    public decimal PowerScale { get; set; }

    public bool IsDuo { get; set; }

    public bool IsLegendary { get; set; }

    [MaxLength(600)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RunBoon> RunBoons { get; set; } = new List<RunBoon>();
}
