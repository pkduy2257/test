using LegacyVault.Domain.Entities;

namespace LegacyVault.Application.Dtos.Responses;

/// <summary>Thông tin kho trả về cho FE.</summary>
public class VaultResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AssetCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }

    public static VaultResponse From(Vault vault, int assetCount)
    {
        return new VaultResponse
        {
            Id = vault.Id,
            Name = vault.Name,
            Description = vault.Description,
            Status = EnumCode.From(vault.Status), // Draft → "DRAFT"
            AssetCount = assetCount,
            CreatedAt = vault.CreatedAt,
            UpdatedAt = vault.UpdatedAt,
            ActivatedAt = vault.ActivatedAt,
        };
    }
}
