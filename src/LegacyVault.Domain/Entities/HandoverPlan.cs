using LegacyVault.Domain.Enums;

namespace LegacyVault.Domain.Entities;

/// <summary>
/// Kế hoạch bàn giao của một kho (bảng HandoverPlans), quan hệ 1–1 với Vault.
/// </summary>
/// <remarks>
/// SETUP-13: mỗi Vault có một kế hoạch hiện hành và một lịch điểm danh cho toàn kho; nhiều Container
/// không có đồng hồ riêng. Kế hoạch mới luôn ở Draft; chỉ Owner chủ động kích hoạt mới chuyển Active (SETUP-01).
/// </remarks>
public class HandoverPlan : BaseEntity
{
    public Guid VaultId { get; set; }

    public Vault Vault { get; set; } = null!;

    public PlanStatus Status { get; set; }

    /// <summary>
    /// Tăng mỗi khi cấu hình kế hoạch đổi (SETUP-04): Container, tài sản trong Container, chỉ định.
    /// Snapshot khi nộp chứng tử ghi lại số này. Bắt đầu từ 1.
    /// </summary>
    public int ConfigVersion { get; set; }

    /// <summary>Lần kích hoạt gần nhất (SETUP-01); null khi chưa từng kích hoạt.</summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
