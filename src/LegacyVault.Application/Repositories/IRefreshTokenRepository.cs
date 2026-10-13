using LegacyVault.Domain.Entities;

namespace LegacyVault.Application.Repositories;

/// <summary>
/// Truy cập bảng RefreshTokens.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>Tìm theo hash của token. Không thấy → null.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>Giống FindByHashAsync nhưng lấy kèm User (JOIN) trong cùng một câu SQL.</summary>
    Task<RefreshToken?> FindByHashWithUserAsync(string tokenHash, CancellationToken ct);

    /// <summary>Các token CHƯA bị thu hồi của một family (một lần đăng nhập trên một thiết bị).</summary>
    Task<List<RefreshToken>> FindActiveByFamilyAsync(Guid familyId, CancellationToken ct);

    /// <summary>Các token CHƯA bị thu hồi của một người dùng (mọi thiết bị).</summary>
    Task<List<RefreshToken>> FindActiveByUserAsync(Guid userId, CancellationToken ct);

    /// <summary>Đánh dấu "sẽ INSERT"; chỉ thật sự ghi DB khi gọi SaveChangesAsync.</summary>
    void Add(RefreshToken token);

    /// <summary>Ghi LastActivityAt = now cho token còn hiệu lực của phiên, nếu lần ghi trước cũ hơn touchedBefore (AUTH-07).</summary>
    Task TouchActivityAsync(Guid familyId, DateTimeOffset now, DateTimeOffset touchedBefore, CancellationToken ct);

    /// <summary>Lưu mọi thay đổi đang chờ xuống DB (xem ghi chú trong RefreshTokenRepository).</summary>
    Task SaveChangesAsync(CancellationToken ct);
}
