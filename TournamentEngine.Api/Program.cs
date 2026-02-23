using TournamentEngine.Api.Services;
using TournamentEngine.Api.Endpoints;
using TournamentEngine.Core.BotLoader;
using TournamentEngine.Core.Tournament;
using TournamentEngine.Core.Scoring;
using TournamentEngine.Core.GameRunner;
using TournamentEngine.Core.Events;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.Common.Dashboard;

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

// Add tournament engine services
builder.Services.AddSingleton<IGameRunner, GameRunner>();
builder.Services.AddSingleton<IScoringSystem, ScoringSystem>();
builder.Services.AddSingleton<ITournamentEventPublisher, NoOpEventPublisher>();
builder.Services.AddSingleton<ITournamentEngine>(sp =>
    new GroupStageTournamentEngine(
        sp.GetRequiredService<IGameRunner>(),
        sp.GetRequiredService<IScoringSystem>()));
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
        sp.GetRequiredService<BotLoader>()));

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

public class NoOpEventPublisher : ITournamentEventPublisher
{
    public Task PublishMatchCompletedAsync(MatchCompletedDto matchEvent) => Task.CompletedTask;
    public Task PublishStandingsUpdatedAsync(StandingsUpdatedDto standingsEvent) => Task.CompletedTask;
    public Task PublishEventStartedAsync(EventStartedEventDto startEvent) => Task.CompletedTask;
    public Task PublishEventCompletedAsync(EventCompletedEventDto completedEvent) => Task.CompletedTask;
    public Task PublishRoundStartedAsync(RoundStartedDto roundEvent) => Task.CompletedTask;
    public Task UpdateCurrentStateAsync(DashboardStateDto state) => Task.CompletedTask;
    public Task PublishTournamentStartedAsync(TournamentStartedEventDto tournamentEvent) => Task.CompletedTask;
    public Task PublishTournamentProgressUpdatedAsync(TournamentProgressUpdatedEventDto progressEvent) => Task.CompletedTask;
    public Task PublishEventStepCompletedAsync(EventStepCompletedDto completedEvent) => Task.CompletedTask;
    public Task PublishTournamentCompletedAsync(TournamentCompletedEventDto completedEvent) => Task.CompletedTask;
}

public partial class Program { }
