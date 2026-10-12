using LegacyVault.Application.Exceptions;
using LegacyVault.Domain.Entities;
using LegacyVault.Domain.Enums;

namespace LegacyVault.Application.Services;

/// <summary>
/// Owner có được sửa kho/tài sản lúc này không (SRS v3.14.2 §5.5, ma trận EDIT-02).
/// </summary>
/// <remarks>
/// Dùng chung cho: tạo tài sản, sửa tên/mô tả kho, sửa tên/mô tả tài sản, thay nội dung tài sản.
/// Được sửa khi đủ cả ba:
/// 1. Kho còn OPEN (SETUP-18).
/// 2. Kế hoạch không có hồ sơ chứng tử/bàn giao/chặn: DRAFT, ACTIVE, CHECKIN_OVERDUE, AWAITING_CHECK (SETUP-05).
/// 3. Quyền dịch vụ là FREE, hoặc PAID_ACTIVE và CHƯA tới ServiceExpiresAt (OPLAN-01).
///    Hết hạn tính theo giờ server ngay tại request, không chờ job đổi Status (NFR-03).
/// Thao tác "giảm" (gỡ/xóa tài sản) được phép cả khi ân hạn; sẽ thêm hàm riêng khi làm chức năng xóa.
/// </remarks>
public static class VaultEditPolicy
{
    /// <summary>Không được sửa → VaultNotEditableException kèm reason.</summary>
    public static void EnsureCanEdit(Vault vault, DateTimeOffset now)
    {
        if (vault.Status != VaultStatus.Open)
        {
            throw new VaultNotEditableException(VaultNotEditableException.VaultClosed);
        }

        var plan = vault.Plan.Status;
        var planEditable = plan == PlanStatus.Draft
            || plan == PlanStatus.Active
            || plan == PlanStatus.CheckinOverdue
            || plan == PlanStatus.AwaitingCheck;
        if (!planEditable)
        {
            throw new VaultNotEditableException(VaultNotEditableException.PlanLocked);
        }

        if (IsServiceReadOnly(vault.Entitlement, now))
        {
            throw new VaultNotEditableException(VaultNotEditableException.ServiceReadOnly);
        }
    }

    private static bool IsServiceReadOnly(VaultEntitlement entitlement, DateTimeOffset now)
    {
        if (entitlement.Status == EntitlementStatus.Free)
        {
            return false;
        }

        if (entitlement.Status != EntitlementStatus.PaidActive)
        {
            return true; // GRACE_READ_ONLY, EXPIRED_REVIEW
        }

        // PAID_ACTIVE nhưng đã quá hạn mà job chưa kịp chuyển sang GRACE_READ_ONLY.
        return entitlement.ServiceExpiresAt != null && now >= entitlement.ServiceExpiresAt.Value;
    }
}
