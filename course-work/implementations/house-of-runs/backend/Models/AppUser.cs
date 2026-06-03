using System.ComponentModel.DataAnnotations;

namespace HouseOfRuns.Api.Models;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(32)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Bio { get; set; }

    [MaxLength(300)]
    public string? AvatarUrl { get; set; }

    [Required, MaxLength(20)]
    public string Role { get; set; } = "User";

    public int Reputation { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Run> Runs { get; set; } = new List<Run>();

    public ICollection<RunComment> RunComments { get; set; } = new List<RunComment>();

    public ICollection<RunLike> RunLikes { get; set; } = new List<RunLike>();
}
