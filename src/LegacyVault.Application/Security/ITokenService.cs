using LegacyVault.Domain.Entities;

namespace LegacyVault.Application.Security;

/// <summary>
/// Sinh token. Application không đọc config (appsettings) nên thời hạn token
/// cũng do Infrastructure cung cấp qua interface này.
/// </summary>
public interface ITokenService
{
    TimeSpan RefreshTokenLifetime { get; }

    AccessToken CreateAccessToken(User user);
    GeneratedRefreshToken CreateRefreshToken();
    string HashRefreshToken(string value);
}
