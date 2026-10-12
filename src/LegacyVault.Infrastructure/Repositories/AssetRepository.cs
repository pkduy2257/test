using LegacyVault.Application.Repositories;
using LegacyVault.Domain.Entities;
using LegacyVault.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LegacyVault.Infrastructure.Repositories;

/// <summary>Cài đặt IAssetRepository bằng EF Core.</summary>
public sealed class AssetRepository : IAssetRepository
{
    // Mã lỗi SQL Server khi chèn trùng: 2601 = unique index, 2627 = unique constraint.
    private const int SqlDuplicateKeyIndex = 2601;
    private const int SqlDuplicateKeyConstraint = 2627;

    /// <summary>Unique index (AssetId, VersionNumber) do migration AddVaultsAndAssets tạo.</summary>
    private const string VersionNumberIndexName = "IX_AssetVersions_AssetId_VersionNumber";

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

    public void AddVersion(AssetVersion version)
    {
        _db.AssetVersions.Add(version);
    }

    public async Task<bool> TryUpdateDetailsAsync(
        Guid vaultId, Guid assetId, DateTimeOffset? expectedUpdatedAt, string name, string? description,
        DateTimeOffset now, CancellationToken ct)
    {
        // Kiểm tra ETag và ghi trong cùng một câu UPDATE, xem VaultRepository.TryUpdateDetailsAsync.
        var updated = await _db.Assets
            .Where(a => a.Id == assetId && a.VaultId == vaultId && a.UpdatedAt == expectedUpdatedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Name, name)
                .SetProperty(a => a.Description, description)
                .SetProperty(a => a.UpdatedAt, (DateTimeOffset?)now), ct);
        return updated == 1;
    }

    public async Task<bool> TrySaveNewVersionAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsDuplicateVersionNumber(ex))
        {
            // Bỏ theo dõi mọi thay đổi hỏng, để không lần SaveChanges nào sau trong request này ghi lại chúng.
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return _db.SaveChangesAsync(ct);
    }

    /// <summary>Lỗi do trùng (AssetId, VersionNumber), không phải lỗi DB nào khác (lỗi khác vẫn ném tiếp ra 500).</summary>
    private static bool IsDuplicateVersionNumber(DbUpdateException ex)
    {
        var sqlException = ex.InnerException as SqlException;
        if (sqlException == null)
        {
            return false;
        }

        var isDuplicate = sqlException.Number == SqlDuplicateKeyIndex
            || sqlException.Number == SqlDuplicateKeyConstraint;
        return isDuplicate && sqlException.Message.Contains(VersionNumberIndexName);
    }
}
