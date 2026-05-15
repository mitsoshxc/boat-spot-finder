using BoatSpotFinder.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BoatSpotFinder.Infrastructure.Logging;

public class NLogAuditLogger : IAuditLogger
{
    private readonly ILogger<NLogAuditLogger> _logger;

    public NLogAuditLogger(ILogger<NLogAuditLogger> logger)
    {
        _logger = logger;
    }

    public void Log(
        string userId,
        string userEmail,
        string action,
        string entityType,
        string entityId,
        string? marinaId,
        object? details)
    {
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["userId"] = userId,
            ["userEmail"] = userEmail,
            ["userRole"] = string.Empty,
            ["action"] = action,
            ["entityType"] = entityType,
            ["entityId"] = entityId,
            ["marinaId"] = marinaId,
            ["details"] = details is null ? null : System.Text.Json.JsonSerializer.Serialize(details)
        }))
        {
            _logger.LogInformation("Audit: {Action} on {EntityType} {EntityId}", action, entityType, entityId);
        }
    }
}
