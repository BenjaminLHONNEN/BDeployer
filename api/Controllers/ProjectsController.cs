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
[Route("projects")]
public sealed class ProjectsController(AppDbContext dbContext, IOptions<DeploymentOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ProjectResponse>>> List(CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects
            .AsNoTracking()
            .Include(x => x.Environments)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(projects.Select(x => x.ToResponse()));
    }

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectResponse>> Get(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Include(x => x.Environments)
            .SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        return project is null ? NotFound() : Ok(project.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> Create(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            ProjectRules.ValidateProject(request.Name, request.GitUrl);
            var project = new Project
            {
                Name = request.Name.Trim(),
                GitUrl = request.GitUrl.Trim(),
                Enabled = request.Enabled
            };

            var environmentNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in request.Environments ?? [])
            {
                var environment = CreateEnvironment(project.Id, item);
                if (!environmentNames.Add(environment.Name))
                {
                    return ValidationProblem($"Environment '{environment.Name}' is duplicated.");
                }

                project.Environments.Add(environment);
            }

            dbContext.Projects.Add(project);
            await dbContext.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(Get), new { projectId = project.Id }, project.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception.Message);
        }
    }

    [HttpPut("{projectId:guid}")]
    public async Task<ActionResult<ProjectResponse>> Update(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            ProjectRules.ValidateProject(request.Name, request.GitUrl);
            var project = await dbContext.Projects
                .Include(x => x.Environments)
                .SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken);
            if (project is null)
            {
                return NotFound();
            }

            project.Name = request.Name.Trim();
            project.GitUrl = request.GitUrl.Trim();
            project.Enabled = request.Enabled;
            project.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(project.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception.Message);
        }
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.FindAsync([projectId], cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private ProjectEnvironment CreateEnvironment(Guid projectId, CreateEnvironmentRequest request)
    {
        var name = ProjectRules.ValidateEnvironmentName(request.Name);
        var timeout = request.TimeoutSeconds ?? options.Value.DefaultTimeoutSeconds;
        ProjectRules.ValidateEnvironment(request.DeploymentScript, timeout);

        return new ProjectEnvironment
        {
            ProjectId = projectId,
            Name = name,
            WorkingDirectory = ProjectRules.GetWorkingDirectory(options.Value.ProjectsRoot, projectId, name),
            DeploymentScript = request.DeploymentScript,
            TimeoutSeconds = timeout,
            Enabled = request.Enabled
        };
    }
}
