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

    /// <summary>
    /// Sửa tên/mô tả tài sản bằng MỘT câu UPDATE có điều kiện UpdatedAt = expectedUpdatedAt (EDIT-07),
    /// đồng thời ghi UpdatedAt = now. Trả false khi không có dòng nào khớp (đã bị sửa, hoặc không thuộc kho).
    /// </summary>
    Task<bool> TryUpdateDetailsAsync(
        Guid vaultId, Guid assetId, DateTimeOffset? expectedUpdatedAt, string name, string? description,
        DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Đánh dấu một phiên bản mới cần INSERT, cho tài sản đã có trong DB.
    /// Bắt buộc gọi: Id của phiên bản sinh sẵn trong code (Guid v7), nên nếu chỉ thêm vào asset.Versions
    /// thì EF tưởng đây là bản ghi cũ và sinh UPDATE thay vì INSERT.
    /// </summary>
    void AddVersion(AssetVersion version);

    /// <summary>
    /// Lưu phiên bản mới (đã AddVersion) cùng các thay đổi khác của request (một transaction).
    /// Trả false khi DB báo trùng (AssetId, VersionNumber): một request khác vừa lưu phiên bản cùng số trước.
    /// </summary>
    Task<bool> TrySaveNewVersionAsync(CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
