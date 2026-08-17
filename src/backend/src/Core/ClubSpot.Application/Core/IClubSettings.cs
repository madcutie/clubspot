namespace ClubSpot.Application.Core;

public sealed record ClubInfo(string Name, string? Venue, string TimeZone, string Currency, int DepositPercent);

public interface IClubSettings
{
    Task<ClubInfo> GetAsync(CancellationToken cancellationToken);
}
