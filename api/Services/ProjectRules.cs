using System.Text.RegularExpressions;
using BDeployer.Api.Models;

namespace BDeployer.Api.Services;

public static partial class ProjectRules
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentNameRegex();

    public static string ValidateEnvironmentName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        if (normalized.Length is < 1 or > 100 || !EnvironmentNameRegex().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Environment name must contain only lowercase letters, numbers and single hyphens.",
                nameof(name));
        }

        return normalized;
    }

    public static string GetWorkingDirectory(string root, Guid projectId, string environmentName)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var path = Path.GetFullPath(Path.Combine(normalizedRoot, projectId.ToString("D"), environmentName));
        var expectedPrefix = normalizedRoot + Path.DirectorySeparatorChar;

        if (!path.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved project path is outside the configured projects root.");
        }

        return path;
    }

    public static void ValidateProject(string name, string gitUrl)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new ArgumentException("Project name is required and must not exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(gitUrl) || gitUrl.Trim().Length > 2000)
        {
            throw new ArgumentException("Git URL is required and must not exceed 2000 characters.");
        }

        if (gitUrl.Any(char.IsControl))
        {
            throw new ArgumentException("Git URL contains invalid characters.");
        }
    }

    public static void ValidateEnvironment(string script, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException("Deployment script is required.");
        }

        if (timeoutSeconds is < 1 or > 86400)
        {
            throw new ArgumentException("Timeout must be between 1 and 86400 seconds.");
        }
    }
}
