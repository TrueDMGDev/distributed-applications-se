using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Dtos;

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
