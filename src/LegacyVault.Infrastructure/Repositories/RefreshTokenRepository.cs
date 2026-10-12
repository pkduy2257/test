using LegacyVault.Application.Repositories;
using LegacyVault.Domain.Identity;
using LegacyVault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LegacyVault.Infrastructure.Repositories;

/// <summary>
/// Cài đặt IRefreshTokenRepository bằng EF Core.
/// </summary>
/// <remarks>
/// Dùng chung AppDbContext với UserRepository (xem ghi chú ở UserRepository).
/// </remarks>
public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct)
    {
        return _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    }

    public Task<RefreshToken?> FindByHashWithUserAsync(string tokenHash, CancellationToken ct)
    {
        // Include = JOIN để lấy luôn User trong một câu SQL.
        return _db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    }

    public Task<List<RefreshToken>> FindActiveByFamilyAsync(Guid familyId, CancellationToken ct)
    {
        return _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(ct);
    }

    public Task<List<RefreshToken>> FindActiveByUserAsync(Guid userId, CancellationToken ct)
    {
        return _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
    }

    public void Add(RefreshToken token)
    {
        _db.RefreshTokens.Add(token);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return _db.SaveChangesAsync(ct);
    }
}
