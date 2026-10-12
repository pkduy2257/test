using LegacyVault.Domain.Identity;

namespace LegacyVault.Application.Repositories;

/// <summary>
/// Truy cập bảng Users (giống interface UserRepository extends JpaRepository bên Spring).
/// </summary>
/// <remarks>
/// Service chỉ biết interface này; câu truy vấn EF Core nằm ở Infrastructure/Repositories/UserRepository.
/// </remarks>
public interface IUserRepository
{
    /// <summary>Tìm theo email ĐÃ chuẩn hóa. Không thấy → null.</summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);

    /// <summary>Tìm theo Id, chỉ để đọc (sửa object trả về sẽ KHÔNG được lưu).</summary>
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Lưu mọi thay đổi đang chờ xuống DB (xem ghi chú trong UserRepository).</summary>
    Task SaveChangesAsync(CancellationToken ct);
}
