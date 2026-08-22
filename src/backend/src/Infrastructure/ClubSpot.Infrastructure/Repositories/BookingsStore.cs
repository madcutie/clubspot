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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class BookingsStore(
    ClubSpotDbContext db, ITenantContext tenantContext, IClubSettings clubSettings, IPeopleLink peopleLink,
    IOptions<PaymentsOptions> paymentsOptions, IClock clock, IActivityLog activityLog,
    ILogger<BookingsStore> logger) : IBookingsStore
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
        // Frozen on the hold, not read again when the payment lands: the club can move the percentage
        // while a checkout is in flight, and the customer owes what was asked of them at that moment.
        var agreedPercent = input.PaymentMode == PaymentMode.OnlineDeposit ? club.DepositPercent : (int?)null;
        var booking = input.PaymentMode == PaymentMode.Club
            ? new Booking(Guid.NewGuid(), tenantContext.Current, court.Id, input.Date, input.StartMinute,
                input.DurationMinutes, slot.Price, input.CustomerName, input.CustomerPhone, personId, input.Origin,
                utcNow, input.CreatedBy)
            : Booking.Hold(Guid.NewGuid(), tenantContext.Current, court.Id, input.Date, input.StartMinute,
                input.DurationMinutes, slot.Price, input.CustomerName, input.CustomerPhone, personId, input.Origin,
                input.PaymentMode, utcNow.AddMinutes(paymentsOptions.Value.HoldMinutes), agreedPercent, utcNow,
                input.CreatedBy);
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
            // Nothing is wrong here — two people wanted the same slot and the constraint picked one —
            // but it is the only explanation for a 409 the caller can otherwise not account for.
            logger.LogInformation(
                "Slot {Court} {Date} {Start}+{Duration} was taken concurrently; the booking was refused.",
                court.Id, input.Date, input.StartMinute, input.DurationMinutes);
            return new BookingCreateResult(BookingCreateOutcome.SlotTaken, Guid.Empty, default);
        }
        return new BookingCreateResult(BookingCreateOutcome.Created, booking.Id, booking.Price,
            booking.Status, chargeAmount, booking.ExpiresAt);
    }

    public async Task<BookingCancelOutcome> CancelAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason) || trimmedReason.Length > Booking.CancellationReasonMaxLength)
            return BookingCancelOutcome.MissingReason;

        var booking = await db.Bookings.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (booking is null) return BookingCancelOutcome.NotFound;

        // Cancelling twice answers the same and leaves a single line in the chronicle.
        if (booking.Status != BookingStatus.Cancelled)
        {
            // What the booking had been paid, photographed into the entry. Cancelling does not move
            // money — refunds are not modelled yet — so this is the only place that says out loud
            // that the club is now holding money for a slot that will not be played.
            var paid = await db.Payments.AsNoTracking()
                .Where(payment => payment.BookingId == booking.Id && payment.Status == PaymentStatus.Approved)
                .SumAsync(payment => payment.Amount.Amount, cancellationToken);

            booking.Cancel(clock.UtcNow, trimmedReason);
            activityLog.Record(new ActivityRecord(BookingActivity.BookingCancelled, BookingId: booking.Id,
                PersonId: booking.PersonId, Reason: trimmedReason,
                Data: new Dictionary<string, object?>
                {
                    ["paidAmount"] = paid,
                    ["currency"] = booking.Price.Currency,
                    ["refundPending"] = paid > 0m ? true : (bool?)null
                }));
            await db.SaveChangesAsync(cancellationToken);
        }
        return BookingCancelOutcome.Cancelled;
    }

    public async Task<HoldReleaseOutcome> ReleaseHoldAsync(Guid id, CancellationToken cancellationToken)
    {
        // Conditional update: a hold the webhook confirmed a moment ago must never be released.
        // Expired and not Cancelled: giving up on the checkout is the same fact as letting the TTL run
        // out, so a payment landing afterwards can still confirm instead of being orphaned, and J2 —
        // which reconciles pending and expired holds — keeps watching it. Cancelled is left meaning
        // exclusively "a person decided, and left a reason".
        var released = await db.Bookings
            .Where(booking => booking.Id == id && booking.Status == BookingStatus.PendingPayment)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(booking => booking.Status, BookingStatus.Expired), cancellationToken);
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
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var outcome = await ApplyPaymentCoreAsync(notification, source, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return outcome;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two notifications for the same payment raced past the lookup below: the unique index on
            // (provider, externalId) is what actually makes a replayed payment a no-op.
            logger.LogInformation(
                "Payment {Provider}/{ExternalId} for booking {Booking} arrived twice at once; the duplicate was dropped.",
                notification.Provider, notification.ExternalId, notification.BookingId);
            return PaymentApplyOutcome.AlreadyProcessed;
        }
    }

    private async Task<PaymentApplyOutcome> ApplyPaymentCoreAsync(PaymentNotification notification,
        PaymentSource source, CancellationToken cancellationToken)
    {
        // Serialization point for everything this booking's money depends on. Without it two approved
        // payments arriving together both read a settled total of zero, neither looks like a duplicate,
        // and the booking ends up charged twice with nothing flagged. Taken before the first read so
        // the whole decision — what is already settled, what this payment is, whether it confirms —
        // happens against a state no other payment can move underneath it.
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM public.bookings WHERE id = {notification.BookingId} FOR UPDATE", cancellationToken);

        // EF answers a query with the instance it already tracks, not the row just read. J2 walks
        // many payments inside one scope, so without this the decision below could be made against
        // a state the lock above was taken precisely to freeze.
        db.ChangeTracker.Clear();

        // Idempotency anchor: (provider, externalId) is unique. A notification that repeats what is
        // already settled is a no-op, but one that carries a *newer* state for a payment still
        // undecided has to land — see Payment.Accepts.
        var existing = await db.Payments.SingleOrDefaultAsync(payment => payment.Provider == notification.Provider
            && payment.ExternalId == notification.ExternalId, cancellationToken);
        if (existing is not null
            && (existing.BookingId != notification.BookingId || !existing.Accepts(notification.Outcome)))
            return PaymentApplyOutcome.AlreadyProcessed;

        var booking = await db.Bookings.SingleOrDefaultAsync(candidate => candidate.Id == notification.BookingId, cancellationToken);
        if (booking is null) return PaymentApplyOutcome.UnknownBooking;

        var club = await clubSettings.GetAsync(cancellationToken);

        // What the booking already settled answers two questions at once: what this payment is —
        // a first charge or the balance left by a deposit — and whether it is duplicate money.
        // A payment of our own that has not settled is not part of that sum.
        var settled = await db.Payments.AsNoTracking()
            .Where(candidate => candidate.BookingId == booking.Id && candidate.Status == PaymentStatus.Approved)
            .SumAsync(candidate => candidate.Amount.Amount, cancellationToken);
        var kind = settled > 0m
            ? PaymentKind.Balance
            : booking.PaymentMode == PaymentMode.OnlineDeposit ? PaymentKind.Deposit : PaymentKind.Full;

        // What this particular payment was asked for: the first charge is the deposit or the whole
        // price, the balance is whatever the deposit left owing. Reading the deposit for both is what
        // made a correct balance payment look short whenever the deposit was not exactly half.
        // The percentage comes off the booking, which froze it when the hold was taken; the live club
        // setting is only reached by holds created before that column existed, and those die with the TTL.
        var expected = kind == PaymentKind.Balance
            ? Money.Of(booking.Price.Amount - settled, booking.Price.Currency)
            : ChargeAmountFor(booking.PaymentMode, booking.Price, booking.DepositPercent ?? club.DepositPercent);

        // The row records the currency the provider actually settled in, not the one the club asked
        // for: written the other way round, a mismatch would be invisible on the payment itself.
        var currency = notification.Currency is { Length: 3 } reported ? reported : booking.Price.Currency;
        var amount = notification.Amount is { } charged
            ? Money.Of(charged, currency)
            : expected;

        var status = notification.Outcome switch
        {
            PaymentOutcome.Approved => PaymentStatus.Approved,
            PaymentOutcome.Rejected => PaymentStatus.Rejected,
            _ => PaymentStatus.Pending
        };

        // A repeat of "still undecided" says nothing new. It has to return before Settle, which
        // refuses a status that is not a decision: Mercado Pago re-notifies an offline payment until
        // it is paid, and J2 keeps finding it, so this is the common case and not an edge one.
        // Not AlreadyProcessed, so callers looking for the payment that did settle keep looking.
        if (existing is not null && notification.Outcome == PaymentOutcome.Pending)
            return PaymentApplyOutcome.Pending;

        Payment payment;
        if (existing is not null)
        {
            payment = existing;
            payment.Settle(amount, kind, status, source);
        }
        else
        {
            payment = new Payment(Guid.NewGuid(), tenantContext.Current, booking.Id, notification.Provider,
                notification.Rail, notification.ExternalId, amount, kind, status, source, clock.UtcNow);
            db.Payments.Add(payment);
        }

        void RecordPayment(string type, PaymentOrphanReason? why = null)
        {
            // Money the club is holding for something it did not agree to. It is already in the
            // chronicle for the operator; this line is so it can also be found while troubleshooting,
            // which is the only way anyone learns about it before the customer complains.
            if (why is { } reason)
                logger.LogWarning(
                    "Payment {Provider}/{ExternalId} of {Amount} {Currency} on booking {Booking} was orphaned: {Reason}.",
                    notification.Provider, notification.ExternalId, amount.Amount, amount.Currency,
                    booking.Id, reason);
            activityLog.Record(new ActivityRecord(
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
        }

        if (notification.Outcome == PaymentOutcome.Pending)
        {
            // The provider has the money but has not decided it: the row is written so the payment is
            // on record and reconciliation keeps watching it, and nothing about the booking moves yet.
            RecordPayment(BookingActivity.PaymentPending);
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Pending;
        }

        if (notification.Outcome == PaymentOutcome.Rejected)
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
            if (duplicate) payment.MarkOrphaned(PaymentOrphanReason.Duplicate);
            RecordPayment(duplicate ? BookingActivity.PaymentOrphaned : BookingActivity.PaymentApproved,
                duplicate ? PaymentOrphanReason.Duplicate : null);
            await db.SaveChangesAsync(cancellationToken);
            return duplicate ? PaymentApplyOutcome.Orphaned : PaymentApplyOutcome.Confirmed;
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            // The buyer paid while the hold was being released: keep the money recorded, flag it.
            payment.MarkOrphaned(PaymentOrphanReason.BookingLost);
            RecordPayment(BookingActivity.PaymentOrphaned, PaymentOrphanReason.BookingLost);
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
            var reason = wrongCurrency ? PaymentOrphanReason.WrongCurrency : PaymentOrphanReason.Short;
            payment.MarkOrphaned(reason);
            RecordPayment(BookingActivity.PaymentOrphaned, reason);
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
            payment.MarkOrphaned(PaymentOrphanReason.SlotLost);
            // The confirmation entry describes a fact that did not happen: it goes, the orphan stays.
            activityLog.DiscardPending();
            RecordPayment(BookingActivity.PaymentOrphaned, PaymentOrphanReason.SlotLost);
            await db.SaveChangesAsync(cancellationToken);
            return PaymentApplyOutcome.Orphaned;
        }
    }

    public async Task RecordCheckoutIssuedAsync(CheckoutIssued issued, CancellationToken cancellationToken)
    {
        db.BookingCheckouts.Add(new BookingCheckout(Guid.NewGuid(), tenantContext.Current, issued.BookingId,
            issued.Provider, issued.Url, issued.Amount, issued.ExpiresAt, clock.UtcNow));
        activityLog.Record(new ActivityRecord(BookingActivity.CheckoutIssued, BookingId: issued.BookingId));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CheckoutIssued?> FindLiveCheckoutAsync(Guid bookingId, string provider, Money amount,
        DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        await db.BookingCheckouts.AsNoTracking()
            .Where(checkout => checkout.BookingId == bookingId
                && checkout.Provider == provider
                && checkout.Amount.Amount == amount.Amount
                && checkout.Amount.Currency == amount.Currency
                && checkout.ExpiresAt == expiresAt)
            .OrderByDescending(checkout => checkout.IssuedAt)
            .Select(checkout => new CheckoutIssued(checkout.BookingId, checkout.Provider, checkout.Url,
                checkout.Amount, checkout.ExpiresAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<BookingSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (booking is null) return null;

        var court = await db.Courts.AsNoTracking().SingleAsync(candidate => candidate.Id == booking.CourtId, cancellationToken);
        var paidAmount = await db.Payments.AsNoTracking()
            .Where(payment => payment.BookingId == id && payment.Status == PaymentStatus.Approved)
            .SumAsync(payment => payment.Amount.Amount, cancellationToken);
        // Every attempt on record, rejected ones included: "no me anduvo la tarjeta y probé de nuevo"
        // is the question this list exists to answer.
        var payments = await db.Payments.AsNoTracking()
            .Where(payment => payment.BookingId == id)
            .OrderBy(payment => payment.CreatedAt)
            .Select(payment => new BookingPaymentLine(payment.CreatedAt, payment.Provider, payment.ExternalId,
                payment.Amount.Amount, payment.Amount.Currency, payment.Kind, payment.Status))
            .ToListAsync(cancellationToken);
        return new BookingSnapshot(booking.Id, court.Id, court.Name, court.Sport, booking.Date,
            booking.StartMinute, booking.DurationMinutes, booking.Price.Amount, paidAmount,
            booking.Status, booking.PaymentMode, booking.ExpiresAt, booking.CreatedAt, payments);
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
