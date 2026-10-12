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

    /// <summary>
    /// Sửa tên/mô tả tài sản (EDIT-06), không tạo phiên bản mới. ifMatch là ETag client đang thấy (EDIT-07).
    /// Lỗi: PreconditionRequiredException (thiếu If-Match), VaultNotFoundException, AssetNotFoundException,
    /// VaultNotEditableException, ValidationFailedException, EditConflictException.
    /// </summary>
    Task<AssetResponse> UpdateAssetDetailsAsync(
        Guid vaultId, Guid assetId, UpdateAssetRequest request, string? ifMatch, CancellationToken ct);

    /// <summary>
    /// Thay nội dung tài sản bằng phiên bản mới (EDIT-03..05). expectedVersionNumber là số phiên bản client đang thấy.
    /// Lỗi: VaultNotFoundException, AssetNotFoundException, VaultNotEditableException, EditConflictException,
    /// InvalidAssetFileException, StorageQuotaExceededException.
    /// </summary>
    Task<AssetResponse> ReplaceAssetContentAsync(
        Guid vaultId, Guid assetId, int expectedVersionNumber, UploadedFile file, CancellationToken ct);
}
