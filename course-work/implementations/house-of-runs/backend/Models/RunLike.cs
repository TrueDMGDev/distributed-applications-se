using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Models;

public sealed class RunLike
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }

    public Run? Run { get; set; }

    public Guid UserId { get; set; }

    public AppUser? User { get; set; }

    [Range(1, 1)]
    public int Value { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
