using LegacyVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegacyVault.Infrastructure.Data.Configurations;

public sealed class VaultConfiguration : IEntityTypeConfiguration<Vault>
{
    public void Configure(EntityTypeBuilder<Vault> builder)
    {
        builder.ToTable("Vaults");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Description).HasMaxLength(1000);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(32);

        // M20: mỗi Owner tối đa một kho chưa hoàn tất. Filtered unique index chỉ xét các hàng
        // thỏa điều kiện lọc → DB chặn được cả khi hai request tạo kho chạy đồng thời.
        builder.HasIndex(v => v.OwnerId)
            .IsUnique()
            .HasFilter("[Status] <> 'Completed'")
            .HasDatabaseName("UX_Vaults_OwnerId_Open");

        // Restrict: không cho xóa user khi còn kho (dữ liệu thừa kế không được biến mất theo tài khoản).
        builder.HasOne(v => v.Owner)
            .WithMany()
            .HasForeignKey(v => v.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
