using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LegacyVault.Application.Security;
using LegacyVault.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LegacyVault.Infrastructure.Security;

/// <summary>
/// Tạo access token (JWT) và refresh token (chuỗi ngẫu nhiên).
/// </summary>
/// <remarks>
/// JWT gồm 3 phần: header.payload.signature. Payload chỉ được mã hóa base64, AI CŨNG ĐỌC ĐƯỢC
/// (dán thử vào jwt.io) → không bao giờ để mật khẩu hay dữ liệu nhạy cảm trong JWT.
/// Chữ ký (HMAC-SHA256 với SigningKey) đảm bảo không ai sửa được payload.
/// </remarks>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(IOptions<JwtSettings> options, TimeProvider timeProvider)
    {
        _settings = options.Value;
        _timeProvider = timeProvider;
    }

    public TimeSpan RefreshTokenLifetime
    {
        get { return TimeSpan.FromDays(_settings.RefreshTokenDays); }
    }

    public AccessToken CreateAccessToken(User user)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_settings.AccessTokenMinutes);

        // Claim = một mẩu thông tin về người dùng nằm trong token.
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),   // "sub" = subject = userId
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtClaimTypes.Role, user.Role.ToString().ToUpperInvariant()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // id riêng của token
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                CreateSigningKey(_settings.SigningKey), SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken { Value = token, ExpiresAt = expiresAt };
    }

    public GeneratedRefreshToken CreateRefreshToken()
    {
        // 64 byte ngẫu nhiên từ bộ sinh số an toàn mật mã (KHÔNG dùng new Random()).
        var bytes = RandomNumberGenerator.GetBytes(64);
        var value = Base64UrlEncoder.Encode(bytes);
        return new GeneratedRefreshToken { Value = value, Hash = HashRefreshToken(value) };
    }

    public string HashRefreshToken(string value)
    {
        // Refresh token đã đủ ngẫu nhiên nên SHA-256 là đủ (khác với mật khẩu do người đặt).
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash); // 64 ký tự hex
    }

    /// <summary>Đổi chuỗi khóa thành khóa ký HMAC. Dùng chung cho lúc KÝ (ở đây) và lúc KIỂM TRA (InfrastructureConfig).</summary>
    internal static SymmetricSecurityKey CreateSigningKey(string key)
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
