namespace LegacyVault.Application.Security;

/// <summary>
/// Mã hóa phong bì (envelope encryption) theo SEC-01..03.
/// </summary>
/// <remarks>
/// Mỗi lần Encrypt sinh một khóa dữ liệu (DEK) MỚI, mã hóa nội dung bằng DEK, rồi bọc DEK bằng khóa chủ (KEK).
/// Service gọi không bao giờ thấy DEK hay KEK — chỉ thấy bản mã và DEK đã bọc.
///
/// associatedData (AAD) là dữ liệu được xác thực nhưng không mã hóa, ví dụ "vault|asset|version".
/// Giải mã phải truyền ĐÚNG AAD đã dùng khi mã hóa; nếu ai đó tráo bản mã của tài sản A sang hàng của
/// tài sản B trong DB, AAD không khớp → giải mã thất bại thay vì trả nhầm tệp.
/// </remarks>
public interface IEnvelopeEncryptionService
{
    EncryptedContent Encrypt(byte[] plaintext, byte[] associatedData);

    /// <summary>
    /// Giải mã và kiểm tra toàn vẹn. Bản mã/nonce/tag/AAD/DEK bọc bị sửa hoặc KEK không đúng
    /// → System.Security.Cryptography.CryptographicException.
    /// </summary>
    byte[] Decrypt(EncryptedContent content, byte[] associatedData);
}
