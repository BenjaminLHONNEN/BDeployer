using BDeployer.Api.Data;
using BDeployer.Api.Middleware;
using BDeployer.Api.Options;
using BDeployer.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddOptions<ApiKeyOptions>()
    .BindConfiguration(ApiKeyOptions.SectionName)
    .Validate(x => !string.IsNullOrWhiteSpace(x.Key), "BDEPLOYER_API_KEY is required.")
    .ValidateOnStart();
builder.Services.AddOptions<DeploymentOptions>()
    .BindConfiguration(DeploymentOptions.SectionName)
    .Validate(x => Path.IsPathFullyQualified(x.ProjectsRoot), "ProjectsRoot must be an absolute path.")
    .ValidateOnStart();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<DeploymentLock>();
builder.Services.AddScoped<DeploymentService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHealthChecks("/health");
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers();

app.Run();
