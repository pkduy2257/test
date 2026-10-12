using LegacyVault.Domain.Enums;

namespace LegacyVault.Domain.Entities;

/// <summary>
/// Kho nguồn của một chủ sở hữu (bảng Vaults).
/// </summary>
/// <remarks>
/// MVP (M20): mỗi Owner chỉ có một kho chưa hoàn tất tại một thời điểm.
/// Luật này nằm trong VaultService, và được DB chặn thêm bằng filtered unique index (VaultConfiguration).
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

    /// <summary>Thời điểm chuyển sang Active (SETUP-01); null khi còn bản nháp.</summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    public List<Asset> Assets { get; set; } = new List<Asset>();
}
