using System.Security.Cryptography;
using System.Text;
using BDeployer.Api.Options;
using Microsoft.Extensions.Options;

namespace BDeployer.Api.Middleware;

public sealed class ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options)
{
    private const string HeaderName = "X-API-Key";
    private readonly byte[] _expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.Key));

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        var providedKey = context.Request.Headers[HeaderName].FirstOrDefault();
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey ?? string.Empty));

        if (!CryptographicOperations.FixedTimeEquals(_expectedHash, providedHash))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing API key." });
            return;
        }

        await next(context);
    }
}
