namespace LegacyVault.Application.Exceptions;

/// <summary>
/// 500 - Bản mã của tài sản không giải mã được hoặc checksum không khớp.
/// </summary>
/// <remarks>
/// Nghĩa là bản mã/khóa bọc đã bị sửa, mất tệp, hoặc cấu hình KEK sai. Đây là sự cố cần điều tra
/// (Luồng 5), không phải lỗi của người dùng; message không nói chi tiết để không gợi ý cho kẻ tấn công.
/// </remarks>
public class AssetIntegrityException : ApiException
{
    public AssetIntegrityException()
        : base(500, "ASSET_INTEGRITY_FAILED", "Không thể mở tệp tài sản. Sự cố đã được ghi nhận.")
    {
    }
}
