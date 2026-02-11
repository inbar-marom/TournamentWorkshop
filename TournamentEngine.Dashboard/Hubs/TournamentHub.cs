namespace TournamentEngine.Dashboard.Hubs;

using Microsoft.AspNetCore.SignalR;
using TournamentEngine.Dashboard.Services;
using TournamentEngine.Core.Common.Dashboard;

/// <summary>
/// SignalR hub for real-time tournament updates.
/// Broadcasts tournament state changes to connected browser clients.
/// </summary>
public class TournamentHub : Hub
{
    private readonly StateManagerService _stateManager;
    private readonly ILogger<TournamentHub> _logger;

    public TournamentHub(StateManagerService stateManager, ILogger<TournamentHub> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        
        // Add client to the viewers group
        await Groups.AddToGroupAsync(Context.ConnectionId, "TournamentViewers");
        
        // Send current state immediately to new client
        var currentState = await _stateManager.GetCurrentStateAsync();
        await Clients.Caller.SendAsync("CurrentState", currentState);
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client subscribes to tournament updates.
    /// </summary>
    public async Task SubscribeToUpdates()
    {
        _logger.LogInformation("Client {ConnectionId} subscribed to tournament updates", Context.ConnectionId);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, "TournamentViewers");
        
        // Send acknowledgment with current state
        var currentState = await _stateManager.GetCurrentStateAsync();
        await Clients.Caller.SendAsync("SubscriptionConfirmed", new
        {
            Message = "Successfully subscribed to tournament updates",
            Timestamp = DateTime.UtcNow,
            CurrentState = currentState
        });
    }

    /// <summary>
    /// Client requests current tournament state snapshot.
    /// </summary>
    public async Task GetCurrentState()
    {
        _logger.LogInformation("Client {ConnectionId} requested current state", Context.ConnectionId);
        
        var state = await _stateManager.GetCurrentStateAsync();
        await Clients.Caller.SendAsync("CurrentState", state);
    }

    /// <summary>
    /// Client requests recent matches.
    /// </summary>
    public async Task GetRecentMatches(int count = 20)
    {
        _logger.LogInformation("Client {ConnectionId} requested recent matches", Context.ConnectionId);
        
        var matches = _stateManager.GetRecentMatches(count);
        await Clients.Caller.SendAsync("RecentMatches", matches);
    }

    /// <summary>
    /// Ping endpoint for connection testing.
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }
}
