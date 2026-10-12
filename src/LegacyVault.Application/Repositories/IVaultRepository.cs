using LegacyVault.Domain.Entities;

namespace LegacyVault.Application.Repositories;

/// <summary>Truy cập bảng Vaults.</summary>
public interface IVaultRepository
{
    /// <summary>Tìm theo Id (có theo dõi thay đổi để service sửa rồi lưu). Không thấy → null.</summary>
    Task<Vault?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Mọi kho của một Owner, mới nhất trước. Chỉ để đọc.</summary>
    Task<List<Vault>> FindByOwnerIdAsync(Guid ownerId, CancellationToken ct);

    void Add(Vault vault);

    Task SaveChangesAsync(CancellationToken ct);
}
