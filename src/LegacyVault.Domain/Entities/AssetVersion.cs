namespace LegacyVault.Domain.Entities;

/// <summary>
/// Một phiên bản tệp của tài sản (bảng AssetVersions) — chính là content_version_id trong SRS.
/// </summary>
/// <remarks>
/// Mã hóa phong bì (envelope encryption, SEC-01/02):
/// - Nội dung tệp được mã hóa AES-256-GCM bằng một khóa dữ liệu (DEK) ngẫu nhiên của RIÊNG phiên bản này.
/// - Bản mã nằm ở kho tệp (StoragePath), không nằm trong DB.
/// - DB chỉ giữ DEK ĐÃ BỌC bằng khóa chủ (KEK). KEK nằm ngoài DB (user-secrets / KMS),
///   nên lộ bản sao DB + kho tệp vẫn không giải mã được.
/// Không có cột nào chứa nội dung rõ hoặc khóa rõ.
/// </remarks>
public class AssetVersion : BaseEntity
{
    public Guid AssetId { get; set; }

    public Asset Asset { get; set; } = null!;

    /// <summary>1, 2, 3... tăng dần trong cùng một tài sản.</summary>
    public int VersionNumber { get; set; }

    /// <summary>Tên tệp gốc người dùng tải lên (chỉ để hiển thị và đặt tên khi tải về).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Kiểu nội dung do SERVER xác định từ chữ ký tệp, không tin giá trị client gửi.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Kích thước bản rõ, tính bằng byte.</summary>
    public long SizeBytes { get; set; }

    /// <summary>SHA-256 của bản rõ (hex chữ thường). Dùng để client kiểm tra tải đủ theo M15.</summary>
    public string Sha256Checksum { get; set; } = string.Empty;

    // ---- Thông tin mã hóa ----

    /// <summary>Tên thuật toán, ví dụ "AES-256-GCM". Lưu lại để sau này đổi thuật toán vẫn đọc được bản cũ.</summary>
    public string EncryptionAlgorithm { get; set; } = string.Empty;

    /// <summary>Đường dẫn tương đối của bản mã trong kho tệp.</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>Nonce 12 byte dùng khi mã hóa nội dung. Không bí mật, nhưng không bao giờ dùng lại với cùng khóa.</summary>
    public byte[] Nonce { get; set; } = Array.Empty<byte>();

    /// <summary>Thẻ xác thực 16 byte của GCM. Sai 1 bit bản mã/nonce/AAD → giải mã thất bại.</summary>
    public byte[] AuthTag { get; set; } = Array.Empty<byte>();

    /// <summary>DEK đã được bọc bằng KEK (nonce + bản mã DEK + tag). Không phải khóa rõ.</summary>
    public byte[] WrappedDataKey { get; set; } = Array.Empty<byte>();

    /// <summary>Id của KEK đã dùng để bọc DEK. Cần khi xoay vòng KEK: bản cũ vẫn biết mở bằng khóa nào.</summary>
    public string KeyId { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
