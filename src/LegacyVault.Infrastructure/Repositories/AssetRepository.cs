using LegacyVault.Application.Repositories;
using LegacyVault.Domain.Entities;
using LegacyVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LegacyVault.Infrastructure.Repositories;

/// <summary>Cài đặt IAssetRepository bằng EF Core.</summary>
public sealed class AssetRepository : IAssetRepository
{
    private readonly AppDbContext _db;

    public AssetRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Asset?> FindByIdAsync(Guid vaultId, Guid assetId, CancellationToken ct)
    {
        // Lọc cả VaultId: assetId đúng nhưng thuộc kho khác vẫn coi như không thấy.
        return _db.Assets
            .Include(a => a.Versions)
            .SingleOrDefaultAsync(a => a.Id == assetId && a.VaultId == vaultId, ct);
    }

    public Task<List<Asset>> FindByVaultIdAsync(Guid vaultId, CancellationToken ct)
    {
        return _db.Assets.AsNoTracking()
            .Include(a => a.Versions)
            .Where(a => a.VaultId == vaultId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<int> CountByVaultIdAsync(Guid vaultId, CancellationToken ct)
    {
        return _db.Assets.CountAsync(a => a.VaultId == vaultId, ct);
    }

    public Task<long> SumCurrentSizeBytesAsync(Guid vaultId, CancellationToken ct)
    {
        // Mỗi tài sản lấy kích thước của phiên bản mới nhất; EF dịch thành một câu SELECT SUM với subquery.
        return _db.Assets
            .Where(a => a.VaultId == vaultId)
            .Select(a => a.Versions
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => v.SizeBytes)
                .FirstOrDefault())
            .SumAsync(ct);
    }

    public void Add(Asset asset)
    {
        _db.Assets.Add(asset);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return _db.SaveChangesAsync(ct);
    }
}
