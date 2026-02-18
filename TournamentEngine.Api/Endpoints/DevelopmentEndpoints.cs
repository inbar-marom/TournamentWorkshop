using Microsoft.AspNetCore.Mvc;
using TournamentEngine.Api.Services;

namespace TournamentEngine.Api.Endpoints;

public static class DevelopmentEndpoints
{
    public static void MapDevelopmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dev");

        group.MapPost("/bypass-verification", SetVerificationBypass)
            .WithName("SetVerificationBypass")
            .WithTags("Development");

        group.MapGet("/bypass-verification", GetVerificationBypassStatus)
            .WithName("GetVerificationBypassStatus")
            .WithTags("Development");
    }

    private static IResult SetVerificationBypass(
        [FromBody] BypassRequest request,
        DevelopmentSettingsService devSettings,
        ILogger<Program> logger)
    {
        devSettings.SetVerificationBypass(request.Enabled);
        
        logger.LogWarning("Verification bypass {Status} - All bots will {Action}",
            request.Enabled ? "ENABLED" : "DISABLED",
            request.Enabled ? "be accepted without validation" : "be validated normally");

        var status = devSettings.GetStatus();
        return Results.Ok(new
        {
            success = true,
            verificationBypassed = status.VerificationBypassed,
            message = request.Enabled 
                ? "⚠️ Verification bypass ENABLED - All bot submissions will be accepted without validation!"
                : "✓ Verification bypass DISABLED - Normal validation will be performed",
            warning = status.Warning
        });
    }

    private static IResult GetVerificationBypassStatus(
        DevelopmentSettingsService devSettings)
    {
        var status = devSettings.GetStatus();
        return Results.Ok(new
        {
            verificationBypassed = status.VerificationBypassed,
            warning = status.Warning
        });
    }
}

public record BypassRequest(bool Enabled);
