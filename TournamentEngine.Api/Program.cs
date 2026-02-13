using TournamentEngine.Api.Models;
using TournamentEngine.Api.Services;

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

// Bot Endpoints
var botGroup = app.MapGroup("/api/bots")
    .WithName("Bots");

botGroup.MapPost("/submit", SubmitBot)
    .WithName("SubmitBot")
    .WithDescription("Submit a single bot");

botGroup.MapPost("/submit-batch", SubmitBatch)
    .WithName("SubmitBatch")
    .WithDescription("Submit multiple bots at once");

botGroup.MapGet("/list", ListBots)
    .WithName("ListBots")
    .WithDescription("List all submitted bots");

botGroup.MapDelete("/{teamName}", DeleteBot)
    .WithName("DeleteBot")
    .WithDescription("Delete a submitted bot");

app.Run();

// Endpoint handlers
async Task<IResult> SubmitBot(BotStorageService storage, BotSubmissionRequest request)
{
    var result = await storage.StoreBotAsync(request);
    return result.Success 
        ? Results.Ok(result)
        : Results.BadRequest(result);
}

async Task<IResult> SubmitBatch(BotStorageService storage, List<BotSubmissionRequest> requests)
{
    var response = new BatchSubmissionResponse();

    foreach (var request in requests ?? new List<BotSubmissionRequest>())
    {
        var result = await storage.StoreBotAsync(request);
        response.Results.Add(result);
        
        if (result.Success)
            response.SuccessCount++;
        else
            response.FailureCount++;
    }

    return Results.Ok(response);
}

IResult ListBots(BotStorageService storage)
{
    var submissions = storage.GetAllSubmissions();
    return Results.Ok(new ListBotsResponse { Bots = submissions });
}

async Task<IResult> DeleteBot(BotStorageService storage, string teamName)
{
    var success = await storage.DeleteBotAsync(teamName);
    return success
        ? Results.Ok(new { success = true, message = $"Bot {teamName} deleted" })
        : Results.NotFound(new { success = false, message = $"Bot {teamName} not found" });
}
