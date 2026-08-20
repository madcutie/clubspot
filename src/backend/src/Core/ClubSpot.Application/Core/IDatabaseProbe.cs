namespace ClubSpot.Application.Core;

// Readiness only: answers whether the database is reachable, never what is in it. The Api needs
// this to tell an orchestrator it can take traffic, and ADR-0005 keeps it off the DbContext.
public interface IDatabaseProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
