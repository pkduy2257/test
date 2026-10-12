using System.Security.Cryptography;
using System.Text;
using LegacyVault.Application.Security;
using Microsoft.Extensions.Options;

namespace LegacyVault.Infrastructure.Security;

/// <summary>
/// Mã hóa phong bì bằng AES-256-GCM cho cả hai tầng (SEC-01..03).
/// </summary>
/// <remarks>
/// Tầng 1 — nội dung:  DEK (32 byte ngẫu nhiên, MỚI cho mỗi phiên bản tệp) + nonce 12 byte → bản mã + tag 16 byte.
/// Tầng 2 — bọc khóa:  KEK (từ EncryptionSettings) mã hóa DEK → WrappedDataKey = nonce(12) | DEK đã mã hóa(32) | tag(16).
///
/// Vì sao nonce ngẫu nhiên là an toàn ở đây?
/// - Mỗi DEK chỉ mã hóa ĐÚNG MỘT tệp, nên nonce không bao giờ lặp với cùng một DEK.
/// - KEK bọc rất nhiều DEK; nonce ngẫu nhiên 96 bit an toàn tới khoảng 2^32 lần bọc cho một KEK
///   (giới hạn NIST SP 800-38D) — dư rất xa cho đồ án; production nên xoay KEK định kỳ.
///
/// Muốn dùng KMS thật (Azure Key Vault, AWS KMS, Google Cloud KMS): chỉ thay WrapKey/UnwrapKey bằng lời gọi
/// "wrap/unwrap" của KMS. KEK khi đó không bao giờ rời khỏi KMS.
/// </remarks>
public sealed class AesGcmEnvelopeEncryptionService : IEnvelopeEncryptionService
{
    public const string AlgorithmName = "AES-256-GCM";

    private const int KeySizeBytes = 32;   // AES-256
    private const int NonceSizeBytes = 12; // 96 bit — kích thước chuẩn của GCM
    private const int TagSizeBytes = 16;   // 128 bit — tag đầy đủ, không cắt ngắn
    private const int WrappedKeySizeBytes = NonceSizeBytes + KeySizeBytes + TagSizeBytes;

    private readonly Dictionary<string, byte[]> _keys;
    private readonly string _activeKeyId;

    public AesGcmEnvelopeEncryptionService(IOptions<EncryptionSettings> settings)
    {
        _activeKeyId = settings.Value.ActiveKeyId;
        _keys = new Dictionary<string, byte[]>();
        foreach (var entry in settings.Value.Keys)
        {
            _keys[entry.Key] = Convert.FromBase64String(entry.Value);
        }
    }

    /// <summary>Dùng khi kiểm tra cấu hình lúc khởi động: chuỗi Base64 có giải ra đúng 32 byte không.</summary>
    public static bool IsValidKey(string base64)
    {
        try
        {
            return Convert.FromBase64String(base64).Length == KeySizeBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public EncryptedContent Encrypt(byte[] plaintext, byte[] associatedData)
    {
        var dataKey = RandomNumberGenerator.GetBytes(KeySizeBytes);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSizeBytes];

            using (var aes = new AesGcm(dataKey, TagSizeBytes))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
            }

            return new EncryptedContent
            {
                Algorithm = AlgorithmName,
                Ciphertext = ciphertext,
                Nonce = nonce,
                AuthTag = tag,
                WrappedDataKey = WrapKey(dataKey, _activeKeyId, associatedData),
                KeyId = _activeKeyId,
            };
        }
        finally
        {
            // DEK rõ chỉ sống trong khối này (SEC-03).
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public byte[] Decrypt(EncryptedContent content, byte[] associatedData)
    {
        if (content.Algorithm != AlgorithmName
            || content.Nonce.Length != NonceSizeBytes
            || content.AuthTag.Length != TagSizeBytes)
        {
            throw new CryptographicException("Định dạng bản mã không được hỗ trợ.");
        }

        var dataKey = UnwrapKey(content.WrappedDataKey, content.KeyId, associatedData);
        try
        {
            var plaintext = new byte[content.Ciphertext.Length];
            using (var aes = new AesGcm(dataKey, TagSizeBytes))
            {
                // Sai tag (bản mã/nonce/AAD bị sửa) → AuthenticationTagMismatchException (con của CryptographicException).
                aes.Decrypt(content.Nonce, content.Ciphertext, content.AuthTag, plaintext, associatedData);
            }

            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    // ===== Tầng bọc khóa (thay bằng KMS khi lên production) =====

    private byte[] WrapKey(byte[] dataKey, string keyId, byte[] associatedData)
    {
        var kek = GetKey(keyId);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var encryptedKey = new byte[KeySizeBytes];
        var tag = new byte[TagSizeBytes];

        using (var aes = new AesGcm(kek, TagSizeBytes))
        {
            aes.Encrypt(nonce, dataKey, encryptedKey, tag, BuildWrapAssociatedData(keyId, associatedData));
        }

        var wrapped = new byte[WrappedKeySizeBytes];
        nonce.CopyTo(wrapped, 0);
        encryptedKey.CopyTo(wrapped, NonceSizeBytes);
        tag.CopyTo(wrapped, NonceSizeBytes + KeySizeBytes);
        return wrapped;
    }

    private byte[] UnwrapKey(byte[] wrapped, string keyId, byte[] associatedData)
    {
        if (wrapped.Length != WrappedKeySizeBytes)
        {
            throw new CryptographicException("Khóa dữ liệu đã bọc có độ dài không hợp lệ.");
        }

        var kek = GetKey(keyId);
        var dataKey = new byte[KeySizeBytes];
        using (var aes = new AesGcm(kek, TagSizeBytes))
        {
            aes.Decrypt(
                wrapped.AsSpan(0, NonceSizeBytes),
                wrapped.AsSpan(NonceSizeBytes, KeySizeBytes),
                wrapped.AsSpan(NonceSizeBytes + KeySizeBytes, TagSizeBytes),
                dataKey,
                BuildWrapAssociatedData(keyId, associatedData));
        }

        return dataKey;
    }

    private byte[] GetKey(string keyId)
    {
        if (!_keys.TryGetValue(keyId, out var key))
        {
            // Không ghi giá trị khóa vào message/log, chỉ ghi id.
            throw new CryptographicException("Không có khóa chủ với id '" + keyId + "'.");
        }

        return key;
    }

    /// <summary>
    /// DEK đã bọc cũng gắn với đúng phiên bản tệp và đúng KEK: chép WrappedDataKey của tệp A sang hàng của
    /// tệp B, hoặc sửa cột KeyId, đều làm unwrap thất bại.
    /// </summary>
    private static byte[] BuildWrapAssociatedData(string keyId, byte[] contentAssociatedData)
    {
        var prefix = Encoding.UTF8.GetBytes("legacyvault/dek-wrap/v1|kek=" + keyId + "|");
        var result = new byte[prefix.Length + contentAssociatedData.Length];
        prefix.CopyTo(result, 0);
        contentAssociatedData.CopyTo(result, prefix.Length);
        return result;
    }
}
