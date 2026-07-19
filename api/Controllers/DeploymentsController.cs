using BDeployer.Api.Contracts;
using BDeployer.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BDeployer.Api.Controllers;

[ApiController]
[Route("deployments")]
public sealed class DeploymentsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("{deploymentId:guid}")]
    public async Task<ActionResult<DeploymentResponse>> Get(
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        var deployment = await dbContext.Deployments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == deploymentId, cancellationToken);
        return deployment is null ? NotFound() : Ok(deployment.ToResponse());
    }
}
