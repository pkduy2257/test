using LegacyVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LegacyVault.Infrastructure.Data;

/// <summary>
/// DbContext thật của EF Core. Chỉ các Repository (thư mục Infrastructure/Repositories) dùng trực tiếp.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Mỗi DbSet ứng với một bảng.
    public DbSet<User> Users
    {
        get { return Set<User>(); }
    }

    public DbSet<RefreshToken> RefreshTokens
    {
        get { return Set<RefreshToken>(); }
    }

    public DbSet<Vault> Vaults
    {
        get { return Set<Vault>(); }
    }

    public DbSet<HandoverPlan> HandoverPlans
    {
        get { return Set<HandoverPlan>(); }
    }

    public DbSet<VaultEntitlement> VaultEntitlements
    {
        get { return Set<VaultEntitlement>(); }
    }

    public DbSet<Asset> Assets
    {
        get { return Set<Asset>(); }
    }

    public DbSet<AssetVersion> AssetVersions
    {
        get { return Set<AssetVersion>(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tự tìm mọi class IEntityTypeConfiguration<T> trong project này (thư mục Data/Configurations/)
        // → mỗi entity có file cấu hình riêng, không dồn hết vào đây.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
