using System.ComponentModel.DataAnnotations;

namespace LegacyVault.Application.Dtos.Requests;

/// <summary>
/// Trường chữ của form POST /api/v1/vaults/{vaultId}/assets/{assetId}/versions (multipart/form-data).
/// Tệp mới đi kèm trong trường "file".
/// </summary>
public class UploadAssetVersionRequest
{
    /// <summary>
    /// Số phiên bản client đang thấy (versionNumber trong AssetResponse). Khác phiên bản hiện hành → 409 EDIT_CONFLICT (EDIT-07).
    /// </summary>
    [Required(ErrorMessage = "Thiếu expectedVersionNumber.")]
    [Range(1, int.MaxValue, ErrorMessage = "expectedVersionNumber phải từ 1 trở lên.")]
    public int? ExpectedVersionNumber { get; set; }
}
