using LegacyVault.Domain.Enums;

namespace LegacyVault.Domain.Entities;

/// <summary>
/// Kho của một Owner (bảng Vaults).
/// </summary>
/// <remarks>
/// SETUP-12: mỗi Owner có tối đa MỘT kho trong toàn hệ thống, kể cả khi kho đã đóng.
/// Luật nằm trong VaultService và được DB chặn thêm bằng unique index trên OwnerId (VaultConfiguration).
/// Mỗi kho luôn đi kèm đúng một kế hoạch (Plan) và một quyền dịch vụ (Entitlement), tạo cùng lúc với kho.
/// </remarks>
public class Vault : BaseEntity
{
    public Guid OwnerId { get; set; }

    public User Owner { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public VaultStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Thời điểm chuyển CLOSED (SETUP-18); null khi kho còn mở.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    public HandoverPlan Plan { get; set; } = null!;

    public VaultEntitlement Entitlement { get; set; } = null!;

    public List<Asset> Assets { get; set; } = new List<Asset>();
}
