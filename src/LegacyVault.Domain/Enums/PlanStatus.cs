namespace LegacyVault.Domain.Enums;

/// <summary>
/// Trạng thái của kế hoạch bàn giao (SRS v3.14.1 §11.1 "Kế hoạch", chuyển trạng thái ở §11.2).
/// </summary>
/// <remarks>
/// Bước tạo kho mới dùng Draft. Các giá trị còn lại khai báo sẵn cho luồng kích hoạt, điểm danh, chứng tử.
/// </remarks>
public enum PlanStatus
{
    /// <summary>Đang thiết lập; chưa điểm danh, chưa bàn giao.</summary>
    Draft = 0,
    /// <summary>Đã kích hoạt theo SETUP-01; lịch điểm danh đang chạy.</summary>
    Active = 1,
    /// <summary>Đến hạn mà Owner chưa điểm danh, đang trong thời gian chờ.</summary>
    CheckinOverdue = 2,
    /// <summary>Hết thời gian chờ; đã tạo việc cho Executor kiểm tra.</summary>
    AwaitingCheck = 3,
    /// <summary>Đã có hồ sơ chứng tử; snapshot đã ghim, chặn sửa cấu hình (SETUP-05).</summary>
    DeathCaseOpen = 4,
    /// <summary>Chứng tử đã duyệt; đang chạy F3.</summary>
    HandoverInProgress = 5,
    /// <summary>Mọi lượt bàn giao đã xong.</summary>
    Closed = 6,
    /// <summary>Bị chặn bởi hold/sự cố; lưu trạng thái trước và nguyên nhân.</summary>
    Blocked = 7,
}
