using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Dtos;

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

    public bool IsUnlocked { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}
