using ClubSpot.Domain.Core;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.UnitTests.Core;

public sealed class ClubTests
{
    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public void A_deposit_is_half_the_price_or_all_of_it(int depositPercent)
    {
        var club = Make(depositPercent);

        Assert.Equal(depositPercent, club.DepositPercent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(70)]
    [InlineData(101)]
    public void No_other_deposit_percentage_is_accepted(int depositPercent) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(depositPercent));

    private static Club Make(int depositPercent) => new(
        TenantId.From(Guid.NewGuid()), "club-test", "Club Test", null,
        "America/Argentina/Buenos_Aires", "ARS", depositPercent, DateTimeOffset.UtcNow);
}
