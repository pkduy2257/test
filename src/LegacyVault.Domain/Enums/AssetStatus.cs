namespace LegacyVault.Domain.Enums;

/// <summary>
/// Trạng thái của một tài sản (SRS mục 4.3 "Trạng thái của từng tài sản").
/// </summary>
/// <remarks>
/// Tài sản mới tạo luôn ở Locked: chưa ai ngoài Owner mở được.
/// Các trạng thái bàn giao chi tiết (đã cấp phiên nhận, đã cam kết...) sẽ thêm khi làm Luồng 4.
/// </remarks>
public enum AssetStatus
{
    /// <summary>Đang khóa — trạng thái bình thường khi chủ sở hữu còn quản lý kho.</summary>
    Locked = 0,
    /// <summary>Đang chờ xác minh hồ sơ sự kiện.</summary>
    PendingVerification = 1,
    /// <summary>Đủ điều kiện bàn giao.</summary>
    EligibleForHandover = 2,
    /// <summary>Bị đóng băng, ví dụ MISSING_RECIPIENT quá hạn.</summary>
    Frozen = 3,
}
