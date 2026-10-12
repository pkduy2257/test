namespace LegacyVault.Domain.Enums;

/// <summary>
/// Trạng thái của kho nguồn (SRS mục 4.3 "Trạng thái của kho").
/// </summary>
/// <remarks>
/// Bước này mới dùng Draft. Các giá trị còn lại khai báo sẵn để luồng điểm danh / xác minh / bàn giao dùng sau.
/// Lưu DB dạng chữ (xem VaultConfiguration) nên thêm giá trị mới không làm sai dữ liệu cũ.
/// </remarks>
public enum VaultStatus
{
    /// <summary>Chủ sở hữu đang thiết lập kho.</summary>
    Draft = 0,
    /// <summary>Đã kích hoạt theo SETUP-01, chủ sở hữu vẫn điểm danh bình thường.</summary>
    Active = 1,
    /// <summary>Trễ điểm danh nhưng còn thời gian chờ.</summary>
    CheckInOverdue = 2,
    /// <summary>Hết thời gian chờ, cần kiểm tra hồ sơ.</summary>
    PendingVerification = 3,
    /// <summary>Hồ sơ đã được chấp thuận.</summary>
    ReadyForHandover = 4,
    /// <summary>Một số tài sản đã được nhận, một số chưa.</summary>
    PartiallyHandedOver = 5,
    /// <summary>Mọi tài sản đã kết thúc xử lý theo mục 4.4.</summary>
    Completed = 6,
    /// <summary>Có vấn đề chưa giải quyết; chưa phải trạng thái kết thúc.</summary>
    Frozen = 7,
}
