namespace BDeployer.Api.Models;

public sealed class ProjectEnvironment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public required string Name { get; set; }
    public required string WorkingDirectory { get; set; }
    public required string DeploymentScript { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Project Project { get; set; } = null!;
    public List<Deployment> Deployments { get; set; } = [];
}
