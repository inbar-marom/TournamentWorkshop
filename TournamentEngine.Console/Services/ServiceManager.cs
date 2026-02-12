namespace TournamentEngine.Console.Services;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using TournamentEngine.Console.Configuration;

/// <summary>
/// Manages the lifecycle of the Dashboard service including startup, health checks, and shutdown
/// </summary>
public class ServiceManager
{
    private readonly ILogger<ServiceManager> _logger;
    private readonly TournamentConfiguration _config;
    private Process? _dashboardProcess;
    private bool _isDisposed;
    private const int HealthCheckIntervalMs = 5000;
    private const int HealthCheckTimeoutMs = 10000;

    public ServiceManager(ILogger<ServiceManager> logger, TournamentConfiguration config)
    {
        _logger = logger;
        _config = config;
        _isDisposed = false;
    }

    /// <summary>
    /// Start the Dashboard service as a background process
    /// </summary>
    public async Task<bool> StartDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (_config.TournamentEngine?.EnableDashboard != true)
        {
            _logger.LogInformation("Dashboard service disabled in configuration");
            return true;
        }

        try
        {
            _logger.LogInformation("Starting Dashboard service on port {Port}...", _config.TournamentEngine.DashboardPort);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project TournamentEngine.Dashboard --nologo",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            _dashboardProcess = Process.Start(processStartInfo);
            if (_dashboardProcess == null)
            {
                _logger.LogError("Failed to start Dashboard process");
                return false;
            }

            _logger.LogInformation("Dashboard process started (PID: {ProcessId})", _dashboardProcess.Id);

            // Wait for dashboard to be ready
            bool isReady = await WaitForDashboardReadyAsync(cancellationToken);
            if (!isReady)
            {
                _logger.LogWarning("Dashboard did not respond within timeout period");
                return false;
            }

            _logger.LogInformation("Dashboard service is ready and responding");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Dashboard service");
            return false;
        }
    }

    /// <summary>
    /// Wait for Dashboard to be ready by attempting to connect to its SignalR hub
    /// </summary>
    private async Task<bool> WaitForDashboardReadyAsync(CancellationToken cancellationToken)
    {
        var dashboardUrl = _config.TournamentEngine?.DashboardUrl ?? "http://localhost:5000/tournamentHub";
        var maxAttempts = 30; // 30 seconds at 1 second intervals
        var attemptCount = 0;

        while (attemptCount < maxAttempts && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var connection = new HubConnectionBuilder()
                    .WithUrl(dashboardUrl)
                    .WithAutomaticReconnect()
                    .Build();

                await connection.StartAsync(cancellationToken);
                await connection.StopAsync(cancellationToken);
                
                _logger.LogInformation("Dashboard hub is accessible");
                return true;
            }
            catch (Exception ex)
            {
                attemptCount++;
                if (attemptCount < maxAttempts)
                {
                    _logger.LogDebug("Dashboard not ready yet (attempt {Attempt}/{MaxAttempts}): {Message}", 
                        attemptCount, maxAttempts, ex.Message);
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Perform a health check on the Dashboard service
    /// </summary>
    public async Task<bool> CheckDashboardHealthAsync(CancellationToken cancellationToken = default)
    {
        if (_dashboardProcess == null || _dashboardProcess.HasExited)
        {
            _logger.LogWarning("Dashboard process is not running");
            return false;
        }

        try
        {
            var dashboardUrl = _config.TournamentEngine?.DashboardUrl ?? "http://localhost:5000/tournamentHub";
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(HealthCheckTimeoutMs);

            var connection = new HubConnectionBuilder()
                .WithUrl(dashboardUrl)
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync(cts.Token);
            await connection.StopAsync(cts.Token);

            _logger.LogDebug("Dashboard health check passed");
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Dashboard health check timed out");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard health check failed");
            return false;
        }
    }

    /// <summary>
    /// Continuously monitor Dashboard health in background
    /// </summary>
    public async Task MonitorHealthAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _dashboardProcess != null && !_dashboardProcess.HasExited)
        {
            try
            {
                var isHealthy = await CheckDashboardHealthAsync(cancellationToken);
                if (!isHealthy)
                {
                    _logger.LogWarning("Dashboard health check failed - service may be unresponsive");
                }

                await Task.Delay(HealthCheckIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health monitoring");
                await Task.Delay(HealthCheckIntervalMs, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Gracefully shutdown the Dashboard service
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (_dashboardProcess == null || _dashboardProcess.HasExited)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Shutting down Dashboard service (PID: {ProcessId})", _dashboardProcess.Id);

            _dashboardProcess.Kill(entireProcessTree: true);
            var timeout = TimeSpan.FromSeconds(5);
            
            if (!_dashboardProcess.WaitForExit((int)timeout.TotalMilliseconds))
            {
                _logger.LogWarning("Dashboard process did not exit gracefully within timeout");
                _dashboardProcess.Kill();
            }

            _logger.LogInformation("Dashboard service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error shutting down Dashboard service");
        }
        finally
        {
            _dashboardProcess?.Dispose();
            _dashboardProcess = null;
        }
    }

    /// <summary>
    /// Get the status of the Dashboard service
    /// </summary>
    public ServiceStatus GetStatus()
    {
        if (_dashboardProcess == null)
        {
            return ServiceStatus.NotStarted;
        }

        if (_dashboardProcess.HasExited)
        {
            return ServiceStatus.Stopped;
        }

        return ServiceStatus.Running;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disposal");
        }

        _isDisposed = true;
    }
}

/// <summary>
/// Represents the current status of a service
/// </summary>
public enum ServiceStatus
{
    NotStarted,
    Running,
    Stopped,
    Error
}
