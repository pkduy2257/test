using System.ComponentModel.DataAnnotations;

namespace LegacyVault.Application.Dtos.Requests;

/// <summary>
/// Body (không bắt buộc) của POST /api/v1/vaults.
/// </summary>
/// <remarks>
/// §5.2 bước 1 chỉ có nút "Tạo kho của tôi", nên FE có thể gửi body rỗng; khi đó kho tên "Kho của tôi".
/// Nếu Owner đã có kho, tên/mô tả gửi lên bị bỏ qua và API trả lại kho cũ nguyên trạng.
/// </remarks>
public class CreateVaultRequest
{
    [MaxLength(200, ErrorMessage = "Tên kho tối đa 200 ký tự.")]
    public string? Name { get; set; }

    [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string? Description { get; set; }
}
