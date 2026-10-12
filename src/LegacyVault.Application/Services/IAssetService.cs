using LegacyVault.Application.Dtos.Requests;
using LegacyVault.Application.Dtos.Responses;

namespace LegacyVault.Application.Services;

/// <summary>Nghiệp vụ tài sản trong kho của Owner đang đăng nhập.</summary>
public interface IAssetService
{
    /// <summary>
    /// Kiểm tra tệp, mã hóa phong bì, lưu bản mã và metadata.
    /// Lỗi: VaultNotFoundException, VaultNotEditableException, AssetLimitExceededException,
    /// StorageQuotaExceededException, InvalidAssetFileException.
    /// </summary>
    Task<AssetResponse> CreateAssetAsync(Guid vaultId, CreateAssetRequest request, UploadedFile file, CancellationToken ct);

    Task<List<AssetResponse>> GetAssetsAsync(Guid vaultId, CancellationToken ct);

    /// <summary>Owner tải tệp của mình (§5.2 bước 2): giải mã phiên bản mới nhất và kiểm tra checksum.</summary>
    Task<AssetFileContent> DownloadAssetAsync(Guid vaultId, Guid assetId, CancellationToken ct);
}
