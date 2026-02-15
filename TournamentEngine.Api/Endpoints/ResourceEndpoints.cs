namespace TournamentEngine.Api.Endpoints;

using Microsoft.AspNetCore.Builder;

/// <summary>
/// API endpoints for downloading resources like bot templates
/// </summary>
public static class ResourceEndpoints
{
    public static void MapResourceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/resources")
            .WithName("Resources");

        group.MapGet("/templates/{templateName}", DownloadTemplate)
            .WithName("DownloadBotTemplate");
    }

    /// <summary>
    /// GET /api/resources/templates/{templateName} - Download a bot template zip file
    /// </summary>
    private static IResult DownloadTemplate(
        string templateName,
        ILogger<Program> logger)
    {
        logger.LogInformation("Template download request for {TemplateName}", templateName);

        // Security: Only allow alphanumeric, hyphens, and underscores
        if (!System.Text.RegularExpressions.Regex.IsMatch(templateName, @"^[a-zA-Z0-9_-]+$"))
        {
            logger.LogWarning("Invalid template name rejected: {TemplateName}", templateName);
            return Results.BadRequest(new { success = false, message = "Invalid template name" });
        }

        // Ensure .zip extension
        if (!templateName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            templateName += ".zip";
        }

        // Look for template file in templates directory
        var templatesPath = Path.Combine(Directory.GetCurrentDirectory(), "templates");
        var templateFilePath = Path.Combine(templatesPath, templateName);

        if (!File.Exists(templateFilePath))
        {
            logger.LogWarning("Template not found: {TemplateName} at {Path}", templateName, templateFilePath);
            return Results.NotFound(new { success = false, message = $"Template {templateName} not found" });
        }

        logger.LogInformation("Serving template file: {TemplateName}", templateName);
        
        var fileStream = File.OpenRead(templateFilePath);
        return Results.File(fileStream, "application/zip", templateName);
    }
}
