using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClubSpot.Infrastructure.Persistence;

public sealed class TenantIdConverter() : ValueConverter<TenantId, Guid>(
    id => id.Value,
    value => TenantId.From(value));
