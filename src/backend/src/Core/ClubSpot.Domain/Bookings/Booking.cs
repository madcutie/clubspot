using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Bookings;

public sealed class Booking : ITenantOwned
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid CourtId { get; private set; }
    public DateOnly Date { get; private set; }
    public int StartMinute { get; private set; }
    public int DurationMinutes { get; private set; }
    public Money Price { get; private set; }
    // Provisional walk-in contact; replaced by a person link when the identity flow lands (ADR-0012).
    public string CustomerName { get; private set; }
    public string? CustomerPhone { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    public Booking(Guid id, TenantId tenantId, Guid courtId, DateOnly date, int startMinute, int durationMinutes,
        Money price, string customerName, string? customerPhone, DateTimeOffset createdAt, Guid createdBy)
    {
        if (durationMinutes <= 0)
            throw new ArgumentException("A booking must have a positive duration.", nameof(durationMinutes));
        if (startMinute < 0 || startMinute + durationMinutes > 1440)
            throw new ArgumentException("A booking must start and end within the same day.", nameof(startMinute));
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name cannot be empty.", nameof(customerName));

        var trimmedName = customerName.Trim();
        if (trimmedName.Length > 120)
            throw new ArgumentOutOfRangeException(nameof(customerName), "Customer name cannot exceed 120 characters.");

        var trimmedPhone = customerPhone?.Trim();
        if (trimmedPhone is { Length: > 40 })
            throw new ArgumentOutOfRangeException(nameof(customerPhone), "Customer phone cannot exceed 40 characters.");

        Id = id;
        TenantId = tenantId;
        CourtId = courtId;
        Date = date;
        StartMinute = startMinute;
        DurationMinutes = durationMinutes;
        Price = price;
        CustomerName = trimmedName;
        CustomerPhone = string.IsNullOrEmpty(trimmedPhone) ? null : trimmedPhone;
        Status = BookingStatus.Confirmed;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public void Cancel(DateTimeOffset at)
    {
        if (Status == BookingStatus.Cancelled)
            throw new InvalidOperationException("The booking is already cancelled.");

        Status = BookingStatus.Cancelled;
        CancelledAt = at;
    }

    private Booking()
    {
        CustomerName = null!;
    }
}
