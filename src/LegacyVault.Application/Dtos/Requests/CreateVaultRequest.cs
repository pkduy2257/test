using System.ComponentModel.DataAnnotations;

namespace LegacyVault.Application.Dtos.Requests;

/// <summary>Body của POST /api/v1/vaults.</summary>
public class CreateVaultRequest
{
    [Required(ErrorMessage = "Tên kho là bắt buộc.")]
    [MaxLength(200, ErrorMessage = "Tên kho tối đa 200 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string? Description { get; set; }
}
