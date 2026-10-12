namespace LegacyVault.Infrastructure.Security;

/// <summary>
/// Cấu hình JWT, đọc từ mục "Jwt" trong appsettings / user-secrets / biến môi trường
/// (giống @ConfigurationProperties(prefix = "jwt") bên Spring Boot).
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "legacyvault-api";
    public string Audience { get; set; } = "legacyvault-web";

    /// <summary>
    /// Khóa bí mật để KÝ token. Ai có khóa này là tự tạo được token Admin,
    /// nên KHÔNG commit lên git — đặt bằng: dotnet user-secrets set "Jwt:SigningKey" "..."
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
