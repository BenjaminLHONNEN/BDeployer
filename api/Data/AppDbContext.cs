using BDeployer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BDeployer.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectEnvironment> ProjectEnvironments => Set<ProjectEnvironment>();
    public DbSet<Deployment> Deployments => Set<Deployment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.GitUrl).HasMaxLength(2000);
            entity.HasMany(x => x.Environments)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectEnvironment>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.WorkingDirectory).HasMaxLength(2000);
            entity.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
            entity.HasMany(x => x.Deployments)
                .WithOne(x => x.Environment)
                .HasForeignKey(x => x.EnvironmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Deployment>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.CommitBefore).HasMaxLength(64);
            entity.Property(x => x.CommitAfter).HasMaxLength(64);
        });
    }
}
