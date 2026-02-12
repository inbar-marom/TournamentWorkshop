namespace TournamentEngine.Console.Utilities;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using TournamentEngine.Core.Common.Dashboard;
using TournamentEngine.Core.Events;

/// <summary>
/// SignalR-based event publisher for console application.
/// Connects to dashboard hub and publishes tournament events in real-time.
/// Gracefully handles connection failures when dashboard is not available.
/// </summary>
public class ConsoleEventPublisher : ITournamentEventPublisher, IAsyncDisposable
{
    private readonly HubConnection? _hubConnection;
    private readonly ILogger<ConsoleEventPublisher>? _logger;
    private Task<bool>? _connectionTask;
    private bool _isConnected = false;

    /// <summary>
    /// Create event publisher with dashboard connection
    /// </summary>
    public ConsoleEventPublisher(string dashboardUrl, ILogger<ConsoleEventPublisher>? logger = null)
    {
        _logger = logger;
        _logger?.LogInformation("ConsoleEventPublisher initializing with URL: {DashboardUrl}", dashboardUrl);
        
        if (!string.IsNullOrEmpty(dashboardUrl))
        {
            try
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(dashboardUrl)
                    .WithAutomaticReconnect()
                    .Build();

                _logger?.LogInformation("HubConnection created, starting connection...");
                // Start connection asynchronously and store the task
                _connectionTask = StartConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize hub connection to {DashboardUrl}", dashboardUrl);
                _hubConnection = null;
            }
        }
        else
        {
            _logger?.LogWarning("Dashboard URL is null or empty - event publishing disabled");
        }
    }

    /// <summary>
    /// Ensure connection is established (call this before publishing events)
    /// </summary>
    public async Task<bool> EnsureConnectedAsync()
    {
        if (_connectionTask != null)
        {
            return await _connectionTask;
        }
        return false;
    }

    private async Task<bool> StartConnectionAsync()
    {
        if (_hubConnection == null) return false;

        try
        {
            _logger?.LogInformation("Attempting to connect to dashboard hub...");
            await _hubConnection.StartAsync();
            _isConnected = true;
            _logger?.LogInformation("✅ Connected to dashboard hub successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Could not connect to dashboard - events will not be published");
            _isConnected = false;
            return false;
        }
    }

    public async Task PublishMatchCompletedAsync(MatchCompletedDto matchEvent)
    {
        await PublishEventAsync("MatchCompleted", matchEvent);
    }

    public async Task PublishStandingsUpdatedAsync(StandingsUpdatedDto standingsEvent)
    {
        await PublishEventAsync("StandingsUpdated", standingsEvent);
    }

    public async Task PublishTournamentStartedAsync(TournamentStartedDto startEvent)
    {
        await PublishEventAsync("TournamentStarted", startEvent);
    }

    public async Task PublishTournamentCompletedAsync(TournamentCompletedDto completedEvent)
    {
        await PublishEventAsync("TournamentCompleted", completedEvent);
    }

    public async Task PublishRoundStartedAsync(RoundStartedDto roundEvent)
    {
        await PublishEventAsync("RoundStarted", roundEvent);
    }

    public async Task UpdateCurrentStateAsync(TournamentStateDto state)
    {
        await PublishEventAsync("CurrentState", state);
    }

    public async Task PublishSeriesStartedAsync(SeriesStartedDto seriesEvent)
    {
        await PublishEventAsync("SeriesStarted", seriesEvent);
    }

    public async Task PublishSeriesProgressUpdatedAsync(SeriesProgressUpdatedDto progressEvent)
    {
        await PublishEventAsync("SeriesProgressUpdated", progressEvent);
    }

    public async Task PublishSeriesStepCompletedAsync(SeriesStepCompletedDto completedEvent)
    {
        await PublishEventAsync("SeriesStepCompleted", completedEvent);
    }

    public async Task PublishSeriesCompletedAsync(SeriesCompletedDto completedEvent)
    {
        await PublishEventAsync("SeriesCompleted", completedEvent);
    }

    private async Task PublishEventAsync<T>(string eventName, T eventData)
    {
        if (_hubConnection == null || !_isConnected)
        {_logger?.LogDebug("Skipping event {EventName} - not connected to dashboard", eventName);
            return; // Silently skip if not connected
        }

        try
        {
            _logger?.LogDebug("Publishing event {EventName} to dashboard", eventName);
            await _hubConnection.SendAsync(eventName, eventData);
            _logger?.LogDebug("Successfully published {EventName}", eventName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to publish {EventName} - dashboard may not be available", eventName);
            // Don't throw - gracefully continue even if dashboard is down
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error disposing hub connection");
            }
        }
    }
}
