using System.ComponentModel.DataAnnotations;

namespace LegacyVault.Application.Dtos.Requests;

/// <summary>
/// Body của PATCH /api/v1/vaults/{vaultId}/assets/{assetId} (EDIT-06): sửa tên/mô tả, không đụng nội dung tệp.
/// </summary>
/// <remarks>Quy ước giống UpdateVaultRequest: null = giữ nguyên, description rỗng = xóa mô tả.</remarks>
public class UpdateAssetRequest
{
    [MaxLength(200, ErrorMessage = "Tên tài sản tối đa 200 ký tự.")]
    public string? Name { get; set; }

    /// <summary>Không mã hóa: không ghi nội dung bí mật vào đây (ASSET-02).</summary>
    [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string? Description { get; set; }
}
