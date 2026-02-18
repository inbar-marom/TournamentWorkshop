using TournamentEngine.Api.Services;
using TournamentEngine.Api.Endpoints;
using TournamentEngine.Core.BotLoader;

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Services.AddLogging();

// Add bot storage service
var storageDir = builder.Configuration["BotApi:StorageDirectory"] ?? "bots/";
var resolvedStorageDir = ResolveBotStorageDirectory(builder.Environment.ContentRootPath, storageDir);
builder.Services.AddSingleton(sp => 
    new BotStorageService(resolvedStorageDir, sp.GetRequiredService<ILogger<BotStorageService>>()));

// Add development settings service
builder.Services.AddSingleton<DevelopmentSettingsService>();

// Add bot loader
builder.Services.AddSingleton<BotLoader>(sp => new BotLoader());

var app = builder.Build();

// Configure middleware
app.UseHttpsRedirection();

// Map bot endpoints
app.MapBotEndpoints();

// Map resource endpoints
app.MapResourceEndpoints();

// Map development endpoints
app.MapDevelopmentEndpoints();

app.Run();

static string ResolveBotStorageDirectory(string contentRootPath, string configuredPath)
{
    if (Path.IsPathRooted(configuredPath))
    {
        return configuredPath;
    }

    var legacyPath = Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    var solutionRoot = FindSolutionRoot(contentRootPath);
    var sharedPath = Path.GetFullPath(Path.Combine(solutionRoot ?? contentRootPath, configuredPath));

    if (Directory.Exists(legacyPath) && Directory.Exists(sharedPath))
    {
        var legacyHasSubmissions = Directory.GetDirectories(legacyPath).Any();
        var sharedHasSubmissions = Directory.GetDirectories(sharedPath).Any();

        if (legacyHasSubmissions && !sharedHasSubmissions)
        {
            return legacyPath;
        }
    }

    return sharedPath;
}

static string? FindSolutionRoot(string startPath)
{
    var current = new DirectoryInfo(startPath);

    while (current != null)
    {
        var solutionPath = Path.Combine(current.FullName, "TournamentEngine.sln");
        if (File.Exists(solutionPath))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return null;
}

public partial class Program { }
