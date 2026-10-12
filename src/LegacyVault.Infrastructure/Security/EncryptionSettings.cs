namespace LegacyVault.Infrastructure.Security;

/// <summary>
/// Vòng khóa chủ (KEK) dùng để bọc khóa dữ liệu, đọc từ mục "Encryption".
/// </summary>
/// <remarks>
/// Đây là "mô-đun quản lý khóa có cấu hình bí mật riêng" thay cho KMS mà SEC-03 cho phép ở bản demo.
/// - Keys: id → khóa 32 byte dạng Base64. KHÔNG commit lên git; đặt bằng user-secrets (dev) hoặc
///   biến môi trường / Key Vault (production). SEC-02: KEK không nằm chung với bản sao DB.
/// - ActiveKeyId: khóa dùng để bọc DEK MỚI. Khóa cũ vẫn giữ trong Keys để mở các bản đã bọc trước đó
///   → xoay vòng KEK = thêm khóa mới + đổi ActiveKeyId, không cần mã hóa lại tệp.
/// </remarks>
public class EncryptionSettings
{
    public const string SectionName = "Encryption";

    public string ActiveKeyId { get; set; } = string.Empty;

    public Dictionary<string, string> Keys { get; set; } = new Dictionary<string, string>();
}
