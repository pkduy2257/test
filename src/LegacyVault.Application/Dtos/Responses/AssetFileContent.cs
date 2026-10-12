namespace LegacyVault.Application.Dtos.Responses;

/// <summary>Tệp đã giải mã để controller trả về cho Owner (M21).</summary>
public class AssetFileContent
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Sha256Checksum { get; set; } = string.Empty;
}
