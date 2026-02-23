using System.Text.Json.Serialization;
using System.Collections.Generic;
using TournamentEngine.Dashboard.Hubs;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Dashboard.Endpoints;
using TournamentEngine.Api.Services;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.BotLoader;
using TournamentEngine.Core.Events;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Api.Endpoints;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Reduce log noise: keep warnings/errors globally, allow selected app summaries
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("TournamentEngine.Dashboard.Services.StateManagerService", LogLevel.Information);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Configure JSON for minimal API endpoints (Results.Ok, Results.Json, etc.)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddRazorPages();

// Add CORS for remote browser access
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)  // Allow any origin
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register bot dashboard services
var botsDir = builder.Configuration["BotDashboard:BotsDirectory"] ?? "bots/";
var resolvedBotsDir = ResolveBotStorageDirectory(builder.Environment.ContentRootPath, botsDir);
builder.Services.AddSingleton(sp =>
    new BotStorageService(resolvedBotsDir, sp.GetRequiredService<ILogger<BotStorageService>>()));
builder.Services.AddSingleton<IBotLoader>(sp =>
    new BotLoader());
builder.Services.AddSingleton<BotLoader>(sp =>
    new BotLoader());

// Register development settings service for bypass verification feature
builder.Services.AddSingleton<DevelopmentSettingsService>();

// Default tournament config for runtime services (can be extended to read from config later)
var tournamentConfig = new TournamentConfig
{
    ImportTimeout = TimeSpan.FromSeconds(10),
    MoveTimeout = TimeSpan.FromMilliseconds(500), //0.5 seconds for faster feedback in dashboard
    MemoryLimitMB = 512,
    MaxRoundsRPSLS = 10,
    MaxRoundsBlotto = 5,
    MaxRoundsPenaltyKicks = 10,
    MaxRoundsSecurityGame = 5,
    LogLevel = "Information",
    BotsDirectory = resolvedBotsDir,
    ResultsFilePath = Path.Combine(builder.Environment.ContentRootPath, "results", "results.json")
};
builder.Services.AddSingleton(tournamentConfig);

builder.Services.AddSingleton(sp =>
    new BotDashboardService(
        sp.GetRequiredService<BotStorageService>(),
        sp.GetRequiredService<IBotLoader>(),
        sp.GetRequiredService<ILogger<BotDashboardService>>(),
        tournamentConfig));

// Register dashboard services  
builder.Services.AddSingleton<StateManagerService>();
builder.Services.AddSingleton<ITournamentEventPublisher, StateTrackingTournamentEventPublisher>();
builder.Services.AddSingleton<LeaderboardService>();
builder.Services.AddSingleton<MatchFeedService>();
builder.Services.AddSingleton<TournamentStatusService>();
builder.Services.AddSingleton<TournamentManagementService>();
builder.Services.AddSingleton<TournamentEngineQueryService>();

// Register tournament execution services for real management runs
builder.Services.AddSingleton<IGameRunner, GameRunner>();
builder.Services.AddSingleton<IScoringSystem, ScoringSystem>();
builder.Services.AddSingleton<IMatchResultsLogger>(_ =>
{
    // Use HTTP logger to POST results to this dashboard's API
    return new HttpMatchResultsLogger("http://localhost:8080");
});
builder.Services.AddSingleton<ITournamentEngine>(sp =>
    new GroupStageTournamentEngine(
        sp.GetRequiredService<IGameRunner>(),
        sp.GetRequiredService<IScoringSystem>(),
        sp.GetRequiredService<IMatchResultsLogger>()));
builder.Services.AddSingleton<ITournamentManager>(sp =>
    new TournamentManager(
        sp.GetRequiredService<ITournamentEngine>(),
        sp.GetRequiredService<IGameRunner>(),
        sp.GetRequiredService<IScoringSystem>(),
        sp.GetRequiredService<ITournamentEventPublisher>()));
builder.Services.AddSingleton<TournamentSeriesManager>(sp =>
    new TournamentSeriesManager(
        sp.GetRequiredService<ITournamentManager>(),
        sp.GetRequiredService<IScoringSystem>(),
        sp.GetRequiredService<ITournamentEventPublisher>(),
        sp.GetRequiredService<IBotLoader>(),
        sp.GetRequiredService<IMatchResultsLogger>()));

// Register Phase 5 services
builder.Services.AddSingleton<GroupStandingsGridService>();
builder.Services.AddSingleton<ChartsService>();
builder.Services.AddSingleton<MatchDetailsService>();
builder.Services.AddSingleton<TournamentVisualizationService>();

// Register Phase 6 services
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<ExportService>();
builder.Services.AddSingleton<ShareService>();
builder.Services.AddSingleton<NotificationPreferencesService>();
builder.Services.AddSingleton<ResponsiveLayoutService>();
builder.Services.AddSingleton<SeriesDashboardViewService>();

// Configure to listen on all network interfaces for remote access
// Default to HTTP only (no HTTPS certificate required)
if (string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"]))
{
    var host = builder.Configuration.GetValue<string>("DashboardSettings:Host") ?? "0.0.0.0";
    var port = builder.Configuration.GetValue<string>("DashboardSettings:Port") ?? "8080";
    builder.WebHost.UseUrls($"http://{host}:{port}");
}

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();
app.MapRazorPages();
app.MapBotDashboardEndpoints();
app.MapManagementEndpoints();
app.MapTournamentEndpoints();  // Tournament data query endpoints
app.MapTournamentConfigEndpoints();  // Tournament configuration endpoints
app.MapTournamentQueryEndpoints();  // Tournament engine query endpoints (live tournament data)
app.MapBotEndpoints();  // Import API endpoints for bot submission
app.MapResourceEndpoints();  // Import resource download endpoints
var dashHost = app.Configuration.GetValue<string>("DashboardSettings:Host") ?? "0.0.0.0";
var dashPort = app.Configuration.GetValue<string>("DashboardSettings:Port") ?? "8080";
Console.WriteLine("🎮 Tournament Dashboard Service started");
Console.WriteLine($"🌐 API: http://localhost:{dashPort}/api");
Console.WriteLine($"💻 Access from remote: http://<your-ip>:{dashPort}");

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

    // Prefer legacyPath (Dashboard project's own bots folder) over sharedPath (solution root)
    // This ensures we use valid bots with .cs source files when both directories exist
    if (Directory.Exists(legacyPath))
    {
        return legacyPath;
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


