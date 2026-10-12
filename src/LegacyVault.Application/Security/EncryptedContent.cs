namespace LegacyVault.Application.Security;

/// <summary>
/// Kết quả mã hóa phong bì một tệp: bản mã + những gì cần để giải mã lại (trừ KEK).
/// </summary>
public class EncryptedContent
{
    /// <summary>Tên thuật toán nội dung, ví dụ "AES-256-GCM".</summary>
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>Bản mã của tệp, cùng độ dài với bản rõ (GCM là mã dòng).</summary>
    public byte[] Ciphertext { get; set; } = Array.Empty<byte>();

    public byte[] Nonce { get; set; } = Array.Empty<byte>();

    public byte[] AuthTag { get; set; } = Array.Empty<byte>();

    /// <summary>DEK đã bọc bằng KEK.</summary>
    public byte[] WrappedDataKey { get; set; } = Array.Empty<byte>();

    /// <summary>Id của KEK đã bọc DEK.</summary>
    public string KeyId { get; set; } = string.Empty;
}
