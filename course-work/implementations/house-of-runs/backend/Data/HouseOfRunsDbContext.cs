using HouseOfRuns.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Data;

public sealed class HouseOfRunsDbContext(DbContextOptions<HouseOfRunsDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<Weapon> Weapons => Set<Weapon>();

    public DbSet<Boon> Boons => Set<Boon>();

    public DbSet<Run> Runs => Set<Run>();

    public DbSet<RunBoon> RunBoons => Set<RunBoon>();

    public DbSet<RunComment> RunComments => Set<RunComment>();

    public DbSet<RunLike> RunLikes => Set<RunLike>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(user => user.Role).HasMaxLength(20);
            entity.HasIndex(user => user.UserName).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<Weapon>(entity =>
        {
            entity.Property(weapon => weapon.BaseDamage).HasPrecision(8, 2);
            entity.HasIndex(weapon => new { weapon.Name, weapon.AspectName }).IsUnique();
        });

        modelBuilder.Entity<Boon>(entity =>
        {
            entity.Property(boon => boon.PowerScale).HasPrecision(8, 2);
            entity.HasIndex(boon => new { boon.Name, boon.God }).IsUnique();
        });

        modelBuilder.Entity<Run>(entity =>
        {
            entity.HasOne(run => run.User)
                .WithMany(user => user.Runs)
                .HasForeignKey(run => run.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(run => run.Weapon)
                .WithMany(weapon => weapon.Runs)
                .HasForeignKey(run => run.WeaponId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(run => new { run.IsPublic, run.PlayedAt });
        });

        modelBuilder.Entity<RunBoon>(entity =>
        {
            entity.HasOne(runBoon => runBoon.Run)
                .WithMany(run => run.RunBoons)
                .HasForeignKey(runBoon => runBoon.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(runBoon => runBoon.Boon)
                .WithMany(boon => boon.RunBoons)
                .HasForeignKey(runBoon => runBoon.BoonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(runBoon => new { runBoon.RunId, runBoon.BoonId });
        });

        modelBuilder.Entity<RunComment>(entity =>
        {
            entity.HasOne(comment => comment.Run)
                .WithMany(run => run.Comments)
                .HasForeignKey(comment => comment.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(comment => comment.User)
                .WithMany(user => user.RunComments)
                .HasForeignKey(comment => comment.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(comment => new { comment.RunId, comment.CreatedAt });
            entity.HasIndex(comment => new { comment.UserId, comment.CreatedAt });
        });

        modelBuilder.Entity<RunLike>(entity =>
        {
            entity.HasOne(like => like.Run)
                .WithMany(run => run.Likes)
                .HasForeignKey(like => like.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(like => like.User)
                .WithMany(user => user.RunLikes)
                .HasForeignKey(like => like.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(like => new { like.RunId, like.UserId }).IsUnique();
            entity.HasIndex(like => new { like.UserId, like.CreatedAt });
        });
    }
}
