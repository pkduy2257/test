using LegacyVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegacyVault.Infrastructure.Data.Configurations;

/// <summary>Bảng VaultEntitlements: quyền dịch vụ hiện hành, đúng một dòng cho mỗi kho.</summary>
public sealed class VaultEntitlementConfiguration : IEntityTypeConfiguration<VaultEntitlement>
{
    public void Configure(EntityTypeBuilder<VaultEntitlement> builder)
    {
        builder.ToTable("VaultEntitlements");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Tier).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasOne(e => e.Vault)
            .WithOne(v => v.Entitlement)
            .HasForeignKey<VaultEntitlement>(e => e.VaultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
