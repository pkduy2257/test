using LegacyVault.Application.Services;
using LegacyVault.Domain.Entities;

namespace LegacyVault.Application.Dtos.Responses;

/// <summary>
/// Thông tin kho trả về cho FE: trạng thái kho, kế hoạch và quyền dịch vụ tách riêng (§11.1),
/// cùng dung lượng đã dùng/tối đa để hiển thị theo QUOTA-06.
/// </summary>
public class VaultResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>"OPEN" hoặc "CLOSED".</summary>
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>"DRAFT", "ACTIVE", "CHECKIN_OVERDUE"...</summary>
    public string PlanStatus { get; set; } = string.Empty;
    public DateTimeOffset? PlanActivatedAt { get; set; }

    /// <summary>"FREE", "XS", "XS_MAX".</summary>
    public string ServiceTier { get; set; } = string.Empty;

    /// <summary>"FREE", "PAID_ACTIVE", "GRACE_READ_ONLY", "EXPIRED_REVIEW".</summary>
    public string ServiceStatus { get; set; } = string.Empty;
    public DateTimeOffset? ServiceExpiresAt { get; set; }

    public long StorageQuotaBytes { get; set; }
    public long StorageUsedBytes { get; set; }
    public int BeneficiarySlots { get; set; }
    public int AssetCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Gửi lại trong header If-Match khi sửa tên/mô tả kho (EDIT-07). Đổi sau mỗi lần sửa.</summary>
    public string Etag { get; set; } = string.Empty;

    /// <summary>vault phải được nạp kèm Plan và Entitlement (IVaultRepository luôn làm vậy).</summary>
    public static VaultResponse From(Vault vault, int assetCount, long storageUsedBytes)
    {
        return new VaultResponse
        {
            Id = vault.Id,
            Name = vault.Name,
            Description = vault.Description,
            Status = EnumCode.From(vault.Status), // Open → "OPEN"
            ClosedAt = vault.ClosedAt,
            PlanStatus = EnumCode.From(vault.Plan.Status),
            PlanActivatedAt = vault.Plan.ActivatedAt,
            ServiceTier = EnumCode.From(vault.Entitlement.Tier), // XsMax → "XS_MAX"
            ServiceStatus = EnumCode.From(vault.Entitlement.Status),
            ServiceExpiresAt = vault.Entitlement.ServiceExpiresAt,
            StorageQuotaBytes = vault.Entitlement.StorageQuotaBytes,
            StorageUsedBytes = storageUsedBytes,
            BeneficiarySlots = vault.Entitlement.IncludedBeneficiarySlots + vault.Entitlement.ExtraBeneficiarySlots,
            AssetCount = assetCount,
            CreatedAt = vault.CreatedAt,
            UpdatedAt = vault.UpdatedAt,
            Etag = EntityTag.From(vault.UpdatedAt),
        };
    }
}
