using LegacyVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegacyVault.Infrastructure.Data.Configurations;

/// <summary>Bảng HandoverPlans: đúng một kế hoạch cho mỗi kho (SETUP-13).</summary>
public sealed class HandoverPlanConfiguration : IEntityTypeConfiguration<HandoverPlan>
{
    public void Configure(EntityTypeBuilder<HandoverPlan> builder)
    {
        builder.ToTable("HandoverPlans");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);

        // Quan hệ 1–1: EF tự tạo unique index trên VaultId, nên một kho không thể có hai kế hoạch.
        builder.HasOne(p => p.Vault)
            .WithOne(v => v.Plan)
            .HasForeignKey<HandoverPlan>(p => p.VaultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
