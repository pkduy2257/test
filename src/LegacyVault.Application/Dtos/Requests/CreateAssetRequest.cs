using System.ComponentModel.DataAnnotations;

namespace LegacyVault.Application.Dtos.Requests;

/// <summary>
/// Các trường chữ của form POST /api/v1/vaults/{vaultId}/assets (multipart/form-data).
/// Tệp đi kèm trong trường "file", controller đổi sang UploadedFile.
/// </summary>
public class CreateAssetRequest
{
    [Required(ErrorMessage = "Tên tài sản là bắt buộc.")]
    [MaxLength(200, ErrorMessage = "Tên tài sản tối đa 200 ký tự.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả dùng cho lời mời người nhận sau này. Không ghi nội dung bí mật vào đây vì trường này không mã hóa.</summary>
    [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string? Description { get; set; }
}
