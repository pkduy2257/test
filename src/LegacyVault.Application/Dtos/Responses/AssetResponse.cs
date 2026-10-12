using LegacyVault.Domain.Entities;

namespace LegacyVault.Application.Dtos.Responses;

/// <summary>
/// Metadata an toàn của một tài sản và phiên bản hiện tại.
/// </summary>
/// <remarks>
/// Cố ý KHÔNG trả nonce, tag, DEK đã bọc, KeyId hay StoragePath: FE không cần, và lộ ra chỉ giúp kẻ tấn công.
/// </remarks>
public class AssetResponse
{
    public Guid Id { get; set; }
    public Guid VaultId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // Phiên bản hiện tại (mới nhất).
    public Guid VersionId { get; set; }
    public int VersionNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256Checksum { get; set; } = string.Empty;

    public static AssetResponse From(Asset asset)
    {
        var latest = asset.Versions.OrderByDescending(v => v.VersionNumber).First();

        return new AssetResponse
        {
            Id = asset.Id,
            VaultId = asset.VaultId,
            Name = asset.Name,
            Description = asset.Description,
            Status = EnumCode.From(asset.Status),
            CreatedAt = asset.CreatedAt,
            VersionId = latest.Id,
            VersionNumber = latest.VersionNumber,
            FileName = latest.OriginalFileName,
            ContentType = latest.ContentType,
            SizeBytes = latest.SizeBytes,
            Sha256Checksum = latest.Sha256Checksum,
        };
    }
}
