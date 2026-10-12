using LegacyVault.Domain.Entities;

namespace LegacyVault.Application.Repositories;

/// <summary>Truy cập bảng Vaults (kèm HandoverPlans và VaultEntitlements).</summary>
public interface IVaultRepository
{
    /// <summary>Tìm theo Id, kèm Plan và Entitlement (có theo dõi thay đổi). Không thấy → null.</summary>
    Task<Vault?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Kho duy nhất của một Owner (SETUP-12), kèm Plan và Entitlement. Chưa có → null.</summary>
    Task<Vault?> FindByOwnerIdAsync(Guid ownerId, CancellationToken ct);

    /// <summary>
    /// Thêm kho mới (cùng Plan và Entitlement của nó) rồi lưu ngay.
    /// Trả false khi DB từ chối vì Owner đã có kho, tức là một request khác vừa tạo kho trước
    /// (hai tab, bấm lặp, retry). Khi đó kho truyền vào không được lưu.
    /// </summary>
    Task<bool> TryAddAsync(Vault vault, CancellationToken ct);

    /// <summary>
    /// Sửa tên/mô tả kho bằng MỘT câu UPDATE có điều kiện UpdatedAt = expectedUpdatedAt (EDIT-07),
    /// đồng thời ghi UpdatedAt = now. Trả false khi không có dòng nào khớp: kho đã bị sửa sau khi client đọc.
    /// Không đi qua change tracker, nên entity đang nạp trong request không tự đổi theo.
    /// </summary>
    Task<bool> TryUpdateDetailsAsync(
        Guid vaultId, DateTimeOffset? expectedUpdatedAt, string name, string? description,
        DateTimeOffset now, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
