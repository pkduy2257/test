using LegacyVault.Domain.Entities;
using LegacyVault.Domain.Enums;

namespace LegacyVault.Application.Security;

/// <summary>
/// Sinh token. Application không đọc config (appsettings) nên thời hạn token
/// cũng do Infrastructure cung cấp qua interface này.
/// </summary>
public interface ITokenService
{
    /// <summary>Thời gian tối đa của một phiên kể từ lúc đăng nhập (AUTH-07: 12 giờ).</summary>
    TimeSpan SessionLifetime { get; }

    /// <summary>Không thao tác quá thời gian này thì hết phiên (AUTH-07: 30 phút, Admin 15 phút).</summary>
    TimeSpan GetIdleTimeout(UserRole role);

    /// <summary>Access token gắn với phiên (claim sid); không sống quá hạn của phiên.</summary>
    AccessToken CreateAccessToken(User user, Guid sessionId, DateTimeOffset sessionExpiresAt);

    GeneratedRefreshToken CreateRefreshToken();
    string HashRefreshToken(string value);
}
