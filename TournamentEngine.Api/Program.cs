using TournamentEngine.Api.Services;
using TournamentEngine.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Services.AddLogging();

// Add bot storage service
var storageDir = builder.Configuration["BotApi:StorageDirectory"] ?? "bots/";
builder.Services.AddSingleton(sp => 
    new BotStorageService(storageDir, sp.GetRequiredService<ILogger<BotStorageService>>()));

var app = builder.Build();

// Configure middleware
app.UseHttpsRedirection();

// Map bot endpoints
app.MapBotEndpoints();

app.Run();

public partial class Program { }
