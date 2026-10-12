namespace LegacyVault.Application.Exceptions;

/// <summary>409 - Kho không còn ở trạng thái cho phép Owner thêm/sửa tài sản (M21).</summary>
public class VaultNotEditableException : ApiException
{
    public VaultNotEditableException(string vaultStatus)
        : base(409, "VAULT_NOT_EDITABLE", "Kho đang ở trạng thái không cho phép thay đổi tài sản.")
    {
        ExtraData["vaultStatus"] = vaultStatus;
    }
}
