namespace LegacyVault.Infrastructure.Security;

/// <summary>Tên claim ngắn gọn ta tự quy ước (thay cho URL dài của Microsoft).</summary>
public static class JwtClaimTypes
{
    public const string Role = "role";
    public const string SessionId = "sid";
}
