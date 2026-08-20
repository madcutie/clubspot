using ClubSpot.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.TenantId).HasColumnName("tenantId").IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
        builder.Property(u => u.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("passwordHash").HasMaxLength(500).IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("isActive").IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("createdAt").IsRequired();
        // Unique across tenants, not within one: sign-in resolves the club from the email (ADR-0018).
        builder.HasIndex(u => u.Email).IsUnique();

        builder.OwnsMany(u => u.UserRoles, roles =>
        {
            roles.ToTable("userRoles");
            roles.WithOwner().HasForeignKey("userId");
            roles.Property<Guid>("userId").HasColumnName("userId");
            roles.Property(r => r.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(40);
            roles.HasKey("userId", nameof(UserRole.Role));
        });
    }
}
