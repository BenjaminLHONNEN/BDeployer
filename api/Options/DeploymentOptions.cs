namespace BDeployer.Api.Options;

public sealed class DeploymentOptions
{
    public const string SectionName = "Deployment";
    public string ProjectsRoot { get; set; } = "/opt/bdeployer/projects";
    public int DefaultTimeoutSeconds { get; set; } = 300;
}
