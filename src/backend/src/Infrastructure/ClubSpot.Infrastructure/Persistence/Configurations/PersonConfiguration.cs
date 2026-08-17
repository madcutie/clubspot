using ClubSpot.Domain.Core.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people");
        builder.HasKey(person => person.Id);
        builder.Property(person => person.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(person => person.TenantId).HasColumnName("tenantId").IsRequired();
        builder.Property(person => person.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(person => person.SearchName).HasColumnName("searchName").HasMaxLength(120).IsRequired();
        builder.Property(person => person.Phone).HasColumnName("phone").HasMaxLength(30).IsRequired();
        builder.Property(person => person.PhoneDigits).HasColumnName("phoneDigits").HasMaxLength(20).IsRequired();
        builder.Property(person => person.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
        builder.Property(person => person.Origin).HasColumnName("origin").HasConversion<string>().HasMaxLength(20);
        builder.Property(person => person.IsBlocked).HasColumnName("isBlocked");
        builder.Property(person => person.CreatedAt).HasColumnName("createdAt");
        builder.Property(person => person.CreatedBy).HasColumnName("createdBy");
        builder.ComplexProperty(person => person.Debt, debt =>
        {
            debt.Property(value => value.Amount).HasColumnName("debtAmount").HasPrecision(14, 2);
            debt.Property(value => value.Currency).HasColumnName("debtCurrency").HasMaxLength(3).IsFixedLength();
        });
        builder.HasIndex(person => new { person.TenantId, person.SearchName });
        builder.HasIndex(person => new { person.TenantId, person.PhoneDigits });
        builder.HasMany(person => person.Notes).WithOne().HasForeignKey(note => note.PersonId).OnDelete(DeleteBehavior.Cascade);
    }
}
