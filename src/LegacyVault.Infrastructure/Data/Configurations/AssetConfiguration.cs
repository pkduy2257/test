using LegacyVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegacyVault.Infrastructure.Data.Configurations;

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(1000);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(a => a.VaultId);

        builder.HasOne(a => a.Vault)
            .WithMany(v => v.Assets)
            .HasForeignKey(a => a.VaultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
