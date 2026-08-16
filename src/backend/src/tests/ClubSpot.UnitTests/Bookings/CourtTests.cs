using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.UnitTests.Bookings;

public sealed class CourtTests
{
    [Fact]
    public void A_court_requires_at_least_one_duration()
    {
        Assert.Throws<ArgumentException>(() => new Court(
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), Sport.Padel, 1, "Court 1", "", false, true,
            Guid.NewGuid(), [], 30, 0, 100m, 100m, 1080));
    }
}
