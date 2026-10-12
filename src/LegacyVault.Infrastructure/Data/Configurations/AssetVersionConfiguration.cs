using LegacyVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegacyVault.Infrastructure.Data.Configurations;

/// <summary>
/// Bảng AssetVersions: metadata + thông tin mã hóa của từng phiên bản tệp.
/// Các cột nhị phân có độ dài cố định vì AES-GCM luôn dùng nonce 12 byte, tag 16 byte, DEK bọc 60 byte.
/// </summary>
public sealed class AssetVersionConfiguration : IEntityTypeConfiguration<AssetVersion>
{
    public void Configure(EntityTypeBuilder<AssetVersion> builder)
    {
        builder.ToTable("AssetVersions");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(v => v.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Sha256Checksum).HasMaxLength(64).IsFixedLength().IsRequired();

        builder.Property(v => v.EncryptionAlgorithm).HasMaxLength(32).IsRequired();
        builder.Property(v => v.StoragePath).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Nonce).HasMaxLength(12).IsRequired();
        builder.Property(v => v.AuthTag).HasMaxLength(16).IsRequired();
        builder.Property(v => v.WrappedDataKey).HasMaxLength(128).IsRequired();
        builder.Property(v => v.KeyId).HasMaxLength(64).IsRequired();

        // Một tài sản không có hai "phiên bản 1".
        builder.HasIndex(v => new { v.AssetId, v.VersionNumber }).IsUnique();

        builder.HasOne(v => v.Asset)
            .WithMany(a => a.Versions)
            .HasForeignKey(v => v.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(v => v.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
