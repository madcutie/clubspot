using ClubSpot.SharedKernel.Primitives;

namespace ClubSpot.Domain.Bookings;

public sealed record AvailableSlot(int StartMinute, int DurationMinutes, Money Price);
