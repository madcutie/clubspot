using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core;
using ClubSpot.Application.Core.People;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Payments;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class BookingsStore(
    ClubSpotDbContext db, ITenantContext tenantContext, IClubSettings clubSettings, IPeopleLink peopleLink,
    IOptions<PaymentsOptions> paymentsOptions, IClock clock) : IBookingsStore
{
    public async Task<BookingCreateResult> CreateAsync(BookingCreateInput input, CancellationToken cancellationToken)
    {
        var court = await db.Courts.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == input.CourtId, cancellationToken);
        if (court is null || !court.IsActive)
            return new BookingCreateResult(BookingCreateOutcome.UnknownCourt, Guid.Empty, default);

        var schedule = await db.Schedules.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == court.ScheduleId, cancellationToken);
        var overrides = await db.AvailabilityOverrides.AsNoTracking()
            .Where(availabilityOverride => availabilityOverride.CourtId == null || availabilityOverride.CourtId == court.Id)
            .Where(availabilityOverride => availabilityOverride.Dates.Any(date => date.Date == input.Date))
            .Include(availabilityOverride => availabilityOverride.Dates)
            .ToListAsync(cancellationToken);

        var club = await clubSettings.GetAsync(cancellationToken);
        var calendar = new ClubCalendar(TimeZoneInfo.FindSystemTimeZoneById(club.TimeZone), clock);
        // The operator sells on the spot: a negative now-minute keeps the minimum notice from cutting starts.
        // The portal customer gets the same clock the portal availability showed them.
        var now = calendar.Now();
        var nowMinute = input.Origin == BookingOrigin.Portal ? now.Hour * 60 + now.Minute : -court.MinimumNoticeMinutes;
        var slots = AvailabilityCalculator.SlotsFor(
            court, schedule, overrides, [], input.Date, calendar.Today(), nowMinute);
        var slot = slots.FirstOrDefault(candidate =>
            candidate.StartMinute == input.StartMinute && candidate.DurationMinutes == input.DurationMinutes);
        if (slot is null) return new BookingCreateResult(BookingCreateOutcome.InvalidSlot, Guid.Empty, default);

        // Lazy expiry: dead holds on this court and date stop blocking before we check and insert.
        await ExpireStaleHoldsAsync(court.Id, input.Date, cancellationToken);

        var utcNow = clock.UtcNow;
        var taken = await db.Bookings.AnyAsync(booking => booking.CourtId == court.Id && booking.Date == input.Date
            && (booking.Status == BookingStatus.Confirmed
                || (booking.Status == BookingStatus.PendingPayment && booking.ExpiresAt > utcNow))
            && booking.StartMinute < input.StartMinute + input.DurationMinutes
            && input.StartMinute < booking.StartMinute + booking.DurationMinutes, cancellationToken);
        if (taken) return new BookingCreateResult(BookingCreateOutcome.SlotTaken, Guid.Empty, default);

        Guid? personId = input.Origin == BookingOrigin.Portal
            ? await peopleLink.EnsurePersonAsync(input.CustomerName, input.CustomerPhone ?? "", input.CustomerEmail, cancellationToken)
            : null;

        var chargeAmount = ChargeAmountFor(input.PaymentMode, slot.Price, club.DepositPercent);
        var booking = input.PaymentMode == PaymentMode.Club
            ? new Booking(Guid.NewGuid(), tenantContext.Current, court.Id, input.Date, input.StartMinute,
                input.DurationMinutes, slot.Price, input.CustomerName, input.CustomerPhone, personId, input.Origin,
                utcNow, input.CreatedBy)
            : Booking.Hold(Guid.NewGuid(), tenantContext.Current, court.Id, input.Date, input.StartMinute,
                input.DurationMinutes, slot.Price, input.CustomerName, input.CustomerPhone, personId, input.Origin,
                input.PaymentMode, utcNow.AddMinutes(paymentsOptions.Value.HoldMinutes), utcNow, input.CreatedBy);
        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            return new BookingCreateResult(BookingCreateOutcome.SlotTaken, Guid.Empty, default);
        }
        return new BookingCreateResult(BookingCreateOutcome.Created, booking.Id, booking.Price,
            booking.Status, chargeAmount, booking.ExpiresAt);
    }

    public async Task<BookingCancelOutcome> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (booking is null) return BookingCancelOutcome.NotFound;

        if (booking.Status != BookingStatus.Cancelled)
        {
            booking.Cancel(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
        return BookingCancelOutcome.Cancelled;
    }

    public async Task<PaymentApplyOutcome> ApplyPaymentAsync(PaymentNotification notification, CancellationToken cancellationToken)
    {
        // Idempotency anchor: (gateway, externalId) is unique, so a replayed webhook is a no-op.
        if (await db.Payments.AnyAsync(payment => payment.Gateway == notification.Gateway
            && payment.ExternalId == notification.ExternalId, cancellationToken))
            return PaymentApplyOutcome.AlreadyProcessed;

        var booking = await db.Bookings.SingleOrDefaultAsync(candidate => candidate.Id == notification.BookingId, cancellationToken);
        if (booking is null) return PaymentApplyOutcome.UnknownBooking;

        var club = await clubSettings.GetAsync(cancellationToken);
        var kind = booking.PaymentMode == PaymentMode.OnlineDeposit ? PaymentKind.Deposit : PaymentKind.Full;
        var amount = notification.Amount is { } charged
            ? Money.Of(charged, booking.Price.Currency)
            : ChargeAmountFor(booking.PaymentMode, booking.Price, club.DepositPercent);

        var payment = new Payment(Guid.NewGuid(), tenantContext.Current, booking.Id, notification.Gateway,
            notification.ExternalId, amount, kind,
            notification.Approved ? PaymentStatus.Approved : PaymentStatus.Rejected, clock.UtcNow);
        db.Payments.Add(payment);

        if (!notification.Approved)
        {
            // The customer can retry inside the hold's TTL; the hold stays as is.
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Rejected;
        }

        if (booking.Status == BookingStatus.Confirmed)
        {
            // Duplicate money on an already confirmed booking: recorded, needs manual follow-up.
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Confirmed;
        }

        booking.ConfirmPayment();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Confirmed;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // The hold had expired and someone else took the slot: keep the money recorded, flag it.
            await db.Entry(booking).ReloadAsync(cancellationToken);
            payment.MarkOrphaned();
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Orphaned;
        }
    }

    public async Task<BookingSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (booking is null) return null;

        var court = await db.Courts.AsNoTracking().SingleAsync(candidate => candidate.Id == booking.CourtId, cancellationToken);
        var paidAmount = await db.Payments.AsNoTracking()
            .Where(payment => payment.BookingId == id && payment.Status == PaymentStatus.Approved)
            .SumAsync(payment => payment.Amount.Amount, cancellationToken);
        return new BookingSnapshot(booking.Id, court.Id, court.Name, court.Sport, booking.Date,
            booking.StartMinute, booking.DurationMinutes, booking.Price.Amount, paidAmount,
            booking.Status, booking.PaymentMode, booking.ExpiresAt);
    }

    private Task<int> ExpireStaleHoldsAsync(Guid courtId, DateOnly date, CancellationToken cancellationToken)
    {
        var utcNow = clock.UtcNow;
        return db.Bookings
            .Where(booking => booking.CourtId == courtId && booking.Date == date
                && booking.Status == BookingStatus.PendingPayment && booking.ExpiresAt <= utcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(booking => booking.Status, BookingStatus.Expired), cancellationToken);
    }

    private static Money ChargeAmountFor(PaymentMode mode, Money price, int depositPercent) => mode switch
    {
        PaymentMode.OnlineDeposit => Money.Of(Math.Round(price.Amount * depositPercent / 100m, 2), price.Currency),
        _ => price
    };
}
