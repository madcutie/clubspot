using ClubSpot.Domain.Core;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.UnitTests.Core;

public sealed class UserTests
{
    [Fact]
    public void User_normalizes_email_and_removes_duplicate_roles()
    {
        var user = new User(
            Guid.NewGuid(),
            TenantId.From(Guid.NewGuid()),
            " Operator@ClubSpot.Test ",
            "Operator",
            "hash",
            [Role.Administrator, Role.Administrator],
            DateTimeOffset.UtcNow);

        Assert.Equal("operator@clubspot.test", user.Email);
        Assert.Single(user.Roles);
        Assert.Equal(Role.Administrator, user.Roles.Single());
    }
}
