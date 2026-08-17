using ClubSpot.Application.Core;
using ClubSpot.Application.Core.People;
using ClubSpot.Domain.Core.People;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class PeopleLink(
    ClubSpotDbContext db, ITenantContext tenantContext, IClubSettings clubSettings, IClock clock) : IPeopleLink
{
    public async Task<Guid> EnsurePersonAsync(string name, string phone, string? email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedEmail))
        {
            var byEmail = await db.People.AsNoTracking()
                .Where(person => person.Email == normalizedEmail)
                .OrderBy(person => person.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (byEmail is not null) return byEmail.Id;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length > 0)
        {
            var byPhone = await db.People.AsNoTracking()
                .Where(person => person.PhoneDigits == digits)
                .OrderBy(person => person.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (byPhone is not null) return byPhone.Id;
        }

        var club = await clubSettings.GetAsync(cancellationToken);
        var person = new Person(Guid.NewGuid(), tenantContext.Current, name, phone, normalizedEmail ?? "",
            PersonOrigin.App, Money.Zero(club.Currency), null, clock);
        db.People.Add(person);
        return person.Id;
    }
}
