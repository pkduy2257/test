namespace LegacyVault.Domain.Enums;

/// <summary>
/// Tình trạng quyền dịch vụ của kho (SRS v3.14.1 §11.1 "Quyền dịch vụ cấp Vault", §8.4).
/// </summary>
public enum EntitlementStatus
{
    /// <summary>Hạng Free: lưu trong 20 MiB, tạo Container rỗng, eKYC và mua dịch vụ.</summary>
    Free = 0,
    /// <summary>Đang trong kỳ đã thanh toán.</summary>
    PaidActive = 1,
    /// <summary>Hết kỳ, đang ân hạn 180 ngày: chặn thao tác thêm, vẫn cho thao tác giảm (OPLAN-01).</summary>
    GraceReadOnly = 2,
    /// <summary>Hết ân hạn, chờ rà soát có người phụ trách (OPLAN-04).</summary>
    ExpiredReview = 3,
}
