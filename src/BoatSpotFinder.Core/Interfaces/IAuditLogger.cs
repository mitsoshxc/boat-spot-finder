namespace BoatSpotFinder.Core.Interfaces;

public interface IAuditLogger
{
    void Log(
        string userId,
        string userEmail,
        string action,
        string entityType,
        string entityId,
        string? marinaId,
        object? details);
}
