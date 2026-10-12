using LegacyVault.Domain.Enums;

namespace LegacyVault.Domain.Entities;

/// <summary>
/// Quyền dịch vụ hiện hành của một kho (bảng VaultEntitlements), quan hệ 1–1 với Vault.
/// </summary>
/// <remarks>
/// Đây là bản tổng hợp để đọc nhanh hạn mức. SRS §11.6: số liệu này chỉ được cập nhật từ nghiệp vụ
/// thanh toán (luồng P), không có API cho Admin tự sửa để cấp quyền. Các kỳ trả phí, đơn và giao dịch
/// sẽ có bảng riêng khi làm luồng thanh toán.
/// </remarks>
public class VaultEntitlement : BaseEntity
{
    public Guid VaultId { get; set; }

    public Vault Vault { get; set; } = null!;

    public ServiceTier Tier { get; set; }

    public EntitlementStatus Status { get; set; }

    /// <summary>Dung lượng tối đa, tính theo byte bản rõ (QUOTA-01: 1 MiB = 1.048.576 byte).</summary>
    public long StorageQuotaBytes { get; set; }

    /// <summary>Số suất người nhận kèm hạng dịch vụ (Free 0, XS 2, XS Max 5).</summary>
    public int IncludedBeneficiarySlots { get; set; }

    /// <summary>Số suất mua thêm còn hiệu lực trong kỳ hiện tại (ADDON-01..05).</summary>
    public int ExtraBeneficiarySlots { get; set; }

    /// <summary>Hết kỳ trả phí hiện tại; null với Free (Free không có kỳ).</summary>
    public DateTimeOffset? ServiceExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
