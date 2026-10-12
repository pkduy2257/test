using LegacyVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegacyVault.Infrastructure.Data.Configurations;

public sealed class VaultConfiguration : IEntityTypeConfiguration<Vault>
{
    /// <summary>Tên unique index trên OwnerId. VaultRepository dựa vào tên này để nhận ra lỗi "Owner đã có kho".</summary>
    public const string OwnerUniqueIndexName = "UX_Vaults_OwnerId";

    public void Configure(EntityTypeBuilder<Vault> builder)
    {
        builder.ToTable("Vaults");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Description).HasMaxLength(1000);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(32);

        // SETUP-12 / DATA-04: mỗi Owner tối đa MỘT kho, không có điều kiện lọc — kho CLOSED vẫn giữ chỗ,
        // nên Owner không tạo được kho thay thế. DB chặn được cả hai request tạo kho chạy đồng thời.
        builder.HasIndex(v => v.OwnerId)
            .IsUnique()
            .HasDatabaseName(OwnerUniqueIndexName);

        // Restrict: không cho xóa user khi còn kho (dữ liệu thừa kế không được biến mất theo tài khoản).
        builder.HasOne(v => v.Owner)
            .WithMany()
            .HasForeignKey(v => v.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
