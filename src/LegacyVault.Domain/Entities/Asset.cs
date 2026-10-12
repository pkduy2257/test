using LegacyVault.Domain.Enums;

namespace LegacyVault.Domain.Entities;

/// <summary>
/// Một tài sản trong kho (bảng Assets): tên, mô tả và các phiên bản tệp.
/// </summary>
/// <remarks>
/// Nội dung tệp KHÔNG nằm ở đây mà ở AssetVersion (ASSET-01: mỗi content version có ID, checksum và DEK riêng).
/// Sửa tệp = thêm phiên bản mới, không ghi đè phiên bản cũ, vì hồ sơ/chữ ký sau này ghim đúng một phiên bản.
/// </remarks>
public class Asset : BaseEntity
{
    public Guid VaultId { get; set; }

    public Vault Vault { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả ngắn, dùng cho lời mời người nhận sau này. Không chứa nội dung tệp.</summary>
    public string? Description { get; set; }

    public AssetStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public List<AssetVersion> Versions { get; set; } = new List<AssetVersion>();
}
