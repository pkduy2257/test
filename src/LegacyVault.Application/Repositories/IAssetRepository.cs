using LegacyVault.Domain.Entities;

namespace LegacyVault.Application.Repositories;

/// <summary>Truy cập bảng Assets (kèm AssetVersions).</summary>
public interface IAssetRepository
{
    /// <summary>Tìm một tài sản trong đúng kho, kèm các phiên bản. Không thấy → null.</summary>
    Task<Asset?> FindByIdAsync(Guid vaultId, Guid assetId, CancellationToken ct);

    /// <summary>Mọi tài sản của một kho kèm phiên bản, cũ nhất trước. Chỉ để đọc.</summary>
    Task<List<Asset>> FindByVaultIdAsync(Guid vaultId, CancellationToken ct);

    Task<int> CountByVaultIdAsync(Guid vaultId, CancellationToken ct);

    /// <summary>
    /// Tổng byte bản rõ của phiên bản hiện hành của mọi tài sản trong kho (QUOTA-01).
    /// Chưa cộng phiên bản cũ được snapshot/grant/hold giữ, vì các luồng đó chưa có.
    /// </summary>
    Task<long> SumCurrentSizeBytesAsync(Guid vaultId, CancellationToken ct);

    /// <summary>Thêm tài sản; các AssetVersion trong asset.Versions được thêm cùng.</summary>
    void Add(Asset asset);

    Task SaveChangesAsync(CancellationToken ct);
}
