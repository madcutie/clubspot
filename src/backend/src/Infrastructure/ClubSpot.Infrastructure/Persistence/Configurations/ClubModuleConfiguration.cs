using ClubSpot.Domain.Core;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class ClubModuleConfiguration : IEntityTypeConfiguration<ClubModule>
{
    public void Configure(EntityTypeBuilder<ClubModule> builder)
    {
        builder.ToTable("clubModule");
        builder.HasKey(module => new { module.TenantId, module.ModuleId });
        builder.Property(module => module.TenantId).HasColumnName("clubId");
        builder.Property(module => module.ModuleId)
            .HasColumnName("moduleId")
            .HasMaxLength(40)
            .HasConversion(moduleId => moduleId.Value, value => ModuleId.From(value));
        builder.Property(module => module.ContractedAt).HasColumnName("contractedAt").IsRequired();
        builder.HasOne<Club>().WithMany().HasForeignKey(module => module.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
