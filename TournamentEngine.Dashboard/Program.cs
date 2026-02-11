using TournamentEngine.Dashboard.Hubs;
using TournamentEngine.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSignalR();
builder.Services.AddControllers();

// Add CORS for remote browser access
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register dashboard services
builder.Services.AddSingleton<StateManagerService>();

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

app.MapGet("/", () => "Tournament Dashboard Service is running. Connect to /tournamentHub for real-time updates.");

Console.WriteLine("🎮 Tournament Dashboard Service started");
Console.WriteLine("📡 SignalR Hub: http://localhost:5000/tournamentHub");
Console.WriteLine("🌐 API: http://localhost:5000/api");
Console.WriteLine("💻 Access from remote: http://<your-ip>:5000");

app.Run();

