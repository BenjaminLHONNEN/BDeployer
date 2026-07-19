using System.Text;
using BDeployer.Api.Data;
using BDeployer.Api.Models;
using BDeployer.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BDeployer.Api.Services;

public sealed class DeploymentService(
    AppDbContext dbContext,
    DeploymentLock deploymentLock,
    IOptions<DeploymentOptions> options,
    ILogger<DeploymentService> logger)
{
    public async Task<Deployment> DeployAsync(
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        var environment = await dbContext.ProjectEnvironments
            .Include(x => x.Project)
            .SingleOrDefaultAsync(
                x => x.Id == environmentId && x.ProjectId == projectId,
                cancellationToken);
        if (environment is null)
        {
            throw new KeyNotFoundException();
        }

        if (!environment.Project.Enabled || !environment.Enabled)
        {
            throw new InvalidOperationException("Project and environment must be enabled before deployment.");
        }

        var expectedDirectory = ProjectRules.GetWorkingDirectory(
            options.Value.ProjectsRoot,
            projectId,
            environment.Name);
        if (!string.Equals(
                Path.GetFullPath(environment.WorkingDirectory),
                expectedDirectory,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Stored working directory does not match the configured projects root.");
        }

        if (!deploymentLock.TryAcquire(environmentId))
        {
            throw new DeploymentAlreadyRunningException(
                $"A deployment is already running for environment '{environment.Name}'.");
        }

        try
        {
            return await ExecuteAsync(environment, cancellationToken);
        }
        finally
        {
            deploymentLock.Release(environmentId);
        }
    }

    private async Task<Deployment> ExecuteAsync(
        ProjectEnvironment environment,
        CancellationToken cancellationToken)
    {
        var deployment = new Deployment
        {
            EnvironmentId = environment.Id,
            ScriptExecuted = environment.DeploymentScript
        };
        dbContext.Deployments.Add(deployment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var output = new StringBuilder();
        var errors = new StringBuilder();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(environment.TimeoutSeconds);

        try
        {
            await EnsureRepositoryAsync(environment, deadline, output, errors, cancellationToken);
            deployment.CommitBefore = await ReadCommitAsync(
                environment.WorkingDirectory,
                GetRemaining(deadline),
                output,
                errors,
                cancellationToken);

            var status = await RunCheckedAsync(
                "git",
                ["-C", environment.WorkingDirectory, "status", "--porcelain"],
                null,
                GetRemaining(deadline),
                output,
                errors,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(status.StandardOutput))
            {
                throw new InvalidOperationException("Repository contains uncommitted changes; deployment aborted.");
            }

            await RunCheckedAsync(
                "git",
                ["-C", environment.WorkingDirectory, "pull", "--ff-only", "origin", "main"],
                null,
                GetRemaining(deadline),
                output,
                errors,
                cancellationToken);
            deployment.CommitAfter = await ReadCommitAsync(
                environment.WorkingDirectory,
                GetRemaining(deadline),
                output,
                errors,
                cancellationToken);

            var scriptResult = await RunCheckedAsync(
                "/bin/bash",
                ["-euo", "pipefail", "-c", environment.DeploymentScript],
                environment.WorkingDirectory,
                GetRemaining(deadline),
                output,
                errors,
                cancellationToken);
            deployment.ExitCode = scriptResult.ExitCode;
            deployment.Status = DeploymentStatus.Succeeded;
        }
        catch (ProcessTimedOutException exception)
        {
            output.Append(exception.StandardOutput);
            errors.Append(exception.StandardError);
            deployment.Status = DeploymentStatus.TimedOut;
            deployment.StandardError = AppendError(errors, exception.Message);
        }
        catch (DeploymentCommandException exception)
        {
            deployment.ExitCode = exception.ExitCode;
            deployment.Status = DeploymentStatus.Failed;
            deployment.StandardError = AppendError(errors, exception.Message);
        }
        catch (OperationCanceledException)
        {
            deployment.Status = DeploymentStatus.Failed;
            deployment.StandardError = AppendError(errors, "Deployment canceled by the caller.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Deployment {DeploymentId} failed.", deployment.Id);
            deployment.Status = DeploymentStatus.Failed;
            deployment.StandardError = AppendError(errors, exception.Message);
        }
        finally
        {
            deployment.StandardOutput = output.ToString();
            if (string.IsNullOrEmpty(deployment.StandardError))
            {
                deployment.StandardError = errors.ToString();
            }

            deployment.FinishedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        return deployment;
    }

    private static async Task EnsureRepositoryAsync(
        ProjectEnvironment environment,
        DateTimeOffset deadline,
        StringBuilder output,
        StringBuilder errors,
        CancellationToken cancellationToken)
    {
        var gitDirectory = Path.Combine(environment.WorkingDirectory, ".git");
        if (!Directory.Exists(gitDirectory))
        {
            if (Directory.Exists(environment.WorkingDirectory) &&
                Directory.EnumerateFileSystemEntries(environment.WorkingDirectory).Any())
            {
                throw new InvalidOperationException("Working directory exists, is not empty, and is not a Git repository.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(environment.WorkingDirectory)!);
            await RunCheckedAsync(
                "git",
                ["clone", "--branch", "main", "--single-branch", "--", environment.Project.GitUrl, environment.WorkingDirectory],
                null,
                GetRemaining(deadline),
                output,
                errors,
                cancellationToken);
        }

        var remote = await RunCheckedAsync(
            "git",
            ["-C", environment.WorkingDirectory, "remote", "get-url", "origin"],
            null,
            GetRemaining(deadline),
            output,
            errors,
            cancellationToken);
        if (!string.Equals(
                remote.StandardOutput.Trim(),
                environment.Project.GitUrl,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Repository origin does not match the configured Git URL.");
        }
    }

    private static async Task<string> ReadCommitAsync(
        string workingDirectory,
        TimeSpan timeout,
        StringBuilder output,
        StringBuilder errors,
        CancellationToken cancellationToken)
    {
        var result = await RunCheckedAsync(
            "git",
            ["-C", workingDirectory, "rev-parse", "HEAD"],
            null,
            timeout,
            output,
            errors,
            cancellationToken);
        return result.StandardOutput.Trim();
    }

    private static async Task<ProcessResult> RunCheckedAsync(
        string fileName,
        IReadOnlyCollection<string> arguments,
        string? workingDirectory,
        TimeSpan timeout,
        StringBuilder output,
        StringBuilder errors,
        CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            fileName,
            arguments,
            workingDirectory,
            timeout,
            cancellationToken);
        output.Append(result.StandardOutput);
        errors.Append(result.StandardError);
        if (result.ExitCode != 0)
        {
            throw new DeploymentCommandException(fileName, result.ExitCode);
        }

        return result;
    }

    private static string AppendError(StringBuilder errors, string message)
    {
        if (errors.Length > 0 && errors[^1] != '\n')
        {
            errors.AppendLine();
        }

        errors.Append(message);
        return errors.ToString();
    }

    private static TimeSpan GetRemaining(DateTimeOffset deadline)
    {
        var remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw new ProcessTimedOutException(string.Empty, string.Empty);
        }

        return remaining;
    }
}

public sealed class DeploymentCommandException(string command, int exitCode)
    : Exception($"Command '{command}' exited with code {exitCode}.")
{
    public int ExitCode { get; } = exitCode;
}
