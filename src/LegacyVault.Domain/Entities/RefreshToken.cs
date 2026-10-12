namespace LegacyVault.Domain.Entities;

/// <summary>
/// Một "phiên đăng nhập" lưu ở server (bảng RefreshTokens). Mỗi thiết bị đăng nhập có một chuỗi refresh token.
/// </summary>
/// <remarks>
/// Ý tưởng:
/// - Access token (JWT) sống ngắn (15 phút) và KHÔNG lưu DB → server không thu hồi được.
/// - Refresh token sống dài (7 ngày) và CÓ lưu DB → đăng xuất = đánh dấu RevokedAt.
/// - Xoay vòng: mỗi lần dùng refresh token để lấy token mới, token cũ bị thu hồi
///   và trỏ tới token mới (ReplacedByTokenId). Tất cả token cùng một lần đăng nhập
///   chung một FamilyId.
/// - Nếu ai đó dùng lại một token ĐÃ bị thu hồi → nhiều khả năng token bị đánh cắp
///   → thu hồi cả family, buộc đăng nhập lại.
/// - Ta chỉ lưu HASH của token. Lộ DB cũng không lấy được token thật để dùng.
/// Các luật trên được cài đặt trong AuthService.
/// </remarks>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Người sở hữu token (EF tự nạp khi dùng Include).</summary>
    public User User { get; set; } = null!;

    /// <summary>SHA-256 của token gốc, dạng hex.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public Guid FamilyId { get; set; }

    /// <summary>true = người dùng chọn "Ghi nhớ đăng nhập" → cookie sống qua khi tắt trình duyệt.</summary>
    public bool IsPersistent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Thời điểm bị thu hồi; null = còn hiệu lực (nếu chưa hết hạn).</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Id của token mới thay thế token này khi xoay vòng.</summary>
    public Guid? ReplacedByTokenId { get; set; }
}
