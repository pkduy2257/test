namespace LegacyVault.Application.Exceptions;

/// <summary>409 - Owner đã có một kho chưa hoàn tất (M20: tối đa một kho nguồn).</summary>
public class VaultAlreadyExistsException : ApiException
{
    public VaultAlreadyExistsException(Guid existingVaultId)
        : base(409, "VAULT_ALREADY_EXISTS", "Bạn đã có một kho đang thiết lập hoặc đang hoạt động.")
    {
        ExtraData["vaultId"] = existingVaultId;
    }
}
