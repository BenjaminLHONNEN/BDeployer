using BDeployer.Api.Models;

namespace BDeployer.Api.Contracts;

public sealed record CreateProjectRequest(
    string Name,
    string GitUrl,
    bool Enabled = true,
    IReadOnlyCollection<CreateEnvironmentRequest>? Environments = null);

public sealed record UpdateProjectRequest(string Name, string GitUrl, bool Enabled);

public sealed record CreateEnvironmentRequest(
    string Name,
    string DeploymentScript,
    int? TimeoutSeconds = null,
    bool Enabled = true);

public sealed record UpdateEnvironmentRequest(
    string Name,
    string DeploymentScript,
    int TimeoutSeconds,
    bool Enabled);

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string GitUrl,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<EnvironmentResponse> Environments);

public sealed record EnvironmentResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string WorkingDirectory,
    string DeploymentScript,
    int TimeoutSeconds,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DeploymentResponse(
    Guid Id,
    Guid EnvironmentId,
    DeploymentStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string ScriptExecuted,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string? CommitBefore,
    string? CommitAfter);

public static class ContractMappings
{
    public static ProjectResponse ToResponse(this Project project) => new(
        project.Id,
        project.Name,
        project.GitUrl,
        project.Enabled,
        project.CreatedAt,
        project.UpdatedAt,
        project.Environments.Select(x => x.ToResponse()).ToArray());

    public static EnvironmentResponse ToResponse(this ProjectEnvironment environment) => new(
        environment.Id,
        environment.ProjectId,
        environment.Name,
        environment.WorkingDirectory,
        environment.DeploymentScript,
        environment.TimeoutSeconds,
        environment.Enabled,
        environment.CreatedAt,
        environment.UpdatedAt);

    public static DeploymentResponse ToResponse(this Deployment deployment) => new(
        deployment.Id,
        deployment.EnvironmentId,
        deployment.Status,
        deployment.StartedAt,
        deployment.FinishedAt,
        deployment.ScriptExecuted,
        deployment.ExitCode,
        deployment.StandardOutput,
        deployment.StandardError,
        deployment.CommitBefore,
        deployment.CommitAfter);
}
