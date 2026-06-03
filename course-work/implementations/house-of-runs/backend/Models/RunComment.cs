using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Models;

public sealed class RunComment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }

    public Run? Run { get; set; }

    public Guid UserId { get; set; }

    public AppUser? User { get; set; }

    [Required, MaxLength(500)]
    public string Body { get; set; } = string.Empty;

    public bool IsEdited { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
