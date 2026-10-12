namespace LegacyVault.Application.Dtos.Responses;

/// <summary>Tệp đã giải mã để controller trả về cho Owner (§5.2 bước 2: Owner xem/tải dữ liệu của mình).</summary>
public class AssetFileContent
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Sha256Checksum { get; set; } = string.Empty;
}
