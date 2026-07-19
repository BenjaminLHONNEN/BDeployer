namespace BDeployer.Api.Models;

public sealed class Deployment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnvironmentId { get; set; }
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Running;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public required string ScriptExecuted { get; set; }
    public int? ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public string? CommitBefore { get; set; }
    public string? CommitAfter { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
}

public enum DeploymentStatus
{
    Running,
    Succeeded,
    Failed,
    TimedOut
}
