using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core;
using ClubSpot.Application.Core.Activity;
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
    IOptions<PaymentsOptions> paymentsOptions, IClock clock, IActivityLog activityLog) : IBookingsStore
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
        activityLog.Record(new ActivityRecord(
            booking.Status == BookingStatus.PendingPayment ? BookingActivity.HoldCreated : BookingActivity.BookingCreated,
            BookingId: booking.Id, PersonId: personId,
            Data: new Dictionary<string, object?>
            {
                ["courtId"] = court.Id,
                ["courtName"] = court.Name,
                ["date"] = input.Date,
                ["startMinute"] = input.StartMinute,
                ["durationMinutes"] = input.DurationMinutes,
                ["price"] = booking.Price.Amount,
                ["currency"] = booking.Price.Currency,
                ["paymentMode"] = input.PaymentMode,
                ["expiresAt"] = booking.ExpiresAt
            }));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            activityLog.DiscardPending();
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

    public async Task<HoldReleaseOutcome> ReleaseHoldAsync(Guid id, CancellationToken cancellationToken)
    {
        // Conditional update: a hold the webhook confirmed a moment ago must never be cancelled.
        var released = await db.Bookings
            .Where(booking => booking.Id == id && booking.Status == BookingStatus.PendingPayment)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(booking => booking.Status, BookingStatus.Cancelled)
                .SetProperty(booking => booking.CancelledAt, clock.UtcNow), cancellationToken);
        if (released > 0)
        {
            activityLog.Record(new ActivityRecord(BookingActivity.HoldReleased, BookingId: id));
            await db.SaveChangesAsync(cancellationToken);
            return HoldReleaseOutcome.Released;
        }

        return await db.Bookings.AnyAsync(booking => booking.Id == id, cancellationToken)
            ? HoldReleaseOutcome.NotPending
            : HoldReleaseOutcome.NotFound;
    }

    public async Task<PaymentApplyOutcome> ApplyPaymentAsync(PaymentNotification notification, PaymentSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ApplyPaymentCoreAsync(notification, source, cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The webhook and the buyer's settle call raced past the check below: the unique index on
            // (provider, externalId) is what actually makes a replayed payment a no-op.
            return PaymentApplyOutcome.AlreadyProcessed;
        }
    }

    private async Task<PaymentApplyOutcome> ApplyPaymentCoreAsync(PaymentNotification notification,
        PaymentSource source, CancellationToken cancellationToken)
    {
        // Idempotency anchor: (provider, externalId) is unique, so a replayed webhook is a no-op.
        if (await db.Payments.AnyAsync(payment => payment.Provider == notification.Provider
            && payment.ExternalId == notification.ExternalId, cancellationToken))
            return PaymentApplyOutcome.AlreadyProcessed;

        var booking = await db.Bookings.SingleOrDefaultAsync(candidate => candidate.Id == notification.BookingId, cancellationToken);
        if (booking is null) return PaymentApplyOutcome.UnknownBooking;

        var club = await clubSettings.GetAsync(cancellationToken);
        var expected = ChargeAmountFor(booking.PaymentMode, booking.Price, club.DepositPercent);

        // What the booking already settled answers two questions at once: what this payment is —
        // a first charge or the balance left by a deposit — and whether it is duplicate money.
        var settled = await db.Payments.AsNoTracking()
            .Where(candidate => candidate.BookingId == booking.Id && candidate.Status == PaymentStatus.Approved)
            .SumAsync(candidate => candidate.Amount.Amount, cancellationToken);
        var kind = settled > 0m
            ? PaymentKind.Balance
            : booking.PaymentMode == PaymentMode.OnlineDeposit ? PaymentKind.Deposit : PaymentKind.Full;

        // The row records the currency the provider actually settled in, not the one the club asked
        // for: written the other way round, a mismatch would be invisible on the payment itself.
        var currency = notification.Currency is { Length: 3 } reported ? reported : booking.Price.Currency;
        var amount = notification.Amount is { } charged
            ? Money.Of(charged, currency)
            : expected;

        var payment = new Payment(Guid.NewGuid(), tenantContext.Current, booking.Id, notification.Provider,
            notification.Rail, notification.ExternalId, amount, kind,
            notification.Approved ? PaymentStatus.Approved : PaymentStatus.Rejected, source, clock.UtcNow);
        db.Payments.Add(payment);

        void RecordPayment(string type, string? why = null) => activityLog.Record(new ActivityRecord(
            type, BookingId: booking.Id, PersonId: booking.PersonId, PaymentId: payment.Id,
            Data: new Dictionary<string, object?>
            {
                ["amount"] = amount.Amount,
                ["currency"] = amount.Currency,
                ["provider"] = notification.Provider,
                ["rail"] = notification.Rail,
                ["externalId"] = notification.ExternalId,
                ["kind"] = kind,
                ["why"] = why
            }));

        if (!notification.Approved)
        {
            // The customer can retry inside the hold's TTL; the hold stays as is.
            RecordPayment(BookingActivity.PaymentRejected);
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Rejected;
        }

        if (booking.Status == BookingStatus.Confirmed)
        {
            // A confirmed booking can still owe money (a deposit paid online, the balance at the
            // counter): only what arrives on top of a settled balance is duplicate money, and that
            // is flagged instead of being filed as one more ordinary payment.
            var duplicate = settled >= booking.Price.Amount;
            if (duplicate) payment.MarkOrphaned();
            RecordPayment(duplicate ? BookingActivity.PaymentOrphaned : BookingActivity.PaymentApproved,
                duplicate ? "duplicate" : null);
            await db.SaveChangesAsync(cancellationToken);
            return duplicate ? PaymentApplyOutcome.Orphaned : PaymentApplyOutcome.Confirmed;
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            // The buyer paid while the hold was being released: keep the money recorded, flag it.
            payment.MarkOrphaned();
            RecordPayment(BookingActivity.PaymentOrphaned, "bookingLost");
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Orphaned;
        }

        // Either the deposit the site asked for or the whole price: nothing in between takes a slot.
        // A short payment, or one settled in another currency, is kept on record and flagged instead
        // of confirming — the club decides what to do with money it did not agree to.
        var wrongCurrency = notification.Currency is { } paidCurrency
            && !string.Equals(paidCurrency, booking.Price.Currency, StringComparison.OrdinalIgnoreCase);
        if (wrongCurrency || amount.Amount < expected.Amount)
        {
            payment.MarkOrphaned();
            RecordPayment(BookingActivity.PaymentOrphaned, wrongCurrency ? "wrongCurrency" : "short");
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Orphaned;
        }

        booking.ConfirmPayment();
        try
        {
            RecordPayment(BookingActivity.PaymentApproved);
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Confirmed;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // The hold had expired and someone else took the slot: keep the money recorded, flag it.
            await db.Entry(booking).ReloadAsync(cancellationToken);
            payment.MarkOrphaned();
            // The confirmation entry describes a fact that did not happen: it goes, the orphan stays.
            activityLog.DiscardPending();
            RecordPayment(BookingActivity.PaymentOrphaned, "slotLost");
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Orphaned;
        }
    }

    public async Task RecordCheckoutIssuedAsync(Guid bookingId, Money amount, DateTimeOffset expiresAt,
        string provider, CancellationToken cancellationToken)
    {
        activityLog.Record(new ActivityRecord(BookingActivity.CheckoutIssued, BookingId: bookingId,
            Data: new Dictionary<string, object?>
            {
                ["amount"] = amount.Amount,
                ["currency"] = amount.Currency,
                ["provider"] = provider,
                ["expiresAt"] = expiresAt
            }));
        await db.SaveChangesAsync(cancellationToken);
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

    public async Task<IReadOnlyList<Guid>> GetUnsettledOnlineBookingIdsAsync(
        DateTimeOffset since, int limit, CancellationToken cancellationToken) =>
        await db.Bookings.AsNoTracking()
            .Where(booking => booking.PaymentMode != PaymentMode.Club
                && (booking.Status == BookingStatus.PendingPayment || booking.Status == BookingStatus.Expired)
                && booking.CreatedAt >= since
                && !db.Payments.Any(payment => payment.BookingId == booking.Id
                    && payment.Status == PaymentStatus.Approved))
            .OrderBy(booking => booking.CreatedAt)
            .Take(limit)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

    private async Task<int> ExpireStaleHoldsAsync(Guid courtId, DateOnly date, CancellationToken cancellationToken)
    {
        var utcNow = clock.UtcNow;
        var stale = db.Bookings
            .Where(booking => booking.CourtId == courtId && booking.Date == date
                && booking.Status == BookingStatus.PendingPayment && booking.ExpiresAt <= utcNow);

        // Read before the update: after it they are no longer pending and there is nothing to name.
        var expiring = await stale
            .Select(booking => new { booking.Id, booking.ExpiresAt })
            .ToListAsync(cancellationToken);
        var expired = await stale.ExecuteUpdateAsync(setters => setters
            .SetProperty(booking => booking.Status, BookingStatus.Expired), cancellationToken);

        // Flushed here and not with the caller's work: the expiry already happened in its own
        // statement, and it is a fact of its own even if what the caller was doing then fails.
        foreach (var booking in expiring)
        {
            activityLog.Record(new ActivityRecord(BookingActivity.HoldExpired, BookingId: booking.Id,
                Data: new Dictionary<string, object?>
                {
                    ["expiredAt"] = booking.ExpiresAt,
                    ["noticedAfterSeconds"] = booking.ExpiresAt is { } due ? (int)(utcNow - due).TotalSeconds : null
                }));
        }
        if (expiring.Count > 0) await db.SaveChangesAsync(cancellationToken);
        return expired;
    }

    private static Money ChargeAmountFor(PaymentMode mode, Money price, int depositPercent) => mode switch
    {
        PaymentMode.OnlineDeposit => Money.Of(Math.Round(price.Amount * depositPercent / 100m, 2), price.Currency),
        _ => price
    };
}
