namespace LegacyVault.Application.Security;

/// <summary>Refresh token vừa sinh: Value gửi cho trình duyệt, Hash lưu vào DB.</summary>
public class GeneratedRefreshToken
{
    public string Value { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}
