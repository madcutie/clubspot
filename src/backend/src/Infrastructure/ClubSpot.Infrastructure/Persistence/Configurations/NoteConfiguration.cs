using ClubSpot.Domain.Core.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("personNote");
        builder.HasKey(note => note.Id);
        builder.Property(note => note.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(note => note.TenantId).HasColumnName("tenantId").IsRequired();
        builder.Property(note => note.PersonId).HasColumnName("personId").IsRequired();
        builder.Property(note => note.Text).HasColumnName("text").HasMaxLength(500).IsRequired();
        builder.Property(note => note.AuthorUserId).HasColumnName("authorUserId").IsRequired();
        builder.Property(note => note.CreatedAt).HasColumnName("createdAt").IsRequired();
        builder.HasIndex(note => new { note.TenantId, note.PersonId, note.CreatedAt });
    }
}
