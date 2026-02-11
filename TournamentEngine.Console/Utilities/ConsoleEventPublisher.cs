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
    private bool _isConnected = false;

    /// <summary>
    /// Create event publisher with dashboard connection
    /// </summary>
    public ConsoleEventPublisher(string dashboardUrl, ILogger<ConsoleEventPublisher>? logger = null)
    {
        _logger = logger;
        
        if (!string.IsNullOrEmpty(dashboardUrl))
        {
            try
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(dashboardUrl)
                    .WithAutomaticReconnect()
                    .Build();

                // Start connection asynchronously (fire and forget)
                _ = StartConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to initialize hub connection to {DashboardUrl}", dashboardUrl);
                _hubConnection = null;
            }
        }
    }

    private async Task StartConnectionAsync()
    {
        if (_hubConnection == null) return;

        try
        {
            await _hubConnection.StartAsync();
            _isConnected = true;
            _logger?.LogInformation("Connected to dashboard hub");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not connect to dashboard - events will not be published");
            _isConnected = false;
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

    private async Task PublishEventAsync<T>(string eventName, T eventData)
    {
        if (_hubConnection == null || !_isConnected)
        {
            return; // Silently skip if not connected
        }

        try
        {
            await _hubConnection.SendAsync(eventName, eventData);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to publish {EventName} - dashboard may not be available", eventName);
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
