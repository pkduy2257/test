namespace LegacyVault.Domain.Enums;

/// <summary>
/// Trạng thái của kho (SRS v3.14.1 §11.1 "Vault").
/// </summary>
/// <remarks>
/// Chỉ nói kho còn dùng được hay đã đóng. Tiến trình bàn giao (nháp, điểm danh, xét chứng tử...) là trạng thái
/// của kế hoạch (PlanStatus); quyền dùng dịch vụ là EntitlementStatus. SRS tách riêng ba thứ này.
/// Lưu DB dạng chữ (xem VaultConfiguration) nên thêm giá trị mới không làm sai dữ liệu cũ.
/// </remarks>
public enum VaultStatus
{
    /// <summary>Kho đang dùng. Mọi kho mới tạo ở trạng thái này.</summary>
    Open = 0,
    /// <summary>
    /// Đã đóng (SETUP-18): chỉ khi kế hoạch CLOSED và mọi payload đã xóa. Giữ định danh và lịch sử;
    /// Owner không tạo được kho thay thế (SETUP-12).
    /// </summary>
    Closed = 1,
}
