namespace LegacyVault.Application.Dtos.Responses;

/// <summary>
/// Kết quả nội bộ giữa service và controller: có token để controller ghi vào cookie.
/// Không bao giờ trả nguyên object này ra JSON (controller chỉ trả Session).
/// </summary>
public class AuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset RefreshTokenExpiresAt { get; set; }

    /// <summary>true = "Ghi nhớ đăng nhập" → cookie refresh sống qua khi tắt trình duyệt.</summary>
    public bool IsPersistent { get; set; }

    public AuthSessionResponse Session { get; set; } = null!;
}
