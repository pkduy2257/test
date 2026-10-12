using LegacyVault.Domain.Enums;

namespace LegacyVault.Domain.Entities;

/// <summary>
/// Tài khoản người dùng (bảng Users).
/// </summary>
/// <remarks>
/// Entity chỉ chứa dữ liệu (giống @Entity với getter/setter bên Java).
/// Luật nghiệp vụ như "sai 5 lần thì khóa 15 phút" nằm trong AuthService.
/// Cấu hình cột, index nằm ở Infrastructure/.../Configurations/UserConfiguration.
/// </remarks>
public class User : BaseEntity
{
    /// <summary>Email đã chuẩn hóa (bỏ khoảng trắng, chữ thường). Duy nhất trong bảng.</summary>
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>Mật khẩu ĐÃ BĂM bằng Argon2. Không bao giờ lưu mật khẩu gốc.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public UserStatus Status { get; set; }

    /// <summary>
    /// Thời điểm xác minh email (AUTH-04); null = chưa xác minh.
    /// SRS §11.1 tách "email đã xác minh" khỏi trạng thái tài khoản. Chưa xác minh thì không tạo được kho (AC-01).
    /// </summary>
    public DateTimeOffset? EmailVerifiedAt { get; set; }

    /// <summary>Số lần nhập sai mật khẩu liên tiếp.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Thời điểm hết khóa; null = không bị khóa.</summary>
    public DateTimeOffset? LockoutEndsAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
