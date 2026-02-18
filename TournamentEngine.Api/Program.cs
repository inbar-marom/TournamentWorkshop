using TournamentEngine.Api.Services;
using TournamentEngine.Api.Endpoints;
using TournamentEngine.Core.BotLoader;

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Services.AddLogging();

// Add bot storage service
var storageDir = builder.Configuration["BotApi:StorageDirectory"] ?? "bots/";
builder.Services.AddSingleton(sp => 
    new BotStorageService(storageDir, sp.GetRequiredService<ILogger<BotStorageService>>()));

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

public partial class Program { }
