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

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton(sp =>
    new BotDashboardService(
        sp.GetRequiredService<BotStorageService>(),
        sp.GetRequiredService<IBotLoader>(),
        sp.GetRequiredService<ILogger<BotDashboardService>>()));

// Default tournament config for runtime services (can be extended to read from config later)
var tournamentConfig = new TournamentConfig
{
    Games = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto, GameType.PenaltyKicks, GameType.SecurityGame },
    ImportTimeout = TimeSpan.FromSeconds(10),
    MoveTimeout = TimeSpan.FromSeconds(2),
    MaxParallelMatches = 1,
    MemoryLimitMB = 512,
    MaxRoundsRPSLS = 50,
    LogLevel = "Information",
    BotsDirectory = resolvedBotsDir,
    ResultsFilePath = Path.Combine(builder.Environment.ContentRootPath, "results", "results.json")
};
builder.Services.AddSingleton(tournamentConfig);

// Register dashboard services
builder.Services.AddSingleton<StateManagerService>();
builder.Services.AddSingleton<SignalRTournamentEventPublisher>();
builder.Services.AddSingleton<ITournamentEventPublisher, SignalRTournamentEventPublisher>();
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
builder.Services.AddSingleton<ITournamentManager, TournamentManager>();
builder.Services.AddSingleton<TournamentSeriesManager>();

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
if (string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001");
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
Console.WriteLine("🎮 Tournament Dashboard Service started");
Console.WriteLine("📡 SignalR Hub: http://localhost:5000/tournamentHub");
Console.WriteLine("🌐 API: http://localhost:5000/api");
Console.WriteLine("💻 Access from remote: http://<your-ip>:5000");

app.Run();

