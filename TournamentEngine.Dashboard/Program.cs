using System.Text.Json.Serialization;
using TournamentEngine.Dashboard.Hubs;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Dashboard.Endpoints;
using TournamentEngine.Api.Services;
using TournamentEngine.Core.Common;
using TournamentEngine.Core.BotLoader;

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
builder.Services.AddSingleton(sp => 
    new BotStorageService(botsDir, sp.GetRequiredService<ILogger<BotStorageService>>()));
builder.Services.AddSingleton<IBotLoader>(sp =>
    new BotLoader());
builder.Services.AddScoped(sp =>
    new BotDashboardService(
        sp.GetRequiredService<BotStorageService>(),
        sp.GetRequiredService<IBotLoader>(),
        sp.GetRequiredService<ILogger<BotDashboardService>>()));

// Register dashboard services
builder.Services.AddSingleton<StateManagerService>();
builder.Services.AddSingleton<SignalRTournamentEventPublisher>();
builder.Services.AddSingleton<LeaderboardService>();
builder.Services.AddSingleton<MatchFeedService>();
builder.Services.AddSingleton<TournamentStatusService>();
builder.Services.AddSingleton<RealtimeUIUpdateService>();
builder.Services.AddSingleton<SignalREventPublisher>();

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
builder.WebHost.UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001");

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();
app.MapHub<TournamentHub>("/tournamentHub");
app.MapBotDashboardEndpoints();
Console.WriteLine("🎮 Tournament Dashboard Service started");
Console.WriteLine("📡 SignalR Hub: http://localhost:5000/tournamentHub");
Console.WriteLine("🌐 API: http://localhost:5000/api");
Console.WriteLine("💻 Access from remote: http://<your-ip>:5000");

app.Run();

