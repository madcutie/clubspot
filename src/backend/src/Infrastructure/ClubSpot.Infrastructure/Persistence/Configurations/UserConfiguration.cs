using ClubSpot.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
        builder.Property(u => u.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique().HasDatabaseName("ux_user_tenant_email");

        builder.OwnsMany(u => u.UserRoles, roles =>
        {
            roles.ToTable("user_role");
            roles.WithOwner().HasForeignKey("user_id");
            roles.Property<Guid>("user_id").HasColumnName("user_id");
            roles.Property(r => r.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(40);
            roles.HasKey("user_id", nameof(UserRole.Role));
        });
    }
}
