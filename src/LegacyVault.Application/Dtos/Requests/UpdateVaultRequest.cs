using System.ComponentModel.DataAnnotations;

namespace LegacyVault.Application.Dtos.Requests;

/// <summary>
/// Body của PATCH /api/v1/vaults/{vaultId} (EDIT-01): chỉ sửa tên và mô tả kho.
/// </summary>
/// <remarks>
/// Trường null/không gửi = giữ nguyên. name gửi lên mà chỉ toàn khoảng trắng → 400.
/// description = "" (hoặc toàn khoảng trắng) → xóa mô tả.
/// Tên/mô tả không mã hóa: không ghi mật khẩu, seed phrase vào đây (ASSET-02).
/// </remarks>
public class UpdateVaultRequest
{
    [MaxLength(200, ErrorMessage = "Tên kho tối đa 200 ký tự.")]
    public string? Name { get; set; }

    [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string? Description { get; set; }
}
