using ClubSpot.Domain.Core;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class ClubModuleConfiguration : IEntityTypeConfiguration<ClubModule>
{
    public void Configure(EntityTypeBuilder<ClubModule> builder)
    {
        builder.ToTable("club_module");
        builder.HasKey(module => new { module.TenantId, module.ModuleId });
        builder.Property(module => module.TenantId).HasColumnName("club_id");
        builder.Property(module => module.ModuleId)
            .HasColumnName("module_id")
            .HasMaxLength(40)
            .HasConversion(moduleId => moduleId.Value, value => ModuleId.From(value));
        builder.Property(module => module.ContractedAt).HasColumnName("contracted_at").IsRequired();
        builder.HasOne<Club>().WithMany().HasForeignKey(module => module.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
