using LegacyVault.Application.Repositories;
using LegacyVault.Domain.Entities;
using LegacyVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LegacyVault.Infrastructure.Repositories;

/// <summary>
/// Cài đặt IUserRepository bằng EF Core.
/// </summary>
/// <remarks>
/// LƯU Ý: UserRepository và RefreshTokenRepository dùng CHUNG một AppDbContext trong mỗi request
/// (DbContext đăng ký Scoped). Vì vậy gọi SaveChangesAsync ở repository nào cũng lưu TẤT CẢ thay đổi
/// đang chờ (cả user lẫn token) trong MỘT transaction.
/// </remarks>
public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct)
    {
        // EF dịch biểu thức này thành: SELECT ... WHERE Email = @email
        return _db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        // AsNoTracking: chỉ đọc, không sửa → EF không cần theo dõi thay đổi, nhanh hơn.
        return _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return _db.SaveChangesAsync(ct);
    }
}
