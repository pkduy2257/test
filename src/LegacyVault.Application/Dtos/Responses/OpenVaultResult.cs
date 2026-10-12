namespace LegacyVault.Application.Dtos.Responses;

/// <summary>
/// Kết quả của "Tạo kho của tôi": kho của Owner và cờ cho biết kho vừa được tạo hay đã có từ trước.
/// Controller dùng cờ này để trả 201 (vừa tạo) hoặc 200 (mở lại kho cũ).
/// </summary>
public class OpenVaultResult
{
    public VaultResponse Vault { get; set; } = null!;

    public bool Created { get; set; }
}
