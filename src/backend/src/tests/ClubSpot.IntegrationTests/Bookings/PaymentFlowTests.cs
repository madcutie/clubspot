using ClubSpot.Domain.Bookings;
using ClubSpot.IntegrationTests.Auth;
using ClubSpot.IntegrationTests.Json;
using ClubSpot.IntegrationTests.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace ClubSpot.IntegrationTests.Bookings;

[Collection("postgres")]
public sealed class PaymentFlowTests(PostgresFixture postgres)
{
    private static readonly TenantId SeedTenant = TenantId.From(Guid.Parse("a7b00b98-6191-433d-8930-3273904c1faa"));

    [Fact]
    public async Task An_online_booking_holds_the_slot_and_confirms_on_approval()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 12);

        var response = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Pago Online", customerPhone = "362 400-0100", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(TestJsonOptions.Default);
        Assert.Equal(BookingStatus.PendingPayment, created!.Status);
        Assert.Equal(created.Price, created.ChargeAmount);
        Assert.NotNull(created.ExpiresAt);
        Assert.Contains("/dev/checkout", created.CheckoutUrl);

        // The live hold blocks a second sale of the same slot.
        var conflicting = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Otro Cliente", customerPhone = "362 400-0101", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-test-1", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.Confirmed, snapshot!.Status);
        Assert.Equal(created.ChargeAmount, snapshot.PaidAmount);
    }

    [Fact]
    public async Task A_replayed_webhook_does_not_duplicate_the_payment()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 13);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0102");

        var body = new { bookingId = created.Id, externalId = "fake-replay-1", approved = true, amount = created.ChargeAmount };
        var first = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", body);
        var replay = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        Assert.Equal(1, await db.Payments.CountAsync(payment => payment.BookingId == created.Id));
    }

    [Fact]
    public async Task A_rejected_payment_keeps_the_hold_pending()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 14);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0103");

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-reject-1", approved = false, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.PendingPayment, snapshot!.Status);
        Assert.Equal(0, snapshot.PaidAmount);
    }

    // The regression for the bug that lost the most money: Mercado Pago announces a payment before it
    // resolves — always for the offline methods — and that first notification used to be filed as a
    // rejection under the payment's own id. The approval that followed then looked like a replay, so
    // the booking never confirmed while the money was taken, and neither J2 nor the portal's settle
    // could repair it because all three share this path.
    [Fact]
    public async Task A_payment_reported_pending_still_confirms_the_slot_when_it_is_approved()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 16);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0110");

        var pending = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-pending-1", approved = false,
            amount = created.ChargeAmount, outcome = "pending"
        });
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        Assert.Equal(BookingStatus.PendingPayment, (await SnapshotAsync(client, created)).Status);

        // Same payment id: it is the same money finally settling, not a second payment.
        var approved = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-pending-1", approved = true,
            amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.Confirmed, snapshot.Status);
        Assert.Equal(created.ChargeAmount, snapshot.PaidAmount);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payments = await db.Payments.Where(payment => payment.BookingId == created.Id).ToListAsync();
        // One row, advanced in place: the unique (provider, externalId) stays the idempotency anchor.
        Assert.Single(payments);
        Assert.Equal(PaymentStatus.Approved, payments[0].Status);
    }

    // Mercado Pago re-notifies an offline payment while it is still unpaid, and J2 finds it again
    // every five minutes: repeating "undecided" is the common case, not an edge one. It used to
    // reach Settle, which refuses a status that is not a decision, so the webhook answered 500 —
    // and a provider that retries until 2xx would have looped on it, taking J2's batch down too.
    [Fact]
    public async Task A_payment_that_stays_undecided_can_be_reported_again()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 18);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0113");

        async Task<HttpResponseMessage> ReportPendingAsync() =>
            await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
            {
                bookingId = created.Id, externalId = "fake-pending-repeat", approved = false,
                amount = created.ChargeAmount, outcome = "pending"
            });

        Assert.Equal(HttpStatusCode.OK, (await ReportPendingAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ReportPendingAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ReportPendingAsync()).StatusCode);

        Assert.Equal(BookingStatus.PendingPayment, (await SnapshotAsync(client, created)).Status);

        // And it still settles afterwards, on the same row.
        var approved = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-pending-repeat", approved = true,
            amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(BookingStatus.Confirmed, (await SnapshotAsync(client, created)).Status);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payments = await db.Payments.Where(payment => payment.BookingId == created.Id).ToListAsync();
        Assert.Single(payments);
        Assert.Equal(PaymentStatus.Approved, payments[0].Status);
    }

    [Fact]
    public async Task A_payment_still_pending_is_not_money_the_booking_has()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 17);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0111");

        await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-pending-2", approved = false,
            amount = created.ChargeAmount, outcome = "pending"
        });

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.PendingPayment, snapshot.Status);
        Assert.Equal(0, snapshot.PaidAmount);
    }

    // A rejection is still terminal for that payment id: the buyer retries with a new one.
    [Fact]
    public async Task A_replayed_rejection_stays_a_no_op()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 18);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0112");

        for (var attempt = 0; attempt < 2; attempt++)
            await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
            {
                bookingId = created.Id, externalId = "fake-reject-replay", approved = false,
                amount = created.ChargeAmount
            });

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payments = await db.Payments.Where(payment => payment.BookingId == created.Id).ToListAsync();
        Assert.Single(payments);
        Assert.Equal(PaymentStatus.Rejected, payments[0].Status);
        Assert.Equal(BookingStatus.PendingPayment, (await SnapshotAsync(client, created)).Status);
    }

    [Fact]
    public async Task A_deposit_charges_the_club_percentage()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 15);

        var created = await HoldAsync(client, court.Id, date, slot, "onlineDeposit", "362 400-0104");

        // Seeded club: depositPercent = 50.
        Assert.Equal(Math.Round(created.Price / 2, 2), created.ChargeAmount);
    }

    [Fact]
    public async Task A_deposit_paid_after_the_club_moved_the_percentage_is_still_the_agreed_one()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 26);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineDeposit", "362 400-0122");
        Assert.Equal(Math.Round(created.Price / 2, 2), created.ChargeAmount);

        try
        {
            // The club switches to charging the whole price up front while this checkout is in flight.
            await SetDepositPercentAsync(100);

            var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
            {
                bookingId = created.Id, externalId = "fake-frozen-1", approved = true, amount = created.ChargeAmount
            });
            Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

            // The customer paid exactly what was asked of them. Recomputing against the live setting is
            // what used to file a correct deposit as a short payment and leave the money orphaned.
            var tenantContext = new AsyncLocalTenantContext();
            await using var db = postgres.CreateDbContext(tenantContext);
            using var scope = tenantContext.BeginScope(SeedTenant);
            var payment = await db.Payments.SingleAsync(candidate => candidate.BookingId == created.Id);
            Assert.Equal(PaymentStatus.Approved, payment.Status);
            Assert.Null(payment.OrphanReason);
            var booking = await db.Bookings.SingleAsync(candidate => candidate.Id == created.Id);
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.Equal(50, booking.DepositPercent);
        }
        finally
        {
            await SetDepositPercentAsync(50);
        }
    }

    private async Task SetDepositPercentAsync(int percent)
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        await db.Database.ExecuteSqlAsync($"UPDATE public.clubs SET \"depositPercent\" = {percent}");
    }

    [Fact]
    public async Task An_expired_hold_stops_blocking_the_slot()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 16);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0105");

        // Time travel: force the hold past its TTL straight in the database.
        var tenantContext = new AsyncLocalTenantContext();
        await using (var db = postgres.CreateDbContext(tenantContext))
        {
            using var scope = tenantContext.BeginScope(SeedTenant);
            await db.Bookings.Where(booking => booking.Id == created.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(booking => booking.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var retry = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Segundo Cliente", customerPhone = "362 400-0106", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        await using var verify = postgres.CreateDbContext(tenantContext);
        using var verifyScope = tenantContext.BeginScope(SeedTenant);
        var stale = await verify.Bookings.SingleAsync(booking => booking.Id == created.Id);
        Assert.Equal(BookingStatus.Expired, stale.Status);
    }

    [Fact]
    public async Task Releasing_a_hold_frees_the_slot_immediately()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 17);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0107");

        var release = await SendWithTokenAsync(client, HttpMethod.Post,
            $"/api/portal/chaco-for-ever/bookings/{created.Id}/release", created.Token);
        Assert.Equal(HttpStatusCode.NoContent, release.StatusCode);

        var retry = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Segundo Cliente", customerPhone = "362 400-0108", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);

        // Expired and not Cancelled: giving up is the same fact as running out of time, and Cancelled
        // is reserved for a person who decided and left a reason.
        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.Expired, snapshot!.Status);
    }

    [Fact]
    public async Task Releasing_never_touches_a_confirmed_booking()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 18);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0109");
        await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-release-1", approved = true, amount = created.ChargeAmount
        });

        var release = await SendWithTokenAsync(client, HttpMethod.Post,
            $"/api/portal/chaco-for-ever/bookings/{created.Id}/release", created.Token);

        Assert.Equal(HttpStatusCode.NoContent, release.StatusCode);
        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.Confirmed, snapshot!.Status);
    }

    [Fact]
    public async Task A_payment_landing_on_a_released_hold_confirms_it_while_the_slot_is_free()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 19);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0110");
        await SendWithTokenAsync(client, HttpMethod.Post,
            $"/api/portal/chaco-for-ever/bookings/{created.Id}/release", created.Token);

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-orphan-1", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        // Whoever pressed "Volver" and paid anyway gets the slot, exactly like whoever let the TTL run
        // out and paid anyway. Nobody else took it in between, so there is nothing to be orphaned about.
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payment = await db.Payments.SingleAsync(candidate => candidate.BookingId == created.Id);
        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Null(payment.OrphanReason);
        var booking = await db.Bookings.SingleAsync(candidate => candidate.Id == created.Id);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public async Task A_payment_landing_on_a_released_hold_is_orphaned_once_the_slot_is_gone()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 25);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0120");
        await SendWithTokenAsync(client, HttpMethod.Post,
            $"/api/portal/chaco-for-ever/bookings/{created.Id}/release", created.Token);

        // The slot the release freed is sold to somebody else before the first payment shows up.
        var second = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Segundo Cliente", customerPhone = "362 400-0121", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-orphan-2", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payment = await db.Payments.SingleAsync(candidate => candidate.BookingId == created.Id);
        Assert.Equal(PaymentStatus.ApprovedOrphan, payment.Status);
        Assert.Equal(PaymentOrphanReason.SlotLost, payment.OrphanReason);
    }

    [Fact]
    public async Task A_second_payment_on_a_fully_paid_booking_is_orphaned()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 20);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0111");
        await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-double-1", approved = true, amount = created.ChargeAmount
        });

        // The operator reissued the link and the customer paid it twice.
        var second = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-double-2", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payments = await db.Payments.Where(payment => payment.BookingId == created.Id)
            .OrderBy(payment => payment.CreatedAt).ToListAsync();
        Assert.Equal(2, payments.Count);
        Assert.Equal(PaymentStatus.Approved, payments[0].Status);
        Assert.Equal(PaymentStatus.ApprovedOrphan, payments[1].Status);
        Assert.Equal(PaymentOrphanReason.Duplicate, payments[1].OrphanReason);
    }

    [Fact]
    public async Task The_balance_of_a_deposit_booking_is_an_ordinary_payment()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 21);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineDeposit", "362 400-0112");
        await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-deposit-1", approved = true, amount = created.ChargeAmount
        });

        // Confirmed by the deposit, the booking still owes the balance: that is not duplicate money.
        var balance = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-deposit-2", approved = true,
            amount = created.Price - created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, balance.StatusCode);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payments = await db.Payments.Where(payment => payment.BookingId == created.Id).ToListAsync();
        Assert.Equal(2, payments.Count);
        Assert.All(payments, payment => Assert.Equal(PaymentStatus.Approved, payment.Status));
        Assert.Equal(created.Price, payments.Sum(payment => payment.Amount.Amount));
        Assert.Equal(PaymentKind.Deposit, payments.Single(payment => payment.ExternalId == "fake-deposit-1").Kind);
        Assert.Equal(PaymentKind.Balance, payments.Single(payment => payment.ExternalId == "fake-deposit-2").Kind);
    }

    [Fact]
    public async Task A_payment_short_of_the_asking_price_never_confirms_the_slot()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 24);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0115");

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-short-1", approved = true,
            amount = created.ChargeAmount - 1m
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.PendingPayment, snapshot!.Status);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payment = await db.Payments.SingleAsync(candidate => candidate.BookingId == created.Id);
        Assert.Equal(PaymentStatus.ApprovedOrphan, payment.Status);
        Assert.Equal(PaymentOrphanReason.Short, payment.OrphanReason);
    }

    [Fact]
    public async Task A_payment_settled_in_another_currency_is_recorded_in_that_currency()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 26);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0116");

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-currency-1", approved = true,
            amount = created.ChargeAmount, currency = "USD"
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payment = await db.Payments.SingleAsync(candidate => candidate.BookingId == created.Id);
        Assert.Equal("USD", payment.Amount.Currency);
        Assert.Equal(PaymentStatus.ApprovedOrphan, payment.Status);
        Assert.Equal(PaymentOrphanReason.WrongCurrency, payment.OrphanReason);
        var booking = await db.Bookings.SingleAsync(candidate => candidate.Id == created.Id);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
    }

    [Fact]
    public async Task The_exact_deposit_the_site_asked_for_confirms_the_slot()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 25);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineDeposit", "362 400-0116");

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-exact-1", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.Confirmed, snapshot!.Status);
        Assert.Equal(created.ChargeAmount, snapshot.PaidAmount);
    }

    [Fact]
    public async Task The_portal_refuses_to_confirm_a_slot_against_a_promise_to_pay_at_the_club()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 22);

        var response = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Vivo Vivaldi", customerPhone = "362 400-0113", customerEmail = (string?)null,
            paymentMode = "club", returnUrl = "http://localhost:5183/?retorno=x"
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        Assert.False(await db.Bookings.AnyAsync());
    }

    [Fact]
    public async Task A_portal_booking_without_a_payment_mode_defaults_to_paying_online()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 23);

        var response = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Sin Modo", customerPhone = "362 400-0114", customerEmail = (string?)null,
            returnUrl = "http://localhost:5183/?retorno=x"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(TestJsonOptions.Default);
        Assert.Equal(BookingStatus.PendingPayment, created!.Status);
        Assert.Equal(created.Price, created.ChargeAmount);
    }

    [Fact]
    public async Task Flooding_the_portal_with_holds_runs_out_of_permits()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 26);
        var request = new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Bot Flood", customerPhone = "362 400-0117", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        };

        // A permit is spent per request, whatever the request turns out to be worth.
        var codes = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 12; attempt++)
            codes.Add((await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", request)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, codes[0]);
        Assert.Equal(10, codes.Count(code => code != HttpStatusCode.TooManyRequests));
        Assert.Equal(2, codes.Count(code => code == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Settling_an_unknown_booking_never_reaches_the_provider()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var settle = await client.PostAsync(
            $"/api/portal/chaco-for-ever/bookings/{Guid.NewGuid()}/settle", null);

        Assert.Equal(HttpStatusCode.NotFound, settle.StatusCode);
    }

    [Fact]
    public async Task A_visitor_holding_only_the_id_can_neither_read_nor_release_the_booking()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 27);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0118");
        var path = $"/api/portal/chaco-for-ever/bookings/{created.Id}";

        var readNoToken = await SendWithTokenAsync(client, HttpMethod.Get, path, null);
        var readWrongToken = await SendWithTokenAsync(client, HttpMethod.Get, path, new string('a', 64));
        var release = await SendWithTokenAsync(client, HttpMethod.Post, path + "/release", null);

        Assert.Equal(HttpStatusCode.NotFound, readNoToken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, readWrongToken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, release.StatusCode);
        // The hold the attacker tried to free is untouched, so the slot stays the buyer's.
        Assert.Equal(BookingStatus.PendingPayment, (await SnapshotAsync(client, created)).Status);
    }

    private static Task<HttpResponseMessage> SendWithTokenAsync(HttpClient client, HttpMethod method,
        string path, string? token)
    {
        var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Add("X-Booking-Token", token);
        return client.SendAsync(request);
    }

    // The portal has to be able to answer "pero yo pagué" without anyone opening the backoffice:
    // when it was booked, and every attempt the provider reported, rejected ones included.
    [Fact]
    public async Task The_booking_carries_when_it_was_made_and_every_payment_attempt()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 29);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineDeposit", "362 400-0119");

        await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "history-rejected", approved = false,
            amount = created.ChargeAmount, outcome = "rejected"
        });
        await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "history-approved", approved = true,
            amount = created.ChargeAmount
        });

        var snapshot = await SnapshotAsync(client, created);

        Assert.NotEqual(default, snapshot.CreatedAt);
        Assert.Equal(2, snapshot.Payments.Count);
        // Oldest first: the customer reads it as a story, not as a set.
        Assert.Equal("history-rejected", snapshot.Payments[0].ExternalId);
        Assert.Equal(PaymentStatus.Rejected, snapshot.Payments[0].Status);
        Assert.Equal(PaymentStatus.Approved, snapshot.Payments[1].Status);
        // A deposit is what was charged, and saying so is the difference between "pagaste" and
        // "pagaste la seña" — the balance is still owed at the counter.
        Assert.All(snapshot.Payments, payment => Assert.Equal(PaymentKind.Deposit, payment.Kind));
        Assert.All(snapshot.Payments, payment => Assert.Equal("ARS", payment.Currency));
        Assert.Equal(created.ChargeAmount, snapshot.PaidAmount);
    }

    private static async Task<SnapshotResponse> SnapshotAsync(HttpClient client, CreatedResponse created)
    {
        var response = await SendWithTokenAsync(client, HttpMethod.Get,
            $"/api/portal/chaco-for-ever/bookings/{created.Id}", created.Token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SnapshotResponse>(TestJsonOptions.Default))!;
    }

    private static async Task<CreatedResponse> HoldAsync(HttpClient client, Guid courtId, DateOnly date,
        SlotResponse slot, string mode, string phone)
    {
        var response = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Cliente Online", customerPhone = phone, customerEmail = (string?)null,
            paymentMode = mode, returnUrl = "http://localhost:5183/?retorno=x"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>(TestJsonOptions.Default))!;
    }

    private async Task<(CourtResponse Court, DateOnly Date, SlotResponse Slot)> FirstSlotAsync(HttpClient client, int daysAhead)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysAhead);
        var availability = await client.GetFromJsonAsync<AvailabilityResponse>(
            $"/api/portal/chaco-for-ever/availability?sport=padel&from={date:O}&to={date:O}", TestJsonOptions.Default);
        var day = Assert.Single(availability!.Days);
        var dayCourt = day.Courts.First(court => court.Slots.Count > 0);
        var catalog = await client.GetFromJsonAsync<CatalogResponse>("/api/portal/chaco-for-ever/catalog", TestJsonOptions.Default);
        var court = catalog!.Sports.SelectMany(group => group.Courts).Single(candidate => candidate.Id == dayCourt.CourtId);
        return (court, date, dayCourt.Slots[0]);
    }

    private async Task ResetAsync()
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        db.Payments.RemoveRange(db.Payments);
        db.BookingCheckouts.RemoveRange(db.BookingCheckouts);
        db.Bookings.RemoveRange(db.Bookings);
        db.AvailabilityOverrides.RemoveRange(db.AvailabilityOverrides);
        db.Courts.RemoveRange(db.Courts);
        db.Schedules.RemoveRange(db.Schedules);
        await db.SaveChangesAsync();
    }

    private sealed record CreatedResponse(Guid Id, decimal Price, decimal ChargeAmount, BookingStatus Status,
        DateTimeOffset? ExpiresAt, string? CheckoutUrl, string Token);
    private sealed record SnapshotResponse(Guid Id, Guid CourtId, string CourtName, Sport Sport, DateOnly Date,
        int StartMinute, int DurationMinutes, decimal Price, decimal PaidAmount, BookingStatus Status,
        PaymentMode PaymentMode, DateTimeOffset? ExpiresAt, DateTimeOffset CreatedAt,
        IReadOnlyList<PaymentLineResponse> Payments);
    private sealed record PaymentLineResponse(DateTimeOffset At, string Provider, string ExternalId,
        decimal Amount, string Currency, PaymentKind Kind, PaymentStatus Status);
    private sealed record CourtResponse(Guid Id, string Name, string Detail, bool IsCovered, int[] Durations);
    private sealed record SportResponse(Sport Sport, IReadOnlyList<CourtResponse> Courts);
    private sealed record ClubResponse(string Name, string? Venue, string Currency, int DepositPercent);
    private sealed record CatalogResponse(ClubResponse Club, IReadOnlyList<SportResponse> Sports, bool OnlinePayments);
    private sealed record SlotResponse(int StartMinute, int Duration, decimal Price);
    private sealed record DayCourtResponse(Guid CourtId, IReadOnlyList<SlotResponse> Slots);
    private sealed record DayResponse(DateOnly Date, IReadOnlyList<DayCourtResponse> Courts);
    private sealed record AvailabilityResponse(string Currency, IReadOnlyList<DayResponse> Days);
}
