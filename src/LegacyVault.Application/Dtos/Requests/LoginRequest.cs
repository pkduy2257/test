using System.ComponentModel.DataAnnotations;

namespace LegacyVault.Application.Dtos.Requests;

// DTO (Data Transfer Object) = hình dạng dữ liệu đi qua API.
// Tách riêng khỏi entity để: (1) không lộ PasswordHash, (2) đổi DB không làm vỡ hợp đồng với FE.
// Tên property khớp với file FE: src/features/auth/model/auth.types.ts

/// <summary>Body của POST /api/v1/auth/login.</summary>
/// <remarks>
/// [Required], [EmailAddress]... là DataAnnotations (giống @NotBlank, @Email bên Java).
/// Nhờ [ApiController], ASP.NET tự kiểm tra và trả 400 trước khi vào controller — ta không phải viết if.
/// </remarks>
public class LoginRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
