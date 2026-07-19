using BDeployer.Api.Contracts;
using BDeployer.Api.Data;
using BDeployer.Api.Models;
using BDeployer.Api.Options;
using BDeployer.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BDeployer.Api.Controllers;

[ApiController]
[Route("projects/{projectId:guid}/environments")]
public sealed class EnvironmentsController(
    AppDbContext dbContext,
    IOptions<DeploymentOptions> options,
    DeploymentService deploymentService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EnvironmentResponse>> Create(
        Guid projectId,
        CreateEnvironmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await dbContext.Projects.AnyAsync(x => x.Id == projectId, cancellationToken))
            {
                return NotFound();
            }

            var name = ProjectRules.ValidateEnvironmentName(request.Name);
            var timeout = request.TimeoutSeconds ?? options.Value.DefaultTimeoutSeconds;
            ProjectRules.ValidateEnvironment(request.DeploymentScript, timeout);

            if (await dbContext.ProjectEnvironments.AnyAsync(
                    x => x.ProjectId == projectId && x.Name == name,
                    cancellationToken))
            {
                return Conflict(new { error = $"Environment '{name}' already exists." });
            }

            var environment = new ProjectEnvironment
            {
                ProjectId = projectId,
                Name = name,
                WorkingDirectory = ProjectRules.GetWorkingDirectory(options.Value.ProjectsRoot, projectId, name),
                DeploymentScript = request.DeploymentScript,
                TimeoutSeconds = timeout,
                Enabled = request.Enabled
            };

            dbContext.ProjectEnvironments.Add(environment);
            await dbContext.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(
                nameof(Get),
                new { projectId, environmentId = environment.Id },
                environment.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception.Message);
        }
    }

    [HttpGet("{environmentId:guid}")]
    public async Task<ActionResult<EnvironmentResponse>> Get(
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        var environment = await dbContext.ProjectEnvironments.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == environmentId && x.ProjectId == projectId,
            cancellationToken);
        return environment is null ? NotFound() : Ok(environment.ToResponse());
    }

    [HttpPut("{environmentId:guid}")]
    public async Task<ActionResult<EnvironmentResponse>> Update(
        Guid projectId,
        Guid environmentId,
        UpdateEnvironmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var environment = await dbContext.ProjectEnvironments.SingleOrDefaultAsync(
                x => x.Id == environmentId && x.ProjectId == projectId,
                cancellationToken);
            if (environment is null)
            {
                return NotFound();
            }

            var name = ProjectRules.ValidateEnvironmentName(request.Name);
            ProjectRules.ValidateEnvironment(request.DeploymentScript, request.TimeoutSeconds);
            if (await dbContext.ProjectEnvironments.AnyAsync(
                    x => x.ProjectId == projectId && x.Id != environmentId && x.Name == name,
                    cancellationToken))
            {
                return Conflict(new { error = $"Environment '{name}' already exists." });
            }

            environment.Name = name;
            environment.WorkingDirectory = ProjectRules.GetWorkingDirectory(
                options.Value.ProjectsRoot,
                projectId,
                name);
            environment.DeploymentScript = request.DeploymentScript;
            environment.TimeoutSeconds = request.TimeoutSeconds;
            environment.Enabled = request.Enabled;
            environment.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(environment.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception.Message);
        }
    }

    [HttpDelete("{environmentId:guid}")]
    public async Task<IActionResult> Delete(
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        var environment = await dbContext.ProjectEnvironments.SingleOrDefaultAsync(
            x => x.Id == environmentId && x.ProjectId == projectId,
            cancellationToken);
        if (environment is null)
        {
            return NotFound();
        }

        dbContext.ProjectEnvironments.Remove(environment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{environmentId:guid}/deploy")]
    public async Task<ActionResult<DeploymentResponse>> Deploy(
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await deploymentService.DeployAsync(projectId, environmentId, cancellationToken);
            return result.Status switch
            {
                DeploymentStatus.Succeeded => Ok(result.ToResponse()),
                DeploymentStatus.TimedOut => StatusCode(StatusCodes.Status408RequestTimeout, result.ToResponse()),
                _ => StatusCode(StatusCodes.Status500InternalServerError, result.ToResponse())
            };
        }
        catch (DeploymentAlreadyRunningException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return UnprocessableEntity(new { error = exception.Message });
        }
    }

    [HttpGet("{environmentId:guid}/deployments")]
    public async Task<ActionResult<IReadOnlyCollection<DeploymentResponse>>> ListDeployments(
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.ProjectEnvironments.AnyAsync(
                x => x.Id == environmentId && x.ProjectId == projectId,
                cancellationToken))
        {
            return NotFound();
        }

        var deployments = await dbContext.Deployments
            .AsNoTracking()
            .Where(x => x.EnvironmentId == environmentId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return Ok(deployments.Select(x => x.ToResponse()));
    }
}
