using System.Text.Json.Serialization;
using System.Collections.Generic;
using TournamentEngine.Dashboard.Hubs;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Dashboard.Endpoints;
using TournamentEngine.Api.Services;
using TournamentEngine.Core.Common;
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
builder.Logging.AddFilter("TournamentEngine.Dashboard.Hubs.TournamentHub", LogLevel.Warning);
builder.Logging.AddFilter("TournamentEngine.Dashboard.Services.BotDashboardService", LogLevel.Warning);
builder.Logging.AddFilter("TournamentEngine.Dashboard.Services.StateManagerService", LogLevel.Information);
builder.Logging.AddFilter("TournamentEngine.Dashboard.Services.SignalRTournamentEventPublisher", LogLevel.Information);

// Add services to the container
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddRazorPages();

// Add CORS for remote browser access and SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)  // Allow any origin
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // Required for SignalR
    });
});

// Register bot dashboard services
var botsDir = builder.Configuration["BotDashboard:BotsDirectory"] ?? "bots/";
var resolvedBotsDir = Path.IsPathRooted(botsDir)
    ? botsDir
    : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, botsDir));
builder.Services.AddSingleton(sp =>
    new BotStorageService(resolvedBotsDir, sp.GetRequiredService<ILogger<BotStorageService>>()));
builder.Services.AddSingleton<IBotLoader>(sp =>
    new BotLoader());

// Default tournament config for runtime services (can be extended to read from config later)
var tournamentConfig = new TournamentConfig
{
    ImportTimeout = TimeSpan.FromSeconds(10),
    MoveTimeout = TimeSpan.FromMilliseconds(500), //0.5 seconds for faster feedback in dashboard
    MemoryLimitMB = 512,
    MaxRoundsRPSLS = 50,
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
builder.Services.AddSingleton<SignalRTournamentEventPublisher>();
builder.Services.AddSingleton<ITournamentEventPublisher>(sp => sp.GetRequiredService<SignalRTournamentEventPublisher>());
builder.Services.AddSingleton<LeaderboardService>();
builder.Services.AddSingleton<MatchFeedService>();
builder.Services.AddSingleton<TournamentStatusService>();
builder.Services.AddSingleton<RealtimeUIUpdateService>();
builder.Services.AddSingleton<SignalREventPublisher>();
builder.Services.AddSingleton<TournamentManagementService>();

// Register tournament execution services for real management runs
builder.Services.AddSingleton<IGameRunner, GameRunner>();
builder.Services.AddSingleton<IScoringSystem, ScoringSystem>();
builder.Services.AddSingleton<ITournamentEngine, GroupStageTournamentEngine>();
builder.Services.AddSingleton<ITournamentManager>(sp =>
    new TournamentManager(
        sp.GetRequiredService<ITournamentEngine>(),
        sp.GetRequiredService<IGameRunner>(),
        sp.GetRequiredService<ITournamentEventPublisher>()));
builder.Services.AddSingleton<TournamentSeriesManager>(sp =>
    new TournamentSeriesManager(
        sp.GetRequiredService<ITournamentManager>(),
        sp.GetRequiredService<IScoringSystem>(),
        sp.GetRequiredService<ITournamentEventPublisher>(),
        sp.GetRequiredService<IBotLoader>()));

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
    builder.WebHost.UseUrls("http://0.0.0.0:8080");
}

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();
app.MapRazorPages();
app.MapHub<TournamentHub>("/tournamentHub");
app.MapBotDashboardEndpoints();
app.MapManagementEndpoints();
app.MapBotEndpoints();  // Import API endpoints for bot submission
app.MapResourceEndpoints();  // Import resource download endpoints
Console.WriteLine("🎮 Tournament Dashboard Service started");
Console.WriteLine("📡 SignalR Hub: http://localhost:8080/tournamentHub");
Console.WriteLine("🌐 API: http://localhost:8080/api");
Console.WriteLine("💻 Access from remote: http://<your-ip>:8080");

app.Run();

